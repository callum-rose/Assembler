using System.Collections.Generic;
using Assembler.Behaviours;
using Assembler.Behaviours.Movement;
using Assembler.Behaviours.Visual;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	/// <summary>
	/// Covers the runtime side of `set behaviour enabled` / `toggle behaviour enabled`: each Execute resolves
	/// its target listeners (reusing <see cref="Listener.ResolveTargets"/>) and writes the target component's
	/// <c>enabled</c> flag. The targets are non-executable <see cref="Velocity"/> components — confirming that,
	/// unlike a Listeners: wiring, these behaviours can toggle self-driven behaviours.
	/// </summary>
	public class BehaviourEnabledTests
	{
		private static Listener DirectTarget(GameBehaviour target) =>
			new DirectListener(target, new Dictionary<string, string>());

		[Test]
		public void SetBehaviourEnabled_WritesEnabledToEveryTarget()
		{
			var host = new GameObject("host");
			var a = new GameObject("a");
			var b = new GameObject("b");
			try
			{
				var targetA = a.AddComponent<Velocity>();
				var targetB = b.AddComponent<Velocity>();
				targetA.enabled = true;
				targetB.enabled = true;

				var behaviour = host.AddComponent<SetBehaviourEnabled>();
				behaviour.Targets = new List<Listener> { DirectTarget(targetA), DirectTarget(targetB) };
				behaviour.Initialise(new SetBehaviourEnabledData("set", new ValueProvider<bool>(false)),
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
				var enabledTarget = on.AddComponent<Velocity>();
				var disabledTarget = off.AddComponent<Velocity>();
				enabledTarget.enabled = true;
				disabledTarget.enabled = false;

				var behaviour = host.AddComponent<ToggleBehaviourEnabled>();
				behaviour.Targets = new List<Listener> { DirectTarget(enabledTarget), DirectTarget(disabledTarget) };
				behaviour.Initialise(new ToggleBehaviourEnabledData("toggle"), new List<Listener>());

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
