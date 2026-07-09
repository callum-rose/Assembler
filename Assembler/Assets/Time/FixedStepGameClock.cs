namespace Assembler.Time
{
	/// <summary>
	/// A deterministic <see cref="IGameClock"/> that advances by a constant <see cref="Step"/> every tick,
	/// ignoring wall-clock entirely. Two runs of the same descriptor ticked the same number of times see an
	/// identical <see cref="Time"/> / <see cref="DeltaTime"/> / <see cref="FrameCount"/> sequence, so a
	/// non-physics game replays bit-identically (Level 1: same build, same machine).
	/// </summary>
	/// <remarks>
	/// Pause / <see cref="Step(int)"/> / <see cref="TimeScale"/> behave exactly as in
	/// <see cref="RealtimeGameClock"/>; the only difference is the delta source — a fixed constant instead of
	/// <see cref="UnityEngine.Time.deltaTime"/>. <see cref="GameClockDriver"/> additionally sets
	/// <see cref="UnityEngine.Time.captureDeltaTime"/> to <see cref="StepSeconds"/> (see
	/// <see cref="CaptureDeltaTime"/>) so Unity's frame cadence and physics accumulator march at the same step.
	/// </remarks>
	public sealed class FixedStepGameClock : IAdvancingGameClock
	{
		/// <summary>Default fixed step, matching a 60 FPS cadence.</summary>
		public const float DefaultStepSeconds = 1f / 60f;

		/// <summary>The constant unscaled seconds each <see cref="Tick"/> advances.</summary>
		public float StepSeconds { get; }

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

		// The fixed step is exactly what the driver captures Unity's global time at, so the engine loop and this
		// clock stay in lockstep. Unscaled by TimeScale: slow-mo scales game delta, not Unity's frame count.
		public float CaptureDeltaTime => StepSeconds;

		private float _timeScale = 1f;
		private int _pendingSteps;

		public FixedStepGameClock(float step = DefaultStepSeconds)
		{
			// A non-positive step would freeze time and break the capture cadence; clamp to the default.
			StepSeconds = step > 0f ? step : DefaultStepSeconds;
		}

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
		/// Advances the clock by one fixed frame. Call once per frame, before any reader runs. Snapshots the
		/// scaled and unscaled deltas from the constant <see cref="Step"/> (never wall-clock), then accumulates
		/// time and the frame count (both frozen while paused, except for a single queued <see cref="Step(int)"/>).
		/// </summary>
		public void Tick()
		{
			UnscaledDeltaTime = StepSeconds;

			// A queued step lets a paused clock advance exactly one frame, for frame-by-frame debugging.
			var stepping = IsPaused && _pendingSteps > 0;

			DeltaTime = IsPaused && !stepping ? 0f : StepSeconds * _timeScale;

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
