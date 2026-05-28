using System;
using System.Collections.Generic;
using Assembler.Behaviours;
using Assembler.Behaviours.Triggers.Timing;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class IntervalTriggerTests
	{
		private sealed class ActionListener : Listener
		{
			private readonly Action _action;

			public ActionListener(Action action, TriggerContext triggerContext)
				: base(new Dictionary<string, string>(), triggerContext)
			{
				_action = action;
			}

			public override void Notify() => _action();
		}

		[Test]
		public void FireIteration_PublishesIncrementingIndexAndCount_ToTriggerContext()
		{
			var go = new GameObject("IntervalTriggerTestObject");
			try
			{
				var trigger = go.AddComponent<IntervalTrigger>();
				var triggerContext = new TriggerContext();
				trigger.TriggerContext = triggerContext;

				var observedIndices = new List<int>();
				var observedCounts = new List<int>();

				Action listener = () =>
				{
					observedIndices.Add(triggerContext.Get<int>("iteration_index"));
					observedCounts.Add(triggerContext.Get<int>("iteration_count"));
				};

				var data = new IntervalTriggerData(
					id: "test_interval",
					interval: new ValueProvider<float>(0f),
					count: new ValueProvider<int>(3),
					autoStart: new ValueProvider<bool>(false));

				trigger.Initialise(data, new List<Listener> { new ActionListener(listener, triggerContext) });

				const int totalIterations = 3;
				for (int i = 0; i < totalIterations; i++)
				{
					trigger.FireIteration(i, totalIterations);
				}

				CollectionAssert.AreEqual(new[] { 0, 1, 2 }, observedIndices);
				CollectionAssert.AreEqual(new[] { 3, 3, 3 }, observedCounts);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void FireIteration_PoppingContextRestoresOuterFrame()
		{
			var go = new GameObject("IntervalTriggerTestObject");
			try
			{
				var trigger = go.AddComponent<IntervalTrigger>();
				var triggerContext = new TriggerContext();
				trigger.TriggerContext = triggerContext;

				triggerContext.Push();
				triggerContext.Set("outer", 42);

				var data = new IntervalTriggerData(
					id: "test_interval",
					interval: new ValueProvider<float>(0f),
					count: new ValueProvider<int>(1),
					autoStart: new ValueProvider<bool>(false));

				trigger.Initialise(data, new List<Listener> { new ActionListener(() => { }, triggerContext) });
				trigger.FireIteration(0, 1);

				Assert.AreEqual(42, triggerContext.Get<int>("outer"));
				triggerContext.Pop();
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}
	}
}
