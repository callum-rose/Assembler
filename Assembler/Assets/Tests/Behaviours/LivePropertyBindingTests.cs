using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Assembler.Behaviours;
using Assembler.Resolving;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class LivePropertyBindingTests
	{
		private readonly List<GameObject> _spawned = new();

		[TearDown]
		public void TearDown()
		{
			foreach (var go in _spawned)
			{
				if (go != null)
				{
					UnityEngine.Object.DestroyImmediate(go);
				}
			}

			_spawned.Clear();
		}

		[Test]
		public void Push_AppliesInitial_AndOnVariableSet()
		{
			var (behaviour, _) = NewBehaviour();
			var provider = new ValueProvider<int>(1);
			var applied = new List<int>();

			provider.BindLive(behaviour, applied.Add, fallback: -1);

			CollectionAssert.AreEqual(new[] { 1 }, applied, "should apply the initial value on bind.");

			provider.Set(5);

			CollectionAssert.AreEqual(new[] { 1, 5 }, applied, "should re-apply when the variable is written.");
		}

		[Test]
		public void Push_AppliesOnObservableExpressionArgChange()
		{
			var (behaviour, _) = NewBehaviour();
			var arg = new ValueProvider<int>(2);
			var provider = new ObservableExpressionValueProvider<int>(
				ctx => arg.Get(ctx) * 10,
				new IObservableValueProvider[] { arg });
			var applied = new List<int>();

			provider.BindLive(behaviour, applied.Add, fallback: -1);

			CollectionAssert.AreEqual(new[] { 20 }, applied);

			arg.Set(3);

			CollectionAssert.AreEqual(new[] { 20, 30 }, applied, "should re-apply when an observable expression arg changes.");
		}

		[Test]
		public void Poll_ReReadsEachTick_AndReAppliesOnlyOnChange()
		{
			var (behaviour, updater) = NewBehaviour();
			var provider = new MutableProvider<int>(1);
			var applied = new List<int>();

			provider.BindLive(behaviour, applied.Add, fallback: -1);

			CollectionAssert.AreEqual(new[] { 1 }, applied);
			Assert.AreEqual(1, TickCount(updater), "a non-observable provider should register exactly one poll tick.");

			InvokeUpdate(updater); // value unchanged -> no re-apply
			CollectionAssert.AreEqual(new[] { 1 }, applied);

			provider.Value = 2;
			InvokeUpdate(updater); // changed -> re-apply
			InvokeUpdate(updater); // unchanged again -> no re-apply
			CollectionAssert.AreEqual(new[] { 1, 2 }, applied);
		}

		[Test]
		public void Push_RegistersNoTick_ForConstant()
		{
			var (behaviour, updater) = NewBehaviour();
			var constant = new ValueProvider<int>(7);

			constant.BindLive(behaviour, _ => { }, fallback: -1);

			Assert.AreEqual(0, TickCount(updater), "a constant binds via push and must register no per-frame tick.");
		}

		[Test]
		public void Push_RegistersNoTick_ForAllObservableExpression()
		{
			var (behaviour, updater) = NewBehaviour();
			var arg = new ValueProvider<int>(1);
			var provider = new ObservableExpressionValueProvider<int>(
				ctx => arg.Get(ctx), new IObservableValueProvider[] { arg });

			provider.BindLive(behaviour, _ => { }, fallback: -1);

			Assert.AreEqual(0, TickCount(updater), "an all-observable expression binds via push and registers no tick.");
		}

		[Test]
		public void Null_AppliesFallback_AndRegistersNoTick()
		{
			var (behaviour, updater) = NewBehaviour();
			var applied = new List<int>();

			NullValueProvider<int>.Instance.BindLive(behaviour, applied.Add, fallback: 99);

			CollectionAssert.AreEqual(new[] { 99 }, applied, "an omitted (Null) provider should apply the fallback once.");
			Assert.AreEqual(0, TickCount(updater));
			Assert.IsNull(behaviour.GetComponent<LivePropertyBindings>(), "Null binding should not even create the sink.");
		}

		[Test]
		public void Teardown_StopsApplying_AfterOwnerDestroyed()
		{
			var (behaviour, _) = NewBehaviour();
			var provider = new ValueProvider<int>(1);
			var applied = new List<int>();

			provider.BindLive(behaviour, applied.Add, fallback: -1);
			provider.Set(2);
			CollectionAssert.AreEqual(new[] { 1, 2 }, applied);

			// EditMode doesn't auto-run OnDestroy, so drive the sink's teardown directly — this is the same
			// callback Unity invokes when the entity GameObject is destroyed at runtime.
			var sink = behaviour.GetComponent<LivePropertyBindings>();
			Assert.IsNotNull(sink, "a push binding should have created the cleanup sink.");
			InvokeOnDestroy(sink);

			provider.Set(3);

			CollectionAssert.AreEqual(new[] { 1, 2 }, applied, "no apply should run after the binding is torn down.");
		}

		private (TestBehaviour behaviour, LivePropertyUpdater updater) NewBehaviour()
		{
			var go = new GameObject("live-host");
			_spawned.Add(go);
			var updater = go.AddComponent<LivePropertyUpdater>();
			var behaviour = go.AddComponent<TestBehaviour>();
			behaviour.LiveProperties = updater;
			return (behaviour, updater);
		}

		private static void InvokeUpdate(LivePropertyUpdater updater) =>
			typeof(LivePropertyUpdater).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic)!
				.Invoke(updater, null);

		private static void InvokeOnDestroy(LivePropertyBindings sink) =>
			typeof(LivePropertyBindings).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)!
				.Invoke(sink, null);

		private static int TickCount(LivePropertyUpdater updater)
		{
			var ticks = (IList)typeof(LivePropertyUpdater)
				.GetField("_ticks", BindingFlags.Instance | BindingFlags.NonPublic)!
				.GetValue(updater)!;
			return ticks.Count;
		}

		private sealed class TestBehaviour : GameBehaviour, INeedsLiveProperties
		{
			public LivePropertyUpdater LiveProperties { get; set; } = null!;
		}

		// A deliberately non-observable provider: BindLive must route it through the polled path.
		private sealed class MutableProvider<T> : IValueProvider<T>
		{
			public T Value;

			public MutableProvider(T value) => Value = value;

			public T Get(TriggerContext ctx) => Value;

			object IValueProvider.Get(TriggerContext ctx) => Value!;
		}
	}
}
