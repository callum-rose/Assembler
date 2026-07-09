namespace Assembler.Time
{
	/// <summary>
	/// The driver-facing view of a clock that can be advanced. Kept separate from <see cref="IGameClock"/>
	/// (which every gameplay consumer sees) so that <see cref="Tick"/> stays off the consumer interface and
	/// test fakes need only implement <see cref="IGameClock"/>. Only <see cref="GameClockDriver"/> and the
	/// clock selection in <c>Builder</c> deal in this interface.
	/// </summary>
	public interface IAdvancingGameClock : IGameClock
	{
		/// <summary>
		/// The value the per-frame driver writes to <see cref="UnityEngine.Time.captureDeltaTime"/> while this
		/// clock is active, or <c>0</c> to leave Unity on wall-clock. A fixed-step clock returns its constant
		/// step here so Unity's frame cadence and physics accumulator advance by that exact amount each frame,
		/// making the whole engine loop deterministic (not just this clock's own <see cref="IGameClock.DeltaTime"/>).
		/// </summary>
		float CaptureDeltaTime { get; }

		/// <summary>
		/// Advances the clock by one frame. Call once per frame, before any reader runs. Not on
		/// <see cref="IGameClock"/> so consumers can't advance time and fakes need not implement it.
		/// </summary>
		void Tick();
	}
}
