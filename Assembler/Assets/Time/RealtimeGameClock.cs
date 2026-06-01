namespace Assembler.Time
{
	/// <summary>
	/// The default <see cref="IGameClock"/>: drives game time from wall-clock
	/// <see cref="UnityEngine.Time.deltaTime"/>, scaled by <see cref="TimeScale"/> and gated by pause.
	/// </summary>
	/// <remarks>
	/// <see cref="Tick"/> snapshots the per-frame deltas once, so a whole frame is atomic: changing
	/// <see cref="TimeScale"/> or pausing mid-frame takes effect next frame and every reader within a
	/// frame sees the same value. A per-frame driver (<see cref="GameClockDriver"/>) is required
	/// because <see cref="Time"/> and <see cref="FrameCount"/> are accumulators that cannot be
	/// recomputed on demand once they diverge from wall-clock.
	/// </remarks>
	public sealed class RealtimeGameClock : IGameClock
	{
		public float DeltaTime { get; private set; }
		public float UnscaledDeltaTime { get; private set; }
		public double Time { get; private set; }
		public int FrameCount { get; private set; }

		public float TimeScale
		{
			get => _timeScale;
			set => _timeScale = value < 0f ? 0f : value;
		}

		public bool IsPaused { get; private set; }
		
		private float _timeScale = 1f;

		public void Pause() => IsPaused = true;

		public void Resume() => IsPaused = false;

		/// <summary>
		/// Advances the clock by one frame. Call once per frame, before any reader runs.
		/// Snapshots the scaled and unscaled deltas, then accumulates time and the frame count
		/// (both frozen while paused).
		/// </summary>
		public void Tick()
		{
			UnscaledDeltaTime = UnityEngine.Time.deltaTime;
			DeltaTime = IsPaused ? 0f : UnscaledDeltaTime * _timeScale;

			if (!IsPaused)
			{
				Time += DeltaTime;
				FrameCount++;
			}
		}
	}
}
