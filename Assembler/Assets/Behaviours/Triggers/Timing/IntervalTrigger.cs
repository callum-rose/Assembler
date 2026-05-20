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
	/// </remarks>
	public class IntervalTrigger : TimingTrigger<IntervalTriggerData>
	{
		private Coroutine? _currentCoroutine;
		private bool _started;

		private void Start()
		{
			if (_started) return;
			_started = true;
			Execute();
		}

		public override void OnPostInitialise()
		{
			if (_started) return;
			_started = true;
			Execute();
		}

		public override void OnDespawn()
		{
			if (_currentCoroutine != null)
			{
				StopCoroutine(_currentCoroutine);
				_currentCoroutine = null;
			}
			_started = false;
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

				NotifyListeners();
			}

			_currentCoroutine = null;
		}
	}
}