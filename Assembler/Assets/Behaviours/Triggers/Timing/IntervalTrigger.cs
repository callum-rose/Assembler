using System.Collections;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Timing
{
	/// <summary>Fires repeatedly at an interval. Optionally limited to a number of repetitions.</summary>
	/// <remarks>
	/// Properties:
	///   Interval: Seconds between fires. Re-read before each wait, so binding it to a variable that other
	///     behaviours mutate changes the live tick rate (e.g. accelerating gravity as a level increases).
	///   Count: Number of times to fire; 0 means fire forever. Re-read each iteration, so a variable-bound
	///     count can extend or shorten the run live.
	///   AutoStart: When true the timer starts on entity start; when false it waits for an Execute call from upstream.
	/// Outputs:
	///   iteration_index [int]: Zero-based index of the current fire (0 on the first fire, 1 on the second, etc.).
	///   iteration_count [int]: Total number of fires configured by Count; 0 when the trigger is unbounded.
	/// </remarks>
	public class IntervalTrigger : TimingTrigger<IntervalTriggerData>, INeedsGameClock
	{
		public IGameClock Clock { get; set; } = null!;

		private Coroutine? _currentCoroutine;

		private void Start()
		{
			Execute(TriggerContext.Empty);
		}

		public override void Execute(TriggerContext ctx)
		{
			bool timerIsRunning = _currentCoroutine is not null;

			if (timerIsRunning)
			{
				UnityEngine.Debug.LogWarning("Interval trigger already running. Restarting");
				StopCoroutine(_currentCoroutine);
			}

			var captured = ctx;

			_currentCoroutine = StartCoroutine(Routine(captured));
		}

		private IEnumerator Routine(TriggerContext captured)
		{
			for (int i = 0; ; i++)
			{
				// Re-read Interval and Count each iteration so binding either to a variable that other
				// behaviours mutate changes the live tick rate and repetition limit.
				int count = Data.Count.Get(captured);
				if (count != 0 && i >= count)
				{
					break;
				}

				yield return new WaitForGameSeconds(Clock, Data.Interval.Get(captured));

				FireIteration(i, count, captured);
			}

			_currentCoroutine = null;
		}

		public void FireIteration(int iterationIndex, int iterationCount, TriggerContext ctx)
		{
			NotifyListeners(ctx.With(b =>
			{
				b["iteration_index"] = iterationIndex;
				b["iteration_count"] = iterationCount;
			}));
		}
	}
}
