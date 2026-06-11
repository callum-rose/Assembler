using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.Triggers.Variables;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class VariableChangedTriggerTests
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

#if DEBUG_CONSOLE
			public override IEnumerable<GameBehaviour> DebugTargets() => Enumerable.Empty<GameBehaviour>();
#endif
		}

		[Test]
		public void Set_NotifiesListenersWithNewAndPreviousValues()
		{
			var go = new GameObject("VariableChangedTriggerTestObject");
			try
			{
				var trigger = go.AddComponent<IntVariableChangedTrigger>();
				var variable = new ValueProvider<int>(5);

				var observedValues = new List<int>();
				var observedPrevious = new List<int>();
				var listener = new ActionListener(ctx =>
				{
					observedValues.Add(ctx.Get<int>("value"));
					observedPrevious.Add(ctx.Get<int>("previous"));
				});

				trigger.Initialise(new VariableChangedTriggerData<int>("on changed", variable),
					new List<Listener> { listener });

				variable.Set(7);
				variable.Set(10);

				CollectionAssert.AreEqual(new[] { 7, 10 }, observedValues);
				CollectionAssert.AreEqual(new[] { 5, 7 }, observedPrevious);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void Set_ToSameValue_DoesNotFire()
		{
			var go = new GameObject("VariableChangedTriggerTestObject");
			try
			{
				var trigger = go.AddComponent<IntVariableChangedTrigger>();
				var variable = new ValueProvider<int>(5);

				var fires = 0;
				trigger.Initialise(new VariableChangedTriggerData<int>("on changed", variable),
					new List<Listener> { new ActionListener(_ => fires++) });

				variable.Set(5);

				Assert.AreEqual(0, fires);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}
	}
}
