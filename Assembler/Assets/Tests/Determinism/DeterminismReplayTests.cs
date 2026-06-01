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
	/// End-to-end Level 1 determinism: a fixed seed + fixed delta + scripted input log reproduces a Snake run
	/// byte-identically on the same build/machine, and a recording replays to the same state (and re-records
	/// identically). See the Determinism (Level 1) section in CLAUDE.md.
	///
	/// Snake 2 uses physics colliders for food-eating and death, which are NOT part of the guarantee, so these
	/// tests assert only on clock-driven, physics-independent state: the head's logical position, its transform
	/// (moved by the clock-stepped `translate`), and the `direction` variable. Runs are kept short so the snake
	/// never hits a wall or itself (which would end the game and tear down the scene mid-test).
	/// </summary>
	public class DeterminismReplayTests
	{
		private const float FixedDt = 1f / 60f;
		private const uint Seed = 12345u;
		private const int Frames = 40;

		private static readonly BehaviourDescriptor UpKey = new("snake head", "up key");
		private static readonly BehaviourDescriptor LeftKey = new("snake head", "left key");
		private static readonly BehaviourDescriptor HeadGizmo = new("snake head", "gizmo");

		private static string SnakePath => Application.dataPath + "/ExampleGameDescriptors/Snake 2.yaml";

		private GameObject? _root;

		[UnityTearDown]
		public IEnumerator TearDown()
		{
			if (_root != null)
			{
				Object.DestroyImmediate(_root);
				_root = null;
			}

			InputBoundary.Reset();
			yield return null;
		}

		[UnityTest]
		public IEnumerator IdenticalConfig_ProducesIdenticalState()
		{
			var firstResult = Build(new BuildOptions(
				RandomSeed: Seed, Clock: ClockMode.FixedStep, FixedDeltaTime: FixedDt));
			yield return RunFrames(Frames);
			var first = Snapshot(firstResult);
			Teardown(firstResult);

			var secondResult = Build(new BuildOptions(
				RandomSeed: Seed, Clock: ClockMode.FixedStep, FixedDeltaTime: FixedDt));
			yield return RunFrames(Frames);
			var second = Snapshot(secondResult);
			Teardown(secondResult);

			AssertStatesEqual(first, second, "two identical-config runs");
		}

		[UnityTest]
		public IEnumerator Replay_ReproducesRecording_AndReRecordsIdentically()
		{
			var script = new Dictionary<int, BehaviourDescriptor[]>
			{
				{ 13, new[] { UpKey } },
				{ 25, new[] { LeftKey } },
			};

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
			var result = Builder.Build(SnakePath, options);
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

			InputBoundary.Reset();
		}

		private static IEnumerator RunFrames(int count)
		{
			for (var i = 0; i < count; i++)
			{
				yield return null;
			}
		}

		private static void AttachScriptedDriver(
			BuildResult result, ReplayRecorder recorder, Dictionary<int, BehaviourDescriptor[]> script)
		{
			result.Root.AddComponent<ScriptedInputDriver>()
				.Initialise(result.Clock, result.BehaviourRegistry, recorder, script);
		}

		private static DeterministicState Snapshot(BuildResult result)
		{
			var headPos = result.VariableRegistry.Get<Vector3>("head pos").Get();
			var direction = result.VariableRegistry.Get<Vector3>("direction").Get();
			var headTransform = result.BehaviourRegistry[HeadGizmo].transform.position;
			return new DeterministicState(headPos, direction, headTransform);
		}

		private static void AssertStatesEqual(DeterministicState a, DeterministicState b, string context)
		{
			AssertVectorsExactlyEqual(a.HeadPos, b.HeadPos, $"{context}: head pos");
			AssertVectorsExactlyEqual(a.Direction, b.Direction, $"{context}: direction");
			AssertVectorsExactlyEqual(a.HeadTransform, b.HeadTransform, $"{context}: head transform");
		}

		private static void AssertVectorsExactlyEqual(Vector3 expected, Vector3 actual, string message)
		{
			Assert.AreEqual(expected.x, actual.x, $"{message} (x)");
			Assert.AreEqual(expected.y, actual.y, $"{message} (y)");
			Assert.AreEqual(expected.z, actual.z, $"{message} (z)");
		}

		private readonly struct DeterministicState
		{
			public DeterministicState(Vector3 headPos, Vector3 direction, Vector3 headTransform)
			{
				HeadPos = headPos;
				Direction = direction;
				HeadTransform = headTransform;
			}

			public Vector3 HeadPos { get; }
			public Vector3 Direction { get; }
			public Vector3 HeadTransform { get; }
		}
	}

	/// <summary>
	/// Test-only input source for the record pass. Runs at the same execution order as <see cref="ReplayDriver"/>
	/// (before gameplay) so a scripted activation lands at the identical point in the tick during record and replay,
	/// keeping the two byte-identical. Records each activation and re-fires the trigger via the public replay seam —
	/// the same data a live <c>FireInput</c> would have produced.
	/// </summary>
	[DefaultExecutionOrder(-9999)]
	public sealed class ScriptedInputDriver : MonoBehaviour
	{
		private IGameClock _clock = null!;
		private BehaviourRegistry _registry = null!;
		private ReplayRecorder _recorder = null!;
		private Dictionary<int, BehaviourDescriptor[]> _script = null!;

		public void Initialise(
			IGameClock clock, BehaviourRegistry registry, ReplayRecorder recorder,
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
				_recorder.Record(descriptor, TriggerContext.Empty);
				if (_registry[descriptor] is IReplayableInputTrigger trigger)
				{
					trigger.ReplayFire(TriggerContext.Empty);
				}
			}
		}
	}
}
