using System.Collections;
using System.Collections.Generic;
using Assembler.Behaviours.Triggers.Input;
using Assembler.Building;
using Assembler.Building.Replay;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using Assembler.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Determinism
{
	/// <summary>
	/// End-to-end Level 1 determinism: a fixed seed + fixed delta + scripted input log reproduces an Asteroids run
	/// byte-identically on the same build/machine, and a recording replays to the same state (and re-records
	/// identically). See the Determinism (Level 1) section in CLAUDE.md.
	///
	/// Asteroids uses physics colliders for bullet/asteroid/ship collisions, which are NOT part of the guarantee,
	/// so these tests assert only on the ship's clock-and-input-driven state: its <c>ship facing</c> (rotated by
	/// A/D input), <c>ship velocity</c> (accumulated by W thrust), and its transform (moved each tick by the
	/// clock-stepped <c>velocity</c> behaviour). Runs are kept under the 1.2 s asteroid spawn interval (72 frames),
	/// so no asteroid ever spawns and no physics interaction can perturb the asserted state.
	/// </summary>
	public class DeterminismReplayTests
	{
		private const float FixedDt = 1f / 60f;
		private const uint Seed = 12345u;
		private const int Frames = 40;

		private static readonly BehaviourDescriptor RotateLeft = new("ship", "rotate left key");
		private static readonly BehaviourDescriptor RotateRight = new("ship", "rotate right key");
		private static readonly BehaviourDescriptor Thrust = new("ship", "thrust key");
		private static readonly BehaviourDescriptor ShipGizmo = new("ship", "gizmo");

		private static string AsteroidsPath => Application.dataPath + "/ExampleGameDescriptors/Asteroids.yaml";

		// A short input log exercising both input paths: A/D rotate the facing, W accumulates velocity along it.
		// All firings land before frame 72, so the asteroid spawner never runs and the ship state stays physics-free.
		private static Dictionary<int, BehaviourDescriptor[]> InputScript() => new()
		{
			{ 4, new[] { RotateLeft } },
			{ 5, new[] { RotateLeft } },
			{ 6, new[] { RotateLeft } },
			{ 10, new[] { Thrust } },
			{ 11, new[] { Thrust } },
			{ 12, new[] { Thrust } },
			{ 13, new[] { Thrust } },
			{ 20, new[] { RotateRight } },
			{ 21, new[] { RotateRight } },
			{ 25, new[] { Thrust } },
			{ 26, new[] { Thrust } },
		};

		private GameObject? _root;

		[UnityTearDown]
		public IEnumerator TearDown()
		{
			if (_root != null)
			{
				Object.DestroyImmediate(_root);
				_root = null;
			}

			yield return null;
		}

		[UnityTest]
		public IEnumerator IdenticalConfig_ProducesIdenticalState()
		{
			var firstResult = Build(new BuildOptions(
				RandomSeed: Seed, Clock: ClockMode.FixedStep, FixedDeltaTime: FixedDt));
			AttachScriptedDriver(firstResult, recorder: null, InputScript());
			yield return RunFrames(Frames);
			var first = Snapshot(firstResult);
			Teardown(firstResult);

			var secondResult = Build(new BuildOptions(
				RandomSeed: Seed, Clock: ClockMode.FixedStep, FixedDeltaTime: FixedDt));
			AttachScriptedDriver(secondResult, recorder: null, InputScript());
			yield return RunFrames(Frames);
			var second = Snapshot(secondResult);
			Teardown(secondResult);

			AssertStatesEqual(first, second, "two identical-config runs");
		}

		[UnityTest]
		public IEnumerator Replay_ReproducesRecording_AndReRecordsIdentically()
		{
			var script = InputScript();

			// --- Record ---
			var recorder = new ReplayRecorder();
			var recordResult = Build(new BuildOptions(
				RandomSeed: Seed, Clock: ClockMode.FixedStep, FixedDeltaTime: FixedDt,
				Replay: ReplayMode.Record, Recorder: recorder));
			AttachScriptedDriver(recordResult, recorder, script);
			yield return RunFrames(Frames);
			var replay1 = recorder.Build();
			var recordedState = Snapshot(recordResult);
			Teardown(recordResult);

			// --- Replay ---
			var replayResult = Build(new BuildOptions(
				Replay: ReplayMode.Replay, Player: new ReplayPlayer(replay1)));
			yield return RunFrames(Frames);
			var replayedState = Snapshot(replayResult);
			Teardown(replayResult);

			AssertStatesEqual(recordedState, replayedState, "replay vs original recording");

			// --- Re-record (pure same-machine determinism) ---
			var recorder2 = new ReplayRecorder();
			var reRecordResult = Build(new BuildOptions(
				RandomSeed: Seed, Clock: ClockMode.FixedStep, FixedDeltaTime: FixedDt,
				Replay: ReplayMode.Record, Recorder: recorder2));
			AttachScriptedDriver(reRecordResult, recorder2, script);
			yield return RunFrames(Frames);
			var replay2 = recorder2.Build();
			var reRecordedState = Snapshot(reRecordResult);
			Teardown(reRecordResult);

			AssertStatesEqual(recordedState, reRecordedState, "re-record vs original recording");
			Assert.AreEqual(
				ReplaySerializer.Serialize(replay1),
				ReplaySerializer.Serialize(replay2),
				"two recordings of the same seed + input log must serialize identically");
		}

		// --- Helpers --------------------------------------------------------

		private BuildResult Build(BuildOptions options)
		{
			var result = Builder.Build(AsteroidsPath, options);
			_root = result.Root;
			return result;
		}

		private void Teardown(BuildResult result)
		{
			if (result.Root != null)
			{
				Object.DestroyImmediate(result.Root);
			}

			if (ReferenceEquals(_root, result.Root))
			{
				_root = null;
			}
		}

		private static IEnumerator RunFrames(int count)
		{
			for (var i = 0; i < count; i++)
			{
				yield return null;
			}
		}

		// Drives the scripted input log. A null recorder just fires (for the plain identical-config run); a non-null
		// one also captures each activation, so a recorded session can later be replayed.
		private static void AttachScriptedDriver(
			BuildResult result, ReplayRecorder? recorder, Dictionary<int, BehaviourDescriptor[]> script)
		{
			result.Root.AddComponent<ScriptedInputDriver>()
				.Initialise(result.Clock, result.BehaviourRegistry, recorder, script);
		}

		private static DeterministicState Snapshot(BuildResult result)
		{
			var facing = result.VariableRegistry.Get<Vector3>("ship facing").Get();
			var velocity = result.VariableRegistry.Get<Vector3>("ship velocity").Get();
			var shipTransform = result.BehaviourRegistry[ShipGizmo].transform.position;
			return new DeterministicState(facing, velocity, shipTransform);
		}

		private static void AssertStatesEqual(DeterministicState a, DeterministicState b, string context)
		{
			AssertVectorsExactlyEqual(a.Facing, b.Facing, $"{context}: ship facing");
			AssertVectorsExactlyEqual(a.Velocity, b.Velocity, $"{context}: ship velocity");
			AssertVectorsExactlyEqual(a.ShipTransform, b.ShipTransform, $"{context}: ship transform");
		}

		private static void AssertVectorsExactlyEqual(Vector3 expected, Vector3 actual, string message)
		{
			Assert.AreEqual(expected.x, actual.x, $"{message} (x)");
			Assert.AreEqual(expected.y, actual.y, $"{message} (y)");
			Assert.AreEqual(expected.z, actual.z, $"{message} (z)");
		}

		private readonly struct DeterministicState
		{
			public DeterministicState(Vector3 facing, Vector3 velocity, Vector3 shipTransform)
			{
				Facing = facing;
				Velocity = velocity;
				ShipTransform = shipTransform;
			}

			public Vector3 Facing { get; }
			public Vector3 Velocity { get; }
			public Vector3 ShipTransform { get; }
		}
	}

	/// <summary>
	/// Test-only input source for the record pass. Runs at the same execution order as <see cref="ReplayDriver"/>
	/// (before gameplay) so a scripted activation lands at the identical point in the tick during record and replay,
	/// keeping the two byte-identical. Re-fires each trigger via the public replay seam, and (when a recorder is
	/// supplied) records the same activation a live firing would have produced.
	/// </summary>
	[DefaultExecutionOrder(-9999)]
	public sealed class ScriptedInputDriver : MonoBehaviour
	{
		private IGameClock _clock = null!;
		private BehaviourRegistry _registry = null!;
		private ReplayRecorder? _recorder;
		private Dictionary<int, BehaviourDescriptor[]> _script = null!;

		public void Initialise(
			IGameClock clock, BehaviourRegistry registry, ReplayRecorder? recorder,
			Dictionary<int, BehaviourDescriptor[]> script)
		{
			_clock = clock;
			_registry = registry;
			_recorder = recorder;
			_script = script;
		}

		private void Update()
		{
			if (!_script.TryGetValue(_clock.FrameCount, out var descriptors))
			{
				return;
			}

			foreach (var descriptor in descriptors)
			{
				_recorder?.Record(descriptor, TriggerContext.Empty);
				if (_registry[descriptor] is IReplayableInputTrigger trigger)
				{
					trigger.ReplayFire(TriggerContext.Empty);
				}
			}
		}
	}
}
