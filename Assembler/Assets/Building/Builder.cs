using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.UI;
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
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

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
			=> gameInfo.Resolve(controls, overridePlatform).Instantiate();

		/// <summary>
		/// First build phase: validate the descriptor's game-over path and controls, then stand up all the
		/// registries (variables, expressions, assets, localisation) and the live input asset. Performs no
		/// scene instantiation, so it can be run on its own (e.g. by the sandbox validator) to attribute
		/// resolution-time failures separately from instantiation-time ones. The returned handle carries the
		/// resolved state into <see cref="Instantiate"/>.
		/// </summary>
		public static ResolvedGame Resolve(this GameInfo gameInfo, ControlsInfo controls, InputPlatform? overridePlatform)
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

			// 2b. Load the localisation string table. Locale is hardcoded for now; this is the seam the
			// future settings/options system will drive.
			var localeSettings = new LocaleSettings("en");
			var stringTableRegistry = new StringTableRegistry(localeSettings);
			stringTableRegistry.LoadAll(gameInfo.Localisation);

			return new ResolvedGame(
				gameInfo,
				controls,
				controlsAsset,
				variableRegistry,
				compiledExpressionsRegistry,
				assetRegistry,
				stringTableRegistry);
		}

		/// <summary>
		/// Second build phase: instantiate the game root, every entity/behaviour and the implicit game-over
		/// controller from an already-<see cref="Resolve"/>d game, then run deferred behaviour initialisation.
		/// Returns the root "Game" GameObject so callers can tear the whole game down by destroying it (the
		/// sandbox validator relies on this).
		/// </summary>
		public static GameObject Instantiate(this ResolvedGame resolved)
		{
			var gameInfo = resolved.GameInfo;
			var controls = resolved.Controls;
			var controlsAsset = resolved.ControlsAsset;
			var variableRegistry = resolved.VariableRegistry;
			var compiledExpressionsRegistry = resolved.CompiledExpressionsRegistry;
			var assetRegistry = resolved.AssetRegistry;
			var stringTableRegistry = resolved.StringTableRegistry;

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

			// uGUI needs exactly one EventSystem to deliver pointer input. The project is Input System-only
			// (activeInputHandler == 2), so the Input System UI module is required — StandaloneInputModule
			// would silently deliver no clicks. Parented to gameRoot so it unloads with the game.
			if (EventSystem.current == null)
			{
				var eventSystem = new GameObject("EventSystem");
				eventSystem.transform.SetParent(gameRoot.transform, worldPositionStays: false);
				eventSystem.AddComponent<EventSystem>();
				eventSystem.AddComponent<InputSystemUIInputModule>();
			}

			// Reusable UI prefab library the leaf UI blocks instantiate. The library asset is committed at
			// Resources/UI, so it must always load; a missing asset is a project setup error, not a per-game
			// condition, so fail fast here rather than threading a nullable reference through the build.
			var uiPrefabs = Resources.Load<UiPrefabLibrary>(UiPrefabLibrary.DefaultResourcePath)
				?? throw new InvalidOperationException(
					$"UiPrefabLibrary not found at Resources/{UiPrefabLibrary.DefaultResourcePath}. " +
					"Run 'Assembler > UI > Generate UI Prefabs' to create it.");

			var gameEntityFactory = new GameEntityFactory(
				variableRegistry,
				compiledExpressionsRegistry,
				behaviourRegistry,
				assetRegistry,
				stringTableRegistry,
				entityTransformRegistry,
				exclusiveGroupRegistry,
				gameClock,
				templatesById,
				gameInfo.ParseContext,
				gameRoot.transform,
				controls,
				controlsAsset,
				uiPrefabs);

			var initialisations = new InitialisationQueue();

			// 4. Append the implicit game-over controller so it builds through the normal pipeline. A top-level
			// GameOverCondition (if any) drives it; derived here rather than carried on ResolvedGame since it's
			// a pure function of the game info.
			var hasCondition = gameInfo.GameOverCondition is not None<bool>;
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

#if DEBUG_CONSOLE
			// 6. Attach the framework-level debug overlay (stripped entirely in non-DEBUG_CONSOLE builds).
			gameRoot.AddComponent<Assembler.Building.Debug.DebugConsole>()
				.Initialise(behaviourRegistry, gameClock, variableRegistry, gameRoot.GetComponent<GameController>());
#endif

			return gameRoot;
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

	/// <summary>
	/// Opaque handle produced by <see cref="Builder.Resolve"/> and consumed by <see cref="Builder.Instantiate"/>,
	/// carrying the registries and validated state between the two build phases. Treat as a one-shot token: pass
	/// the value straight from Resolve into Instantiate.
	/// </summary>
	public sealed record ResolvedGame(
		GameInfo GameInfo,
		ControlsInfo Controls,
		InputActionAsset ControlsAsset,
		VariableRegistry VariableRegistry,
		CompiledExpressionsRegistry CompiledExpressionsRegistry,
		AssetRegistry AssetRegistry,
		StringTableRegistry StringTableRegistry);
}
