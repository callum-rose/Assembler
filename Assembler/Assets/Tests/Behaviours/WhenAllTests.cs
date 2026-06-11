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
	public class WhenAllTests
	{
		private sealed class ActionListener : Listener
		{
			private readonly Action<TriggerContext> _action;

			public ActionListener(Action<TriggerContext> action)
				: base(new Dictionary<string, string>()) => _action = action;

			public override void Notify(TriggerContext ctx) => _action(Prepare(ctx));

			public override IEnumerable<GameBehaviour> DebugTargets() => Enumerable.Empty<GameBehaviour>();
		}

		// A stand-in upstream trigger: NotifyListeners is protected, so expose a public fire to drive the
		// fire hook that `when all` subscribes to.
		private sealed class StubTrigger : GameBehaviour
		{
			public void Fire() => NotifyListeners(TriggerContext.Empty);
		}

		[Test]
		public void FiresOnlyOnceAllTriggersHaveFired_ThenReArms()
		{
			var go = new GameObject(nameof(WhenAllTests));
			try
			{
				var whenAll = go.AddComponent<WhenAll>();
				var a = go.AddComponent<StubTrigger>();
				var b = go.AddComponent<StubTrigger>();

				var fired = 0;
				whenAll.Initialise(new WhenAllData("gate", new[] { "a", "b" }),
					new List<Listener> { new ActionListener(_ => fired++) });
				whenAll.Observe(a, "a");
				whenAll.Observe(b, "b");

				// One trigger, and even repeats of it, don't complete the AND.
				a.Fire();
				a.Fire();
				Assert.AreEqual(0, fired);

				// The last outstanding trigger completes it.
				b.Fire();
				Assert.AreEqual(1, fired);

				// It re-arms: a fresh round fires again.
				a.Fire();
				Assert.AreEqual(1, fired);
				b.Fire();
				Assert.AreEqual(2, fired);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}
	}
}
