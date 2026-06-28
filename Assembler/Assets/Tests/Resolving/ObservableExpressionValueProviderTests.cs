using System;
using System.Collections.Generic;
using Assembler.Compiler.Compiler;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using Assembler.Time;
using NUnit.Framework;

namespace Tests.Resolving
{
	public class ObservableExpressionValueProviderTests
	{
		// ---- the provider in isolation -------------------------------------------

		[Test]
		public void Invalidated_RaisedAndRecomputes_WhenArgChanges()
		{
			var a = new ValueProvider<float>(1f);
			var b = new ValueProvider<float>(2f);
			var provider = new ObservableExpressionValueProvider<float>(
				ctx => a.Get(ctx) + b.Get(ctx),
				new IObservableValueProvider[] { a, b });

			Assert.AreEqual(3f, provider.Get(TriggerContext.Empty), 1e-4f);

			var pulses = 0;
			provider.Invalidated += () => pulses++;

			a.Set(5f);

			Assert.AreEqual(1, pulses, "an arg change should pulse Invalidated once.");
			Assert.AreEqual(7f, provider.Get(TriggerContext.Empty), 1e-4f, "the recomputed value should reflect the new arg.");
		}

		[Test]
		public void Invalidated_Suppressed_WhenResultUnchanged()
		{
			// The result ignores the args, so an arg pulse recomputes the same value and must not re-pulse.
			var a = new ValueProvider<float>(1f);
			var provider = new ObservableExpressionValueProvider<float>(
				_ => 42f,
				new IObservableValueProvider[] { a });

			provider.Get(TriggerContext.Empty);

			var pulses = 0;
			provider.Invalidated += () => pulses++;

			a.Set(99f);

			Assert.AreEqual(0, pulses, "a no-op recompute should not pulse Invalidated.");
		}

		[Test]
		public void Dispose_UnsubscribesFromArgs()
		{
			var a = new ValueProvider<float>(1f);
			var provider = new ObservableExpressionValueProvider<float>(
				ctx => a.Get(ctx),
				new IObservableValueProvider[] { a });

			provider.Get(TriggerContext.Empty);

			var pulses = 0;
			provider.Invalidated += () => pulses++;

			provider.Dispose();
			a.Set(5f);

			Assert.AreEqual(0, pulses, "after Dispose the provider must not react to arg changes.");
		}

		// ---- ValueResolver chooses the variant from the arg providers ------------

		[Test]
		public void Resolve_BuildsObservableVariant_WhenEveryArgIsObservable()
		{
			var registry = NewRegistry();
			registry.CompileAndRegisterAll(new[] { Add() });

			var ctx = ContextWith(registry, clock: null);

			// Constant args resolve to ValueProvider, which is observable-but-silent.
			var source = new ExpressionSource<float>("add", new IValueSourceArg[]
			{
				new ConstantSource<float>(1f),
				new ConstantSource<float>(2f)
			});

			var provider = source.Resolve(ctx);

			Assert.IsInstanceOf<ObservableExpressionValueProvider<float>>(provider);
			Assert.AreEqual(3f, provider.Get(TriggerContext.Empty), 1e-4f);
		}

		[Test]
		public void Resolve_BuildsPlainVariant_WhenAnArgIsAClock()
		{
			var registry = NewRegistry();
			registry.CompileAndRegisterAll(new[] { Add() });

			var fake = new FakeGameClock { DeltaTime = 0.5f };
			var ctx = ContextWith(registry, fake);

			// A !clock arg is not observable, so the whole expression must fall back to the polled variant.
			var source = new ExpressionSource<float>("add", new IValueSourceArg[]
			{
				new ConstantSource<float>(1f),
				new ClockValueSource<float>(ClockProperty.DeltaTime)
			});

			var provider = source.Resolve(ctx);

			Assert.IsInstanceOf<ExpressionValueProvider<float>>(provider);
			Assert.IsNotInstanceOf<ObservableExpressionValueProvider<float>>(provider);
			Assert.AreEqual(1.5f, provider.Get(TriggerContext.Empty), 1e-4f);
		}

		private static CompiledExpressionsRegistry NewRegistry() =>
			new(BuiltInTypeRegistry.Default, new ExpressionMethodCompiler());

		private static ExpressionInfo Add() =>
			new("add", new (string type, string name)[] { ("float", "a"), ("float", "b") },
				"float", Array.Empty<string>(), Array.Empty<string>(), "return a + b;");

		// Only the expression registry (and, for the clock case, the clock) are exercised; the other
		// registries are not touched while resolving a constant/clock-arg expression, so leave them null.
		private static ResolutionContext ContextWith(CompiledExpressionsRegistry expressions, IGameClock? clock) =>
			new(null!, expressions, null!, null!, null, null!, null!, clock!);

		private sealed class FakeGameClock : IGameClock
		{
			public float DeltaTime { get; set; }
			public float UnscaledDeltaTime { get; set; }
			public double Time { get; set; }
			public int FrameCount { get; set; }
			public float TimeScale { get; set; } = 1f;
			public bool IsPaused { get; set; }
			public void Pause() => IsPaused = true;
			public void Resume() => IsPaused = false;
			public void Step(int frames = 1) { }
		}
	}
}
