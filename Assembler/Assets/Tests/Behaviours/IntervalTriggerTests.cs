using System.Collections.Generic;
using Assembler.Behaviours;
using Assembler.Behaviours.Triggers.Timing;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class IntervalTriggerTests : BehaviourTestFixture
	{
		[Test]
		public void FireIteration_PublishesIncrementingIndexAndCount()
		{
			var go = Track(new GameObject("IntervalTriggerTestObject"));
			var trigger = go.AddComponent<IntervalTrigger>();

			var observedIndices = new List<int>();
			var observedCounts = new List<int>();

			var listener = new ActionListener(ctx =>
			{
				observedIndices.Add(ctx.Get<int>("iteration_index"));
				observedCounts.Add(ctx.Get<int>("iteration_count"));
			});

			var data = new IntervalTriggerData(
				id: "test_interval",
				interval: new ValueProvider<float>(0f),
				count: new ValueProvider<int>(3),
				autoStart: new ValueProvider<bool>(false));

			trigger.Initialise(data, new List<Listener> { listener });

			const int totalIterations = 3;
			for (int i = 0; i < totalIterations; i++)
			{
				trigger.FireIteration(i, totalIterations, TriggerContext.Empty);
			}

			CollectionAssert.AreEqual(new[] { 0, 1, 2 }, observedIndices);
			CollectionAssert.AreEqual(new[] { 3, 3, 3 }, observedCounts);
		}

		[Test]
		public void FireIteration_ForwardsUpstreamOutputsThroughToListeners()
		{
			var go = Track(new GameObject("IntervalTriggerTestObject"));
			var trigger = go.AddComponent<IntervalTrigger>();

			int observedOuter = 0;
			var listener = new ActionListener(ctx => observedOuter = ctx.Get<int>("outer"));

			var data = new IntervalTriggerData(
				id: "test_interval",
				interval: new ValueProvider<float>(0f),
				count: new ValueProvider<int>(1),
				autoStart: new ValueProvider<bool>(false));

			trigger.Initialise(data, new List<Listener> { listener });

			var upstream = TriggerContext.New("outer", 42);
			trigger.FireIteration(0, 1, upstream);

			Assert.AreEqual(42, observedOuter);
		}
	}
}
