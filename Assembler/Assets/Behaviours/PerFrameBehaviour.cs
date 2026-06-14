using Assembler.Resolving;
using Assembler.Time;

namespace Assembler.Behaviours
{
	/// <summary>
	/// Base for self-driven behaviours that do a unit of work every game frame — integrate a velocity, clamp a
	/// position, steer toward a target, advance an FSM, and so on. Centralises the two things every such
	/// behaviour used to hand-roll:
	/// <list type="bullet">
	///   <item>the <c>Update() =&gt; Step()</c> wiring, so a subclass implements only <see cref="Step"/>; and</item>
	///   <item>a clock-gated early-out so per-frame work stops while the game is paused.</item>
	/// </list>
	/// The early-out keys off <see cref="IGameClock.FrameCount"/> rather than <see cref="IGameClock.IsPaused"/>:
	/// the frame count freezes while paused and ticks exactly once for a queued frame-by-frame debug step, so
	/// <see cref="Step"/> runs once per <em>advanced</em> game frame — skipped while paused, but still advanced
	/// by a debug step. Every per-frame behaviour is thus clock-aware (the build pipeline injects the shared
	/// clock via <see cref="INeedsGameClock"/>), even the frame-rate-independent ones (clamp/wrap/speed-limit)
	/// that never read <see cref="IGameClock.DeltaTime"/> but still shouldn't run while the game is paused.
	/// </summary>
	/// <typeparam name="TData">The behaviour's resolved data type.</typeparam>
	public abstract class PerFrameBehaviour<TData> : GameBehaviour<TData>, INeedsGameClock
		where TData : BehaviourData
	{
		public IGameClock Clock { get; set; } = null!;

		private int _lastSteppedFrame = -1;

		private void Update()
		{
			// FrameCount only advances on a game frame (it's frozen while paused, +1 for a debug step), so this
			// runs Step once per advanced frame and never while paused — without freezing a debug step's motion.
			if (Clock.FrameCount == _lastSteppedFrame)
			{
				return;
			}

			_lastSteppedFrame = Clock.FrameCount;
			Step();
		}

		/// <summary>Perform this behaviour's per-frame work. Runs once per advanced game frame.</summary>
		internal abstract void Step();
	}
}
