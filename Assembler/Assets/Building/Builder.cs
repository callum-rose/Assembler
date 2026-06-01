using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Compiler.Compiler;
using Assembler.Deserialisation;
using Assembler.Input;
using Assembler.Parsing;
using Assembler.Parsing.Controls;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Time;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assembler.Building
{
	public static class Builder
	{
		public static void Build(string yamlPath, InputPlatform? overridePlatform = null)
		{
			var yaml = File.ReadAllText(yamlPath);
			var gameDto = new GameFileParser().Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			var controls = ControlsTransformer.Transform(gameDto.Controls);
			Build(gameInfo, controls, overridePlatform);
		}

		public static void Build(GameInfo gameInfo) => Build(gameInfo, ControlsInfo.Empty, null);

		public static void Build(GameInfo gameInfo, ControlsInfo controls, InputPlatform? overridePlatform)
		{
			// 0. Enforce a game-over path so a game can never get stuck unfinishable.
			var hasCondition = gameInfo.GameOverCondition is not None<bool>;

			if (!hasCondition && !HasGameOverListener(gameInfo))
			{
				throw new InvalidOperationException(
					"Game descriptor must declare a game-over path (a top-level GameOverCondition " +
					"or a !gameover listener).");
			}

			// 0b. Resolve the active platform group and hard-fail on any used-but-unbound input action, then
			// build the live InputActionAsset for that platform. Mirrors the game-over check above.
			var platform = overridePlatform ?? PlatformSelector.Resolve();
			var activeGroup = PlatformFallback.ResolveGroup(platform, controls);

			ControlsValidator.Validate(gameInfo, controls, activeGroup);

			var controlsAsset = InputActionBuilder.Build(controls, activeGroup);

			// 1. Initialize variables and expressions
			var typeRegistry = BuiltInTypeRegistry.Default;

			var variableRegistry = new VariableRegistry();

			foreach (var variableInfo in gameInfo.Variables)
			{
				variableRegistry.Register(variableInfo);
			}

			var compiledExpressionsRegistry = new CompiledExpressionsRegistry(typeRegistry, new ExpressionMethodCompiler());

			compiledExpressionsRegistry.CompileAndRegisterAll(gameInfo.Expressions);

			// 2. Load assets
			var assetRegistry = new AssetRegistry();
			assetRegistry.LoadAll(gameInfo.Assets);

			// 3. Instantiate Entities and Behaviours
			var behaviourRegistry = new BehaviourRegistry();
			var entityTransformRegistry = new EntityTransformRegistry();

			// The single source of game time, injected everywhere timing matters. Created before the
			// registry and factory that depend on it. A driver MonoBehaviour ticks it once per frame
			// (ahead of every behaviour Update via DefaultExecutionOrder).
			var gameClock = new RealtimeGameClock();

			var exclusiveGroupRegistry = new ExclusiveGroupRegistry(gameClock);

			var templatesById = gameInfo.Templates.ToDictionary(t => t.Id, t => t);

			// The shared root parents every entity, so destroying it unloads the whole game.
			var gameRoot = new GameObject("Game");
			gameRoot.AddComponent<GameController>();
			gameRoot.AddComponent<GameClockDriver>().Clock = gameClock;

			// Enable the controls asset for the game's lifetime and tie its destruction to the game root, so
			// individual input triggers never enable/disable (and never leak) the shared asset themselves.
			gameRoot.AddComponent<ControlsAssetOwner>().Initialise(controlsAsset);

			var gameEntityFactory = new GameEntityFactory(
				variableRegistry,
				compiledExpressionsRegistry,
				behaviourRegistry,
				assetRegistry,
				entityTransformRegistry,
				exclusiveGroupRegistry,
				gameClock,
				templatesById,
				gameInfo.ParseContext,
				gameRoot.transform,
				controls,
				controlsAsset);

			var initialisations = new InitialisationQueue();

			// 4. Append the implicit game-over controller so it builds through the normal pipeline.
			var entities = gameInfo.Entities
				.Append(BuildGameOverControllerInfo(hasCondition ? gameInfo.GameOverCondition : null));

			foreach (var entityInfo in entities)
			{
				var result = gameEntityFactory.Create(entityInfo);
				behaviourRegistry.Register(result);
				initialisations.Enqueue(result);
			}

			// 5. Initialise Behaviours
			initialisations.ExecuteAll(behaviourRegistry);
		}

		/// <summary>
		/// Builds the implicit entity that ends the game. It always hosts an <see cref="EndGameInfo"/>
		/// behaviour (targeted by the <c>!gameover</c> listener); when a top-level GameOverCondition is
		/// present it also gets an every-frame trigger gated by that condition, both driving the same
		/// end-game behaviour.
		/// </summary>
		private static ConcreteEntityInfo BuildGameOverControllerInfo(ValueSource<bool>? condition)
		{
			var entityId = GameOverController.EntityId;
			var endId = GameOverController.EndBehaviourId;

			var behaviours = new List<BehaviourInfo>
			{
				new EndGameInfo(endId, Array.Empty<ListenerInfo>())
			};

			if (condition != null)
			{
				var toEnd = new ListenerInfo[] { new DirectListenerInfo(new BehaviourDescriptor(entityId, endId)) };
				var toGate = new ListenerInfo[] { new DirectListenerInfo(new BehaviourDescriptor(entityId, "gate")) };

				behaviours.Add(new EveryFrameTriggerInfo("tick", toGate));
				behaviours.Add(new ConditionGateInfo("gate", toEnd, condition));
			}

			return new ConcreteEntityInfo(
				entityId,
				Array.Empty<string>(),
				new ConstantSource<Vector3>(Vector3.zero),
				new ConstantSource<Vector3>(Vector3.zero),
				behaviours,
				Array.Empty<ValueInfo>(),
				Array.Empty<ChildEntityInfo>());
		}

		private static bool HasGameOverListener(GameInfo gameInfo)
		{
			bool InBehaviours(IEnumerable<BehaviourInfo> behaviours) =>
				behaviours.Any(b => b.Listeners.Any(l => l is GameOverListenerInfo));

			bool InChildren(IEnumerable<ChildEntityInfo> children) =>
				children.Any(c => InBehaviours(c.Behaviours) || InChildren(c.Children));

			bool InEntity(EntityInfo e) => InBehaviours(e.Behaviours) || InChildren(e.Children);

			return gameInfo.Entities.Any(InEntity) || gameInfo.Templates.Any(InEntity);
		}
	}
}
