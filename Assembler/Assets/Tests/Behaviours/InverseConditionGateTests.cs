using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.Gating;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class InverseConditionGateTests
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
		public void Execute_FalseCondition_NotifiesListeners()
		{
			var go = new GameObject("InverseConditionGateTestObject");
			try
			{
				var gate = go.AddComponent<InverseConditionGate>();

				var fired = 0;
				var listener = new ActionListener(_ => fired++);

				gate.Initialise(new ConditionGateData("test_inverse_gate", new ValueProvider<bool>(false)),
					new List<Listener> { listener });

				gate.Execute(TriggerContext.Empty);

				Assert.AreEqual(1, fired);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void Execute_TrueCondition_DoesNotNotifyListeners()
		{
			var go = new GameObject("InverseConditionGateTestObject");
			try
			{
				var gate = go.AddComponent<InverseConditionGate>();

				var fired = 0;
				var listener = new ActionListener(_ => fired++);

				gate.Initialise(new ConditionGateData("test_inverse_gate", new ValueProvider<bool>(true)),
					new List<Listener> { listener });

				gate.Execute(TriggerContext.Empty);

				Assert.AreEqual(0, fired);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}
	}
}
