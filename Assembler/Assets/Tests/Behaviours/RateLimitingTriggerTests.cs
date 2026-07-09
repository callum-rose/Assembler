using System.Collections.Generic;
using Assembler.Behaviours;
using Assembler.Behaviours.Gating;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	/// <summary>
	/// Rate-limiting meta triggers (debounce, throttle) driven by <see cref="FakeGameClock.Time"/> — asserts
	/// they suppress/drop notifications that arrive within their interval and forward the rest.
	/// </summary>
	public class RateLimitingTriggerTests : BehaviourTestFixture
	{
		// ---- Debounced trigger (clock.Time driven) ----

		[Test]
		public void Debounced_SuppressesWithinIntervalThenForwards()
		{
			var go = Track(new GameObject("debounced"));
			var fake = new FakeGameClock();
			var debounced = NewBehaviour<DebouncedTrigger>(go, fake);

			int fires = 0;
			debounced.Initialise(new DebouncedTriggerData("d", new ValueProvider<float>(1f)),
				new List<Listener> { new ActionListener(_ => fires++) });

			fake.Time = 0d;
			debounced.Execute(TriggerContext.Empty); // first: forwarded
			fake.Time = 0.5d;
			debounced.Execute(TriggerContext.Empty); // within interval: suppressed
			fake.Time = 2.0d;
			debounced.Execute(TriggerContext.Empty); // past interval: forwarded

			Assert.AreEqual(2, fires);
		}

		[Test]
		public void Debounced_StaysSuppressedWhileTimeFrozen()
		{
			var go = Track(new GameObject("debounced"));
			var fake = new FakeGameClock { Time = 0.5d };
			var debounced = NewBehaviour<DebouncedTrigger>(go, fake);

			int fires = 0;
			debounced.Initialise(new DebouncedTriggerData("d", new ValueProvider<float>(1f)),
				new List<Listener> { new ActionListener(_ => fires++) });

			debounced.Execute(TriggerContext.Empty); // first: forwarded
													 // Time frozen (e.g. paused): every subsequent trigger is within the interval.
			debounced.Execute(TriggerContext.Empty);
			debounced.Execute(TriggerContext.Empty);

			Assert.AreEqual(1, fires);
		}

		// ---- Throttled trigger (clock.Time driven) ----

		[Test]
		public void Throttled_DropsFasterThanRate()
		{
			var go = Track(new GameObject("throttled"));
			var fake = new FakeGameClock();
			var throttled = NewBehaviour<ThrottledTrigger>(go, fake);

			int fires = 0;
			throttled.Initialise(new ThrottledTriggerData("t", new ValueProvider<float>(2f)), // min interval 0.5s
				new List<Listener> { new ActionListener(_ => fires++) });

			fake.Time = 0d;
			throttled.Execute(TriggerContext.Empty); // forwarded
			fake.Time = 0.2d;
			throttled.Execute(TriggerContext.Empty); // dropped
			fake.Time = 0.6d;
			throttled.Execute(TriggerContext.Empty); // forwarded

			Assert.AreEqual(2, fires);
		}

		[Test]
		public void Throttled_RateZeroDropsAll()
		{
			var go = Track(new GameObject("throttled"));
			var fake = new FakeGameClock();
			var throttled = NewBehaviour<ThrottledTrigger>(go, fake);

			int fires = 0;
			throttled.Initialise(new ThrottledTriggerData("t", new ValueProvider<float>(0f)),
				new List<Listener> { new ActionListener(_ => fires++) });

			throttled.Execute(TriggerContext.Empty);
			fake.Time = 100d;
			throttled.Execute(TriggerContext.Empty);

			Assert.AreEqual(0, fires);
		}
	}
}
