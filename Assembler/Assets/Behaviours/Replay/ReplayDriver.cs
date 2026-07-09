using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.Replay
{
	/// <summary>
	/// Owns the ambient <see cref="InputReplayHub"/> for the game's lifetime and, in replay mode, re-emits each
	/// tick's captured input on the matching clock frame. Added to the game root by <c>Builder.Instantiate</c> only
	/// when a session is active.
	/// </summary>
	/// <remarks>
	/// A <c>DefaultExecutionOrder</c> attribute places this after <c>GameClockDriver</c> (which ticks the clock
	/// at a large negative order) but ahead of gameplay behaviours (default order 0), so a replayed input lands
	/// before anything consumes it that frame — matching the order live input is delivered during capture. Same-tick
	/// ordering between multiple behaviours that resume in one frame is the known #101 carve-out (#241) and is not
	/// addressed here.
	/// </remarks>
	[DefaultExecutionOrder(-9000)]
	public sealed class ReplayDriver : MonoBehaviour
	{
		private InputReplaySession _session = null!;
		private IGameClock _clock = null!;

		private void Update()
		{
			if (_session.Mode is ReplayMode.Replay)
			{
				_session.ReplayFrame(_clock.FrameCount);
			}
		}

		// Clear the ambient session on teardown so a fixed-step / replay run never leaks its session into a
		// subsequent game loaded in the same process. Guarded so a later game's driver isn't clobbered.
		private void OnDestroy()
		{
			if (ReferenceEquals(InputReplayHub.Current, _session))
			{
				InputReplayHub.Current = null;
			}
		}

		/// <summary>Binds the run clock to the session, publishes the session on the ambient hub immediately (so
		/// triggers emitting during the build's initialisation pass already see it), and wires the clock the replay
		/// pump reads. Single point that threads the clock into the session.</summary>
		public void Initialise(InputReplaySession session, IGameClock clock)
		{
			_session = session;
			_clock = clock;
			session.Bind(clock);
			InputReplayHub.Current = session;
		}
	}
}
