using Assembler.Time;

namespace Tests.Resolving
{
	/// <summary>
	/// Hand-driven <see cref="IGameClock"/> for unit tests: every property is settable, and
	/// <see cref="Advance"/> simulates a frame tick (accumulating <see cref="Time"/> and
	/// <see cref="FrameCount"/>). <c>Tick()</c> is not on the interface, so a fake need not implement it.
	/// </summary>
	public sealed class FakeGameClock : IGameClock
	{
		public float DeltaTime { get; set; }
		public float UnscaledDeltaTime { get; set; }
		public double Time { get; set; }
		public int FrameCount { get; set; }
		public float TimeScale { get; set; } = 1f;
		public bool IsPaused { get; set; }

		public void Pause()
		{
			IsPaused = true;
			DeltaTime = 0f;
		}

		public void Resume() => IsPaused = false;

		public void Step(int frames = 1) { }

		public void Advance(float seconds)
		{
			DeltaTime = seconds;
			Time += seconds;
			FrameCount++;
		}
	}
}
