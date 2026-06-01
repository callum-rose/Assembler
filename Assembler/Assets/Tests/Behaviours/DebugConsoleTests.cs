using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Building.Debug;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class DebugConsoleTests
	{
		// ---- TriggerLog ring buffer ----

		[Test]
		public void TriggerLog_DropsOldestWhenOverCapacity()
		{
			var log = new TriggerLog(2);
			log.Append(Entry(1));
			log.Append(Entry(2));
			log.Append(Entry(3)); // evicts frame 1

			Assert.AreEqual(2, log.Count);
			var frames = log.Entries().Select(e => e.Frame).ToArray();
			CollectionAssert.AreEqual(new[] { 2, 3 }, frames); // oldest -> newest
		}

		[Test]
		public void TriggerLog_RecordsDescriptorAndKeys()
		{
			var log = new TriggerLog(4);
			log.Append(new TriggerLog.Entry(5, new BehaviourDescriptor("ball", "collide"), "X",
				new[] { "point", "other" }));

			var entry = log.Entries().Single();
			Assert.AreEqual(5, entry.Frame);
			Assert.AreEqual("ball", entry.Descriptor!.EntityId);
			Assert.AreEqual("collide", entry.Descriptor!.BehaviourId);
			CollectionAssert.AreEqual(new[] { "point", "other" }, entry.Keys);
		}

		[Test]
		public void TriggerLog_ClearEmptiesBuffer()
		{
			var log = new TriggerLog(4);
			log.Append(Entry(1));
			log.Clear();

			Assert.AreEqual(0, log.Count);
			Assert.IsEmpty(log.Entries());
		}

		private static TriggerLog.Entry Entry(int frame) =>
			new(frame, new BehaviourDescriptor("e", "b" + frame), "B", Array.Empty<string>());

		// ---- GameBehaviour.Fired hook (only present under DEBUG_CONSOLE) ----

#if DEBUG_CONSOLE
		private sealed class FiringBehaviour : GameBehaviour
		{
			public override void Execute(TriggerContext ctx) { }

			public void FireNow(TriggerContext ctx) => NotifyListeners(ctx);
		}

		[Test]
		public void GameBehaviour_Fired_RaisedOnNotifyListeners()
		{
			var go = new GameObject("fire");
			try
			{
				var behaviour = go.AddComponent<FiringBehaviour>();
				GameBehaviour? captured = null;

				void Handler(GameBehaviour source, TriggerContext ctx) => captured = source;

				GameBehaviour.Fired += Handler;
				try
				{
					behaviour.FireNow(TriggerContext.Empty);
				}
				finally
				{
					GameBehaviour.Fired -= Handler;
				}

				Assert.AreSame(behaviour, captured);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void DirectListener_DebugTargets_ReturnsTarget()
		{
			var go = new GameObject("target");
			try
			{
				var target = go.AddComponent<FiringBehaviour>();
				var listener = new DirectListener(target, new Dictionary<string, string>());

				CollectionAssert.AreEqual(new GameBehaviour[] { target }, listener.DebugTargets().ToList());
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(go);
			}
		}
#endif
	}
}
