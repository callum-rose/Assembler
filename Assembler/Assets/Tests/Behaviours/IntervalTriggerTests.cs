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
			private readonly Action<TriggerContext> _action;

			public ActionListener(Action<TriggerContext> action)
				: base(new Dictionary<string, string>())
			{
				_action = action;
			}

			public override void Notify(TriggerContext ctx) => _action(Prepare(ctx));
		}

		[Test]
		public void FireIteration_PublishesIncrementingIndexAndCount()
		{
			var go = new GameObject("IntervalTriggerTestObject");
			try
			{
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
		public void Invoke_RestoresPreviousContext_OnReturn()
		{
			var go = new GameObject("IntervalTriggerTestObject");
			try
			{
				var trigger = go.AddComponent<IntervalTrigger>();
				var holder = new TriggerContextHolder { Current = TriggerContext.Empty.With("outer", 42) };
				trigger.AttachContextHolder(holder);

				var data = new IntervalTriggerData(
					id: "test_interval",
					interval: new ValueProvider<float>(0f),
					count: new ValueProvider<int>(1),
					autoStart: new ValueProvider<bool>(false));

				trigger.Initialise(data, new List<Listener>());

				trigger.Invoke(TriggerContext.Empty.With("inner", 99));

				Assert.AreEqual(42, holder.Current.Get<int>("outer"));
				Assert.IsFalse(holder.Current.TryGet<int>("inner", out _));
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}
	}
}
