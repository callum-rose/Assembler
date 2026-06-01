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
		private float _timeScale = 1f;
		private bool _isPaused;

		private float _deltaTime;
		private float _unscaledDeltaTime;
		private double _time;
		private int _frameCount;

		public float DeltaTime => _deltaTime;

		public float UnscaledDeltaTime => _unscaledDeltaTime;

		public double Time => _time;

		public int FrameCount => _frameCount;

		public float TimeScale
		{
			get => _timeScale;
			set => _timeScale = value < 0f ? 0f : value;
		}

		public bool IsPaused => _isPaused;

		public void Pause() => _isPaused = true;

		public void Resume() => _isPaused = false;

		/// <summary>
		/// Advances the clock by one frame. Call once per frame, before any reader runs.
		/// Snapshots the scaled and unscaled deltas, then accumulates time and the frame count
		/// (both frozen while paused).
		/// </summary>
		public void Tick()
		{
			_unscaledDeltaTime = UnityEngine.Time.deltaTime;
			_deltaTime = _isPaused ? 0f : _unscaledDeltaTime * _timeScale;

			if (!_isPaused)
			{
				_time += _deltaTime;
				_frameCount++;
			}
		}
	}
}
