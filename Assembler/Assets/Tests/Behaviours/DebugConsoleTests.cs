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
		// ---- TriggerLog (coalescing) ----

		[Test]
		public void TriggerLog_CoalescesRepeatedFiringsOfSameBehaviour()
		{
			var log = new TriggerLog(8);
			var hold = new BehaviourDescriptor("player", "hold");

			log.Record(1, hold, "KeyHold", new[] { "pressed" });
			log.Record(2, hold, "KeyHold", new[] { "pressed" });
			log.Record(5, hold, "KeyHold", new[] { "pressed" });

			var entry = log.Entries().Single(); // one row despite three firings
			Assert.AreEqual(3, entry.Count);
			Assert.AreEqual(1, entry.FirstFrame);
			Assert.AreEqual(5, entry.LastFrame);
		}

		[Test]
		public void TriggerLog_DropsLeastRecentWhenOverCapacity()
		{
			var log = new TriggerLog(2);
			log.Record(1, Descriptor("a"), "A", Array.Empty<string>());
			log.Record(2, Descriptor("b"), "B", Array.Empty<string>());
			log.Record(3, Descriptor("c"), "C", Array.Empty<string>()); // evicts "a" (least recent)

			Assert.AreEqual(2, log.Count);
			var ids = log.Entries().Select(e => e.Descriptor!.BehaviourId).ToArray();
			CollectionAssert.AreEqual(new[] { "c", "b" }, ids); // most recently fired first
		}

		[Test]
		public void TriggerLog_RecordsDescriptorAndKeys()
		{
			var log = new TriggerLog(4);
			log.Record(5, new BehaviourDescriptor("ball", "collide"), "X", new[] { "point", "other" });

			var entry = log.Entries().Single();
			Assert.AreEqual(5, entry.LastFrame);
			Assert.AreEqual(1, entry.Count);
			Assert.AreEqual("ball", entry.Descriptor!.EntityId);
			Assert.AreEqual("collide", entry.Descriptor!.BehaviourId);
			CollectionAssert.AreEqual(new[] { "point", "other" }, entry.Keys);
		}

		[Test]
		public void TriggerLog_ClearEmptiesBuffer()
		{
			var log = new TriggerLog(4);
			log.Record(1, Descriptor("b"), "B", Array.Empty<string>());
			log.Clear();

			Assert.AreEqual(0, log.Count);
			Assert.IsEmpty(log.Entries());
		}

		private static BehaviourDescriptor Descriptor(string behaviourId) => new("e", behaviourId);

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
