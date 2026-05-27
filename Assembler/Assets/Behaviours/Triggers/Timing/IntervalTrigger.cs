using System.Collections;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Triggers.Timing
{
	/// <summary>Fires repeatedly at a fixed interval. Optionally limited to a number of repetitions.</summary>
	/// <remarks>
	/// Properties:
	///   Interval: Seconds between fires.
	///   Count: Number of times to fire; 0 means fire forever.
	///   AutoStart: When true the timer starts on entity start; when false it waits for an Execute call from upstream.
	/// Outputs:
	///   iteration_index [int]: Zero-based index of the current fire (0 on the first fire, 1 on the second, etc.).
	///   iteration_count [int]: Total number of fires configured by Count; 0 when the trigger is unbounded.
	/// </remarks>
	public class IntervalTrigger : TimingTrigger<IntervalTriggerData>
	{
		private Coroutine? _currentCoroutine;

		private void Start()
		{
			Execute();
		}

		public override void Execute()
		{
			bool timerIsRunning = _currentCoroutine is not null;

			if (timerIsRunning)
			{
				UnityEngine.Debug.LogWarning("Interval trigger already running. Restarting");
				StopCoroutine(_currentCoroutine);
			}

			_currentCoroutine = StartCoroutine(Routine(Data.Interval.Value, Data.Count.Value));
		}

		private IEnumerator Routine(float interval, int count)
		{
			for (int i = 0; count == 0 || i < count; i++)
			{
				yield return new WaitForSeconds(interval);

				FireIteration(i, count);
			}

			_currentCoroutine = null;
		}

		public void FireIteration(int iterationIndex, int iterationCount)
		{
			TriggerContext.Push();
			try
			{
				TriggerContext.Set("iteration_index", iterationIndex);
				TriggerContext.Set("iteration_count", iterationCount);
				NotifyListeners();
			}
			finally
			{
				TriggerContext.Pop();
			}
		}
	}
}