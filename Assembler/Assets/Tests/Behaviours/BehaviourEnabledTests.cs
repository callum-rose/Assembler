using System.Collections.Generic;
using Assembler.Behaviours;
using Assembler.Behaviours.Visual;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	/// <summary>
	/// Covers the runtime side of `set behaviour enabled` / `toggle behaviour enabled`: each Execute resolves
	/// its <see cref="BehaviourTargets"/> and writes the target component's <c>enabled</c> flag. Target
	/// resolution against the live registry (direct + tag) is exercised by the build-coverage and sandbox
	/// paths; here the targets are supplied directly.
	/// </summary>
	public class BehaviourEnabledTests
	{
		private sealed class DummyBehaviour : MonoBehaviour { }

		[Test]
		public void SetBehaviourEnabled_WritesEnabledToEveryTarget()
		{
			var host = new GameObject("host");
			var a = new GameObject("a");
			var b = new GameObject("b");
			try
			{
				var targetA = a.AddComponent<DummyBehaviour>();
				var targetB = b.AddComponent<DummyBehaviour>();
				targetA.enabled = true;
				targetB.enabled = true;

				var behaviour = host.AddComponent<SetBehaviourEnabled>();
				behaviour.Initialise(
					new SetBehaviourEnabledData("set",
						new BehaviourTargets(_ => new Behaviour[] { targetA, targetB }),
						new ValueProvider<bool>(false)),
					new List<Listener>());

				behaviour.Execute(TriggerContext.Empty);

				Assert.IsFalse(targetA.enabled);
				Assert.IsFalse(targetB.enabled);
			}
			finally
			{
				Object.DestroyImmediate(host);
				Object.DestroyImmediate(a);
				Object.DestroyImmediate(b);
			}
		}

		[Test]
		public void ToggleBehaviourEnabled_FlipsEachTargetRelativeToItsOwnState()
		{
			var host = new GameObject("host");
			var on = new GameObject("on");
			var off = new GameObject("off");
			try
			{
				var enabledTarget = on.AddComponent<DummyBehaviour>();
				var disabledTarget = off.AddComponent<DummyBehaviour>();
				enabledTarget.enabled = true;
				disabledTarget.enabled = false;

				var behaviour = host.AddComponent<ToggleBehaviourEnabled>();
				behaviour.Initialise(
					new ToggleBehaviourEnabledData("toggle",
						new BehaviourTargets(_ => new Behaviour[] { enabledTarget, disabledTarget })),
					new List<Listener>());

				behaviour.Execute(TriggerContext.Empty);

				Assert.IsFalse(enabledTarget.enabled, "an enabled target flips off");
				Assert.IsTrue(disabledTarget.enabled, "a disabled target flips on");

				behaviour.Execute(TriggerContext.Empty);

				Assert.IsTrue(enabledTarget.enabled, "a second toggle restores the original state");
				Assert.IsFalse(disabledTarget.enabled);
			}
			finally
			{
				Object.DestroyImmediate(host);
				Object.DestroyImmediate(on);
				Object.DestroyImmediate(off);
			}
		}
	}
}
