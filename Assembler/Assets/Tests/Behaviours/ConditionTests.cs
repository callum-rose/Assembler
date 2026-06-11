using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.Triggers.Conditionals;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	// `condition` gates an upstream trigger on a (named) boolean expression. At runtime it behaves exactly
	// like `condition gate` — the difference is only the authoring ABI — so these mirror the gate tests.
	public class ConditionTests
	{
		private sealed class ActionListener : Listener
		{
			private readonly Action<TriggerContext> _action;

			public ActionListener(Action<TriggerContext> action)
				: base(new Dictionary<string, string>()) => _action = action;

			public override void Notify(TriggerContext ctx) => _action(Prepare(ctx));

			public override IEnumerable<GameBehaviour> DebugTargets() => Enumerable.Empty<GameBehaviour>();
		}

		[Test]
		public void Execute_TrueCondition_NotifiesListeners()
		{
			var go = new GameObject(nameof(ConditionTests));
			try
			{
				var condition = go.AddComponent<Condition>();
				var fired = 0;
				condition.Initialise(new ConditionGateData("c", new ValueProvider<bool>(true)),
					new List<Listener> { new ActionListener(_ => fired++) });

				condition.Execute(TriggerContext.Empty);

				Assert.AreEqual(1, fired);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void Execute_FalseCondition_DoesNotNotifyListeners()
		{
			var go = new GameObject(nameof(ConditionTests));
			try
			{
				var condition = go.AddComponent<Condition>();
				var fired = 0;
				condition.Initialise(new ConditionGateData("c", new ValueProvider<bool>(false)),
					new List<Listener> { new ActionListener(_ => fired++) });

				condition.Execute(TriggerContext.Empty);

				Assert.AreEqual(0, fired);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}
	}
}
