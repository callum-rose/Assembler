using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.Replay
{
	/// <summary>
	/// The per-run input capture/replay log and the completion of the determinism story (issue #101): together with
	/// the fixed-step clock and seeded PRNG carried in <see cref="RunOptions"/>, a captured
	/// <see cref="InputReplaySession"/> replayed on a fresh build of the same descriptor reproduces the run exactly.
	/// </summary>
	/// <remarks>
	/// A session is threaded into <c>Builder.Instantiate</c> alongside <see cref="RunOptions"/> and published on the
	/// ambient <see cref="InputReplayHub"/> for the game's lifetime. In <see cref="ReplayMode.Record"/> the input
	/// triggers append every emission (tagged with the current <see cref="IGameClock.FrameCount"/>); in
	/// <see cref="ReplayMode.Replay"/> they suppress live device reads and the run's <see cref="ReplayDriver"/> feeds
	/// the recorded emissions back on their original frames, resolving each trigger through a lookup the builder binds
	/// over the live <c>BehaviourRegistry</c> (so runtime-spawned triggers are found too). The log is kept in memory —
	/// <see cref="RecordedInput"/> values a caller can inspect, assert on, or (de)serialise itself.
	/// </remarks>
	public sealed class InputReplaySession
	{
		private readonly List<RecordedInput> _log;
		private IGameClock? _clock;
		private Func<BehaviourDescriptor, IReplayableInput?>? _triggerLookup;

		// Replay reads the log in ascending-frame order; this cursor advances monotonically with the clock so
		// each frame's replay is O(emissions that frame) rather than a full-log scan. Reset by Bind so one captured
		// log can be replayed against many builds.
		private int _cursor;

		private InputReplaySession(ReplayMode mode, List<RecordedInput> log)
		{
			Mode = mode;
			_log = log;
		}

		/// <summary>Whether this session is capturing or replaying.</summary>
		public ReplayMode Mode { get; }

		/// <summary>True while this session is replaying (input triggers suppress their live emissions).</summary>
		public bool IsReplaying => Mode is ReplayMode.Replay;

		/// <summary>The captured emissions in fire order. Read after a record run to replay it (<see cref="Replay"/>) or persist it.</summary>
		public IReadOnlyList<RecordedInput> Log => _log;

		/// <summary>Start a fresh capture. The live game drives input; every input-trigger emission is appended.</summary>
		public static InputReplaySession Record() => new(ReplayMode.Record, new List<RecordedInput>());

		/// <summary>Replay a previously captured <paramref name="log"/>. Live device reads are suppressed; the log drives the game.</summary>
		public static InputReplaySession Replay(IEnumerable<RecordedInput> log) =>
			new(ReplayMode.Replay, log.ToList());

		/// <summary>
		/// Bind the run clock so record can tag emissions with the current frame and replay can advance with it, and
		/// reset the replay cursor so this session can drive a fresh run. Called once per run by the builder before
		/// any behaviour runs.
		/// </summary>
		public void Bind(IGameClock clock)
		{
			_clock = clock;
			_cursor = 0;
		}

		/// <summary>
		/// Bind the descriptor → live-trigger lookup replay routes recorded emissions through. The builder passes a
		/// lookup over the runtime <c>BehaviourRegistry</c>, which stays current as entities spawn and despawn — so
		/// there is no separate registration step to keep in sync (and destroyed triggers are simply not found).
		/// </summary>
		public void BindTriggerLookup(Func<BehaviourDescriptor, IReplayableInput?> lookup) => _triggerLookup = lookup;

		/// <summary>Append an emission to the log during a record run. No-op unless recording (so a normal run pays nothing).</summary>
		internal void Record(BehaviourDescriptor trigger, TriggerContext context)
		{
			if (Mode is not ReplayMode.Record || _clock is null)
			{
				return;
			}

			_log.Add(new RecordedInput(_clock.FrameCount, trigger, context));
		}

		/// <summary>
		/// Re-emit every recorded emission due on or before <paramref name="frame"/>, in capture order, into its
		/// originating trigger. Driven once per frame by <see cref="ReplayDriver"/> in replay mode. The
		/// on-or-before guard keeps the cursor from stalling if a frame is ever skipped; for the contiguous
		/// fixed-step frame sequence this is exactly "emissions for this frame".
		/// </summary>
		internal void ReplayFrame(int frame)
		{
			while (_cursor < _log.Count && _log[_cursor].Frame <= frame)
			{
				var recorded = _log[_cursor];
				_cursor++;

				if (_triggerLookup?.Invoke(recorded.Trigger) is { } trigger)
				{
					trigger.ReplayEmit(recorded.Context);
				}
				else
				{
					// A recorded trigger with no live match means the run diverged from capture (the trigger's entity
					// isn't present this frame). Surfacing it turns a silent input-drop into a debuggable signal.
					UnityEngine.Debug.LogWarning(
						$"[Assembler] Replay: no live input trigger for {recorded.Trigger} at frame {recorded.Frame} — " +
						"the replay has diverged from the recorded run (dropped this emission).");
				}
			}
		}
	}
}
