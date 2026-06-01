namespace Assembler.Time
{
	/// <summary>
	/// A deterministic <see cref="IGameClock"/>: every <see cref="Tick"/> advances a constant delta rather than
	/// the wall-clock <see cref="UnityEngine.Time.deltaTime"/>. Required for byte-identical replay (Level 1
	/// determinism; see CLAUDE.md). Pause, step, and timescale semantics mirror <see cref="RealtimeGameClock"/>.
	/// </summary>
	/// <remarks>
	/// For Level 1, one logical tick equals one Unity Update frame; this clock just makes the per-frame delta
	/// constant. Decoupled accumulation (multiple sim ticks per render frame) is future work.
	/// </remarks>
	public sealed class FixedStepGameClock : ITickableClock
	{
		private readonly float _fixedDeltaTime;

		public FixedStepGameClock(float fixedDeltaTime = 1f / 60f)
		{
			_fixedDeltaTime = fixedDeltaTime;
		}

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
		private int _pendingSteps;

		public void Pause() => IsPaused = true;

		public void Resume() => IsPaused = false;

		public void Step(int frames = 1)
		{
			// Stepping only makes sense while paused; queued frames must not leak into a later pause.
			if (IsPaused && frames > 0)
			{
				_pendingSteps += frames;
			}
		}

		/// <summary>
		/// Advances the clock by one fixed frame. Call once per frame, before any reader runs. Unlike the
		/// realtime clock the delta is a constant, so the run is reproducible given the same tick count.
		/// </summary>
		public void Tick()
		{
			UnscaledDeltaTime = _fixedDeltaTime;

			// A queued step lets a paused clock advance exactly one frame, for frame-by-frame debugging.
			var stepping = IsPaused && _pendingSteps > 0;

			DeltaTime = IsPaused && !stepping ? 0f : _fixedDeltaTime * _timeScale;

			if (!IsPaused || stepping)
			{
				Time += DeltaTime;
				FrameCount++;

				if (stepping)
				{
					_pendingSteps--;
				}
			}
		}
	}
}
