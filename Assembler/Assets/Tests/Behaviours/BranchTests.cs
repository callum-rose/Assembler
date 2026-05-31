using System;
using System.Collections.Generic;
using Assembler.Behaviours;
using Assembler.Behaviours.Triggers.Conditionals;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class BranchTests
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
		public void Execute_TrueCondition_FiresOnlyTrueChannelListeners()
		{
			var go = new GameObject("BranchTestObject");
			try
			{
				var branch = go.AddComponent<Branch>();

				var trueFired = 0;
				var falseFired = 0;
				var trueListener = new ActionListener(_ => trueFired++) { When = true };
				var falseListener = new ActionListener(_ => falseFired++) { When = false };

				branch.Initialise(new BranchData("test_branch", new ValueProvider<bool>(true)),
					new List<Listener> { trueListener, falseListener });

				branch.Execute(TriggerContext.Empty);

				Assert.AreEqual(1, trueFired);
				Assert.AreEqual(0, falseFired);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void Execute_FalseCondition_FiresOnlyFalseChannelListeners()
		{
			var go = new GameObject("BranchTestObject");
			try
			{
				var branch = go.AddComponent<Branch>();

				var trueFired = 0;
				var falseFired = 0;
				var trueListener = new ActionListener(_ => trueFired++) { When = true };
				var falseListener = new ActionListener(_ => falseFired++) { When = false };

				branch.Initialise(new BranchData("test_branch", new ValueProvider<bool>(false)),
					new List<Listener> { trueListener, falseListener });

				branch.Execute(TriggerContext.Empty);

				Assert.AreEqual(0, trueFired);
				Assert.AreEqual(1, falseFired);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}
	}
}
