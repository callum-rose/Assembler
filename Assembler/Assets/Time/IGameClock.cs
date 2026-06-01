namespace Assembler.Time
{
	/// <summary>
	/// The single source of game time. Injected everywhere timing matters instead of reading
	/// <see cref="UnityEngine.Time"/> directly, so gameplay can be paused, slowed, sped up, and
	/// (eventually) replayed deterministically without affecting UI/menus or the editor.
	/// </summary>
	/// <remarks>
	/// All readers see a value snapshotted once per frame (see <c>RealtimeGameClock.Tick</c>), so a
	/// pause or timescale change applied mid-frame only takes effect on the next frame and every
	/// reader within a frame observes the same delta. Concrete advancement (<c>Tick</c>) is not on
	/// this interface so test fakes need not implement it.
	/// </remarks>
	public interface IGameClock
	{
		/// <summary>Scaled game delta for this frame: <c>IsPaused ? 0 : (wall-clock delta) * TimeScale</c>.</summary>
		float DeltaTime { get; }

		/// <summary>Unscaled wall-clock delta for this frame. For UI/menus that keep animating while paused.</summary>
		float UnscaledDeltaTime { get; }

		/// <summary>Accumulated scaled game time since the clock started.</summary>
		double Time { get; }

		/// <summary>Number of game frames advanced (does not increment while paused).</summary>
		int FrameCount { get; }

		/// <summary>Playback rate: 1 normal, 0.5 slow-mo, 0 paused. Clamped to be non-negative.</summary>
		float TimeScale { get; set; }

		/// <summary>True while the clock is paused (delta is 0 and the frame count is frozen).</summary>
		bool IsPaused { get; }

		/// <summary>Freezes game time: delta becomes 0 and the frame count stops advancing.</summary>
		void Pause();

		/// <summary>Resumes game time at the current <see cref="TimeScale"/>.</summary>
		void Resume();

		/// <summary>
		/// Queues <paramref name="frames"/> single-frame advances while paused. Each queued frame lets
		/// the clock tick exactly once (advancing <see cref="Time"/> and <see cref="FrameCount"/>) before
		/// freezing again, for frame-by-frame debugging. No effect when not paused; non-positive counts
		/// are ignored.
		/// </summary>
		void Step(int frames = 1);
	}
}
