using System;
using System.Collections.Generic;
using System.Reflection;
using Assembler.Behaviours;
using Assembler.Behaviours.Flow;
using Assembler.Behaviours.Gating;
using Assembler.Behaviours.Movement;
using Assembler.Behaviours.Triggers.Timing;
using Assembler.Behaviours.UI;
using Assembler.Building;
using Assembler.Resolving;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	/// <summary>Guards the executable/non-executable partition introduced for issue #201: only
	/// <see cref="IAmExecutable"/> behaviours are valid <c>Listeners:</c> targets, triggers and self-driven
	/// behaviours expose no public <c>Execute</c>, and a listener resolving to a non-executable target fails
	/// loudly rather than silently doing nothing.</summary>
	public class IAmExecutableContractTests
	{
		[Test]
		public void ActionBehaviours_AreExecutable()
		{
			Assert.IsTrue(typeof(IAmExecutable).IsAssignableFrom(typeof(EndGame)),
				"EndGame must be IAmExecutable or the `!gameover` listener target breaks.");
			Assert.IsTrue(typeof(IAmExecutable).IsAssignableFrom(typeof(Translate)));
		}

		[Test]
		public void ForwardingAndGatingTriggers_AreExecutable()
		{
			foreach (var t in new[]
			{
				typeof(ConditionGate), typeof(InverseConditionGate), typeof(ExclusiveTrigger),
				typeof(DebouncedTrigger), typeof(ThrottledTrigger), typeof(DeferredTrigger),
				typeof(TimerTrigger), typeof(IntervalTrigger),
			})
			{
				Assert.IsTrue(typeof(IAmExecutable).IsAssignableFrom(t),
					$"{t.Name} is a forwarding/gating trigger and must be a valid listener target.");
			}
		}

		[Test]
		public void ContinuousAndSourceBehaviours_AreNotExecutable()
		{
			foreach (var t in new[]
			{
				typeof(Velocity), typeof(Acceleration), typeof(EveryFrameTrigger), typeof(OnStartTrigger),
				typeof(UIButton),
			})
			{
				Assert.IsFalse(typeof(IAmExecutable).IsAssignableFrom(t),
					$"{t.Name} runs itself or only emits — it must not be a listener target.");
			}
		}

		[Test]
		public void TargetingNonExecutableBehaviour_ThrowsDescriptiveError()
		{
			var go = new GameObject("velocity");
			try
			{
				IReadOnlyList<GameBehaviour> targets = new GameBehaviour[] { go.AddComponent<Velocity>() };
				var listener = new BehaviourTaggedListener(
					new ValueProvider<string>("movers"),
					_ => targets,
					new Dictionary<string, string>());

				var ex = Assert.Throws<InvalidOperationException>(() => listener.Notify(TriggerContext.Empty));
				StringAssert.Contains("not", ex!.Message);
				StringAssert.Contains("Velocity", ex.Message);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		// Every registered behaviour must keep the partition consistent: a public Execute(TriggerContext) is
		// only legal when the type implements IAmExecutable, and an IAmExecutable type must not also be
		// self-driven (a private Update()). Catches a future behaviour that re-introduces the silent no-op.
		[Test]
		public void RegisteredBehaviours_KeepExecutablePartition()
		{
			foreach (var monoType in GameBehaviourFactory.MonoBehaviourByInfo.Values)
			{
				var execute = monoType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Instance,
					null, new[] { typeof(TriggerContext) }, null);
				var executable = typeof(IAmExecutable).IsAssignableFrom(monoType);

				if (execute is not null)
				{
					Assert.IsTrue(executable,
						$"{monoType.Name} exposes a public Execute(TriggerContext) but is not IAmExecutable.");
				}

				if (executable)
				{
					var update = monoType.GetMethod("Update",
						BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
						null, Type.EmptyTypes, null);
					Assert.IsNull(update,
						$"{monoType.Name} is IAmExecutable yet declares Update() — self-driven behaviours " +
						"run themselves and must not be listener targets.");
				}
			}
		}
	}
}
