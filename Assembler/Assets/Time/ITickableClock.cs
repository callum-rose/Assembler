namespace Assembler.Time
{
	/// <summary>
	/// An <see cref="IGameClock"/> that the per-frame driver advances. <see cref="Tick"/> is kept off
	/// <see cref="IGameClock"/> so test fakes need not implement it; the real clocks (realtime and fixed-step)
	/// implement this so <see cref="GameClockDriver"/> can drive either one.
	/// </summary>
	public interface ITickableClock : IGameClock
	{
		/// <summary>Advances the clock by one frame. Call once per frame, before any reader runs.</summary>
		void Tick();
	}
}
