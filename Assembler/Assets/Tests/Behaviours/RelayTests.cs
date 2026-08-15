using System.Collections.Generic;
using Assembler.Behaviours;
using Assembler.Behaviours.Flow;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	/// <summary>A relay is the degenerate forwarding trigger: it never gates, so every Execute reaches every
	/// listener with the upstream context intact. That is what lets several triggers share one fan-out list
	/// instead of each repeating it.</summary>
	public class RelayTests
	{
		[Test]
		public void Execute_NotifiesEveryListener()
		{
			var go = new GameObject("RelayTestObject");
			try
			{
				var relay = go.AddComponent<Relay>();

				var first = 0;
				var second = 0;

				relay.Initialise(new RelayData("test_relay"), new List<Listener>
				{
					new ActionListener(_ => first++),
					new ActionListener(_ => second++),
				});

				relay.Execute(TriggerContext.Empty);

				Assert.AreEqual(1, first);
				Assert.AreEqual(1, second);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void Execute_ForwardsUpstreamContextUnchanged()
		{
			var go = new GameObject("RelayTestObject");
			try
			{
				var relay = go.AddComponent<Relay>();

				var received = Vector3.zero;
				relay.Initialise(new RelayData("test_relay"),
					new List<Listener> { new ActionListener(ctx => received = ctx.Get<Vector3>("contact_point")) });

				relay.Execute(TriggerContext.New("contact_point", new Vector3(1f, 2f, 3f)));

				Assert.AreEqual(new Vector3(1f, 2f, 3f), received);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void Execute_CalledRepeatedly_ForwardsEveryTime()
		{
			var go = new GameObject("RelayTestObject");
			try
			{
				var relay = go.AddComponent<Relay>();

				var fired = 0;
				relay.Initialise(new RelayData("test_relay"),
					new List<Listener> { new ActionListener(_ => fired++) });

				relay.Execute(TriggerContext.Empty);
				relay.Execute(TriggerContext.Empty);
				relay.Execute(TriggerContext.Empty);

				Assert.AreEqual(3, fired);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}
	}
}
