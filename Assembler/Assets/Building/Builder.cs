using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.Triggers.Input;
using Assembler.Building.Replay;
using Assembler.Compiler.Compiler;
using Assembler.Deserialisation;
using Assembler.Input;
using Assembler.Libraries;
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
		public static BuildResult Build(string yamlPath, InputPlatform? overridePlatform = null) =>
			Build(yamlPath, new BuildOptions(OverridePlatform: overridePlatform));

		/// <summary>
		/// Builds from a YAML descriptor on disk. This is the only entry that supports record/replay, since it can
		/// compute the descriptor hash from the raw YAML text (see Determinism (Level 1) in CLAUDE.md).
		/// </summary>
		public static BuildResult Build(string yamlPath, BuildOptions options)
		{
			var yaml = File.ReadAllText(yamlPath);
			var descriptorHash = DescriptorHash.Compute(yaml);
			var gameDto = new GameFileParser().Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			var controls = ControlsTransformer.Transform(gameDto.Controls);
			return Build(gameInfo, controls, options, descriptorHash);
		}

		public static BuildResult Build(GameInfo gameInfo) => Build(gameInfo, ControlsInfo.Empty, BuildOptions.Default);

		public static BuildResult Build(GameInfo gameInfo, ControlsInfo controls, InputPlatform? overridePlatform) =>
			Build(gameInfo, controls, new BuildOptions(OverridePlatform: overridePlatform));

		public static BuildResult Build(GameInfo gameInfo, ControlsInfo controls, BuildOptions options) =>
			Build(gameInfo, controls, options, descriptorHash: null);

		private static BuildResult Build(GameInfo gameInfo, ControlsInfo controls, BuildOptions options, string? descriptorHash)
		{
			// Clear any record/replay state from a prior build so nothing leaks between runs.
			InputBoundary.Reset();

			// Record/replay needs the descriptor hash, which only the YAML-path entry can compute.
			if (options.Replay != ReplayMode.Off && descriptorHash == null)
			{
				throw new InvalidOperationException(
					"Record/replay is only supported via the YAML-path build entry (it needs the descriptor hash).");
			}

			// Replay forces the deterministic config captured in the recording: fixed clock, recorded delta and seed.
			if (options.Replay == ReplayMode.Replay)
			{
				var recorded = (options.Player ?? throw new InvalidOperationException(
					"Replay mode requires a Player built from a recorded session.")).Replay;

				options = options with
				{
					Clock = ClockMode.FixedStep,
					FixedDeltaTime = recorded.FixedDeltaTime,
					RandomSeed = recorded.Seed
				};
			}

			// Seed the per-run PRNG before any runtime randomness is drawn (first draws happen in
			// initialisations.ExecuteAll). Defaulting to TickCount preserves unseeded variety while letting a
			// caller pin the seed for deterministic replay. See Determinism (Level 1) in CLAUDE.md.
			var seed = options.RandomSeed ?? (uint)Environment.TickCount;
			RandomState.Seed(seed);

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
			var platform = options.OverridePlatform ?? PlatformSelector.Resolve();
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
			// (ahead of every behaviour Update via DefaultExecutionOrder). FixedStep makes the per-tick
			// delta constant for deterministic replay; Realtime is the default (see Determinism in CLAUDE.md).
			ITickableClock gameClock = options.Clock == ClockMode.FixedStep
				? new FixedStepGameClock(options.FixedDeltaTime)
				: new RealtimeGameClock();

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

			// 5b. Wire record/replay at the input boundary now the behaviour graph is fully registered.
			switch (options.Replay)
			{
				case ReplayMode.Record:
				{
					var recorder = options.Recorder ?? throw new InvalidOperationException(
						"Record mode requires a Recorder to capture into.");
					recorder.Initialise(gameClock, descriptorHash!, seed, options.FixedDeltaTime, platform);
					InputBoundary.Sink = recorder;
					break;
				}
				case ReplayMode.Replay:
				{
					var player = options.Player!; // Non-null: validated above.
					if (player.Replay.DescriptorHash != descriptorHash)
					{
						throw new InvalidOperationException(
							"Replay descriptor hash does not match the built descriptor — the descriptor has changed since recording.");
					}

					player.Initialise(gameClock, behaviourRegistry);
					InputBoundary.BeginReplay(player);
					gameRoot.AddComponent<ReplayDriver>().Player = player;
					break;
				}
			}

#if DEBUG_CONSOLE
			// 6. Attach the framework-level debug overlay (stripped entirely in non-DEBUG_CONSOLE builds).
			gameRoot.AddComponent<Assembler.Building.Debug.DebugConsole>()
				.Initialise(behaviourRegistry, gameClock, variableRegistry, gameRoot.GetComponent<GameController>());
#endif

			return new BuildResult(gameRoot, behaviourRegistry, variableRegistry, gameClock, seed);
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
