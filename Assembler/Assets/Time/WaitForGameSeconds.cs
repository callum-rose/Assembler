using UnityEngine;

namespace Assembler.Time
{
	/// <summary>
	/// Coroutine yield instruction that waits a number of <em>game</em> seconds, accumulating the
	/// injected <see cref="IGameClock"/>'s scaled delta. Mirrors Unity's <c>WaitForSecondsRealtime</c>,
	/// but respects pause and slow-mo: while the clock is paused the wait never advances.
	/// </summary>
	public sealed class WaitForGameSeconds : CustomYieldInstruction
	{
		private readonly IGameClock _clock;
		private readonly float _seconds;
		private float _elapsed;

		public WaitForGameSeconds(IGameClock clock, float seconds)
		{
			_clock = clock;
			_seconds = seconds;
		}

		public override bool keepWaiting
		{
			get
			{
				_elapsed += _clock.DeltaTime;
				return _elapsed < _seconds;
			}
		}
	}
}
