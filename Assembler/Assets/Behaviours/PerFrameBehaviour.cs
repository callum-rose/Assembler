using Assembler.Resolving;
using Assembler.Time;

namespace Assembler.Behaviours
{
	/// <summary>
	/// Base for behaviours that do a unit of work every game frame — integrate a velocity, clamp a position,
	/// steer toward a target, advance an FSM, and so on. A subclass implements only <see cref="Step"/>; the
	/// per-frame invocation is centralised so ordering is deterministic.
	/// <para>
	/// These behaviours are <em>not</em> self-driven off their own <c>Update</c>: Unity does not guarantee an
	/// order between different components' <c>Update</c>, so a shared-velocity stack (acceleration → drag →
	/// speed limit → velocity, all mutating one <c>!var velocity</c>) would run in an undefined order and diverge
	/// on replay (issue #241). Instead the <c>BehaviourRegistry</c> collects every per-frame behaviour in
	/// registration (descriptor) order and a single <c>PerFrameDriver</c> on the game root calls
	/// <see cref="Step"/> on each in that order, once per <em>advanced</em> game frame.
	/// </para>
	/// The driver gates on <see cref="IGameClock.FrameCount"/> (not <see cref="IGameClock.IsPaused"/>): the frame
	/// count freezes while paused and ticks exactly once for a queued frame-by-frame debug step, so
	/// <see cref="Step"/> runs once per advanced frame — skipped while paused, but still advanced by a debug step.
	/// Every per-frame behaviour is thus clock-aware (the build pipeline injects the shared clock via
	/// <see cref="INeedsGameClock"/>), even the frame-rate-independent ones (clamp/wrap/speed-limit) that never
	/// read <see cref="IGameClock.DeltaTime"/> but still shouldn't run while the game is paused.
	/// </summary>
	/// <typeparam name="TData">The behaviour's resolved data type.</typeparam>
	public abstract class PerFrameBehaviour<TData> : GameBehaviour<TData>, INeedsGameClock, IPerFrameStepped
		where TData : BehaviourData
	{
		public IGameClock Clock { get; set; } = null!;

		// Explicit implementation forwards the driver's public step call to the internal per-frame work, keeping
		// Step() out of subclasses' public surface (they only override the internal method).
		void IPerFrameStepped.Step() => Step();

		/// <summary>Perform this behaviour's per-frame work. Runs once per advanced game frame.</summary>
		internal abstract void Step();
	}
}
