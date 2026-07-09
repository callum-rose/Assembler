using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.Replay;
using Assembler.Building;
using Assembler.Deserialisation;
using Assembler.Input;
using Assembler.Parsing;
using Assembler.Parsing.Controls;
using Assembler.Time;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace Tests.Determinism
{
	/// <summary>
	/// The end-to-end determinism regression for issue #101: record an input log on a real descriptor built with
	/// the fixed-step clock + a fixed seed, replay that log on a fresh build of the same descriptor, and assert the
	/// final game state matches — the proof that the Phase 1 clock and Phase 2 seed compose into a reproducible
	/// replay through the Phase 3 input record/replay boundary.
	/// </summary>
	/// <remarks>
	/// PlayMode, because behaviour <c>Update</c> and input only run in play mode. The probe descriptor is
	/// deliberately physics-free (Level 1 excludes physics, and two in-process runs would otherwise share a
	/// <c>PhysicsScene</c>). Input is injected through the <c>auto</c> controls scheme (see the descriptor) driven by
	/// a synthetic <see cref="Gamepad"/>, so the capture exercises the real device → action → trigger → record path.
	/// </remarks>
	public sealed class ReplayDeterminismTests
	{
		// Enough frames to visit several directions; each yield is one FixedStep tick (one clock frame).
		private const int Frames = 40;
		private const uint SeedA = 12345u;
		private const uint SeedB = 987u;

		// The probe descriptor is embedded rather than loaded from disk so the test runs unchanged in a built
		// player (where Application.dataPath is not the Assets folder) — the reliable headless path is a player
		// test build (Tools/run-tests.sh --player), since in-editor batch PlayMode hangs on this setup. Physics-free
		// and UI-free; the mover is moved only inside the input trigger's own notify chain (no intra-tick race).
		private const string ProbeYaml = @"
Game:
  Title: Replay Probe
  Description: Deterministic record/replay probe (issue 101) - not a playable game.

World:
  Dimensionality: 2

Variables:
  is dead: false

Controls:
  Actions:
    move: { Type: value, ValueType: vector2 }
  Bindings:
    auto:
      move: [ ""<Gamepad>/leftStick"" ]

Entities:
  mover:
    Position: !expr
      Do: 'new UnityEngine.Vector3(RandomFloat(-3f, 3f), RandomFloat(-3f, 3f), 0f)'
      RegisterTypes: [ UnityEngine.Vector3 ]
    Behaviours:
      move input:
        Type: input action
        Properties: { Action: move }
        Listeners: [ nudge ]
      nudge:
        Type: translate
        Properties:
          Displacement: !output axis

  referee:
    Position: !vec { X: 0, Y: 0 }
    Behaviours:
      tick:
        Type: every frame trigger
        Listeners: [ gate ]
      gate:
        Type: condition gate
        Properties:
          Condition: !var is dead
        Listeners:
          - !gameover
";

		// A second probe that puts the shared-velocity stack (issue 241) under replay. The mover carries one
		// per-entity `velocity` variable that four behaviours compose onto every frame, declared in the order the
		// contract requires: acceleration -> drag -> speed limit -> velocity (integrator). Before #241 each ran off
		// its own Update in Unity's undefined component order, so this stack was excluded from the replay guarantee;
		// the central PerFrameDriver now steps them in registration order, so the run reproduces on replay. Input
		// also nudges the mover (via translate) so the replayed input path is exercised alongside the always-on stack.
		private const string StackProbeYaml = @"
Game:
  Title: Velocity Stack Probe
  Description: Deterministic shared-velocity-stack ordering probe (issue 241) - not a playable game.

World:
  Dimensionality: 2

Variables:
  is dead: false

Controls:
  Actions:
    move: { Type: value, ValueType: vector2 }
  Bindings:
    auto:
      move: [ ""<Gamepad>/leftStick"" ]

Entities:
  mover:
    Position: !expr
      Do: 'new UnityEngine.Vector3(RandomFloat(-3f, 3f), RandomFloat(-3f, 3f), 0f)'
      RegisterTypes: [ UnityEngine.Vector3 ]
    Variables:
      velocity: !vec { X: 0, Y: 0, Z: 0 }
    Behaviours:
      move input:
        Type: input action
        Properties: { Action: move }
        Listeners: [ nudge ]
      nudge:
        Type: translate
        Properties:
          Displacement: !output axis
      accel:
        Type: acceleration
        Properties:
          Acceleration: !vec { X: 2, Y: 1, Z: 0 }
          Velocity: !var velocity
      drag:
        Type: drag
        Properties:
          Velocity: !var velocity
          Coefficient: 0.5
      speed cap:
        Type: speed limit
        Properties:
          Velocity: !var velocity
          Max: 4
      integrate:
        Type: velocity
        Properties:
          Velocity: !var velocity

  referee:
    Position: !vec { X: 0, Y: 0 }
    Behaviours:
      tick:
        Type: every frame trigger
        Listeners: [ gate ]
      gate:
        Type: condition gate
        Properties:
          Condition: !var is dead
        Listeners:
          - !gameover
";

		[UnityTest]
		public IEnumerator Replaying_a_recorded_log_reproduces_final_state()
		{
			var schedule = BuildInputSchedule();

			// 1. Record: live (synthetic) input drives the game while every emission is captured.
			var recorder = InputReplaySession.Record();
			var recorded = new Vector3[1];
			yield return RunProbe(ProbeYaml, SeedA, recorder, driveInput: true, schedule, p => recorded[0] = p);

			Assert.That(recorder.Log, Is.Not.Empty, "Record run captured no input — the auto scheme did not drive the action.");

			// 2. Replay: same descriptor, same seed, no device — the captured log alone drives the game.
			var player = InputReplaySession.Replay(recorder.Log);
			var replayed = new Vector3[1];
			yield return RunProbe(ProbeYaml, SeedA, player, driveInput: false, schedule, p => replayed[0] = p);

			// 3. Same build, same machine, same seed + input log ⇒ identical final state (Level 1).
			Assert.That(
				Vector3.Distance(replayed[0], recorded[0]), Is.LessThan(1e-4f),
				$"Replay diverged from the recorded run: recorded {recorded[0]:F5}, replayed {replayed[0]:F5}.");
		}

		[UnityTest]
		public IEnumerator A_different_seed_changes_the_outcome()
		{
			var schedule = BuildInputSchedule();

			var recorder = InputReplaySession.Record();
			var recorded = new Vector3[1];
			yield return RunProbe(ProbeYaml, SeedA, recorder, driveInput: true, schedule, p => recorded[0] = p);

			// Replay the exact same input log but under a different seed: the seeded-random start must move the
			// outcome, proving the seed genuinely binds the run (and isn't silently ignored).
			var player = InputReplaySession.Replay(recorder.Log);
			var otherSeed = new Vector3[1];
			yield return RunProbe(ProbeYaml, SeedB, player, driveInput: false, schedule, p => otherSeed[0] = p);

			Assert.That(
				Vector3.Distance(otherSeed[0], recorded[0]), Is.GreaterThan(1e-3f),
				"A different seed produced the same final state — the run seed is not binding.");
		}

		[UnityTest]
		public IEnumerator Shared_velocity_stack_replays_deterministically()
		{
			var schedule = BuildInputSchedule();

			// Record a run whose mover is driven by both the replayed input and the always-on shared-velocity stack.
			var recorder = InputReplaySession.Record();
			var recorded = new Vector3[1];
			var start = new Vector3[1];
			yield return RunProbe(StackProbeYaml, SeedA, recorder, driveInput: true, schedule, p => recorded[0] = p,
				onStartPosition: p => start[0] = p);

			Assert.That(recorder.Log, Is.Not.Empty, "Record run captured no input — the auto scheme did not drive the action.");

			// The stack must actually have moved the mover (otherwise a match would prove nothing) — the net
			// displacement from its random start is well beyond what the input nudges alone contribute.
			Assert.That(
				Vector3.Distance(recorded[0], start[0]), Is.GreaterThan(0.5f),
				"The velocity stack did not move the mover, so the replay match would be vacuous.");

			// Replay the captured log on a fresh build: the whole movement stack (input + acceleration -> drag ->
			// speed limit -> velocity) must reproduce the same final position now that it steps in a fixed order.
			var player = InputReplaySession.Replay(recorder.Log);
			var replayed = new Vector3[1];
			yield return RunProbe(StackProbeYaml, SeedA, player, driveInput: false, schedule, p => replayed[0] = p);

			Assert.That(
				Vector3.Distance(replayed[0], recorded[0]), Is.LessThan(1e-4f),
				$"Velocity-stack replay diverged: recorded {recorded[0]:F5}, replayed {replayed[0]:F5}.");
		}

		// Builds one probe game, runs it for Frames ticks under the fixed-step clock, reports the mover's final
		// world position, then tears it down (which clears the ambient replay hub via ReplayDriver.OnDestroy).
		private IEnumerator RunProbe(string yaml, uint seed, InputReplaySession session, bool driveInput,
			IReadOnlyList<Vector2> schedule, System.Action<Vector3> onFinalPosition,
			System.Action<Vector3> onStartPosition = null)
		{
			var gameDto = new GameFileParser().Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			var controls = ControlsTransformer.Transform(gameDto.Controls);

			// Force the automation scheme so the descriptor's `auto` bindings mask in (override-only, like the
			// editor simulating a platform).
			var resolveTask = gameInfo.ResolveAsync(controls, InputPlatform.Auto);
			yield return new WaitUntil(() => resolveTask.IsCompleted);
			if (resolveTask.IsFaulted)
			{
				throw resolveTask.Exception!;
			}

			// Add the synthetic device the `auto` scheme binds to before building, so the action resolves onto it.
			var pad = driveInput ? InputSystem.AddDevice<Gamepad>() : null;

			var root = resolveTask.Result.Instantiate(new RunOptions(GameClockMode.FixedStep, seed), session);

			// The mover's start position (its seeded-random Position expression) is applied during Instantiate, so
			// capture it now — before any tick — for tests that assert the stack produced a real displacement.
			if (onStartPosition != null)
			{
				var moverAtStart = root.GetComponentsInChildren<GameEntity>(true).First(e => e.Id == "mover");
				onStartPosition(moverAtStart.transform.position);
			}

			for (var f = 0; f < Frames; f++)
			{
				if (pad != null)
				{
					// Change the stick state synchronously so the trigger's Update reads it this frame and records it.
					InputState.Change(pad.leftStick, schedule[f]);
				}

				yield return null;
			}

			var mover = root.GetComponentsInChildren<GameEntity>(true).First(e => e.Id == "mover");
			onFinalPosition(mover.transform.position);

			Object.Destroy(root);
			if (pad != null)
			{
				InputSystem.RemoveDevice(pad);
			}

			// Let OnDestroy run so the ambient InputReplayHub is cleared before the next build.
			yield return null;
		}

		// A varied per-frame stick schedule: right, up, back-left, then neutral — moves the mover and changes
		// direction so the capture spans several distinct emissions, with a non-zero net displacement.
		private static IReadOnlyList<Vector2> BuildInputSchedule()
		{
			var schedule = new Vector2[Frames];
			for (var f = 0; f < Frames; f++)
			{
				schedule[f] = (f / 10) switch
				{
					0 => Vector2.right,
					1 => Vector2.up,
					2 => Vector2.left,
					_ => Vector2.zero
				};
			}

			return schedule;
		}
	}
}
