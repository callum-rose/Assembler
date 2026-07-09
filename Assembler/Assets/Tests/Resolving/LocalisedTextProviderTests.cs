using System;
using System.Collections.Generic;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;

namespace Tests.Resolving
{
	public class LocalisedTextProviderTests
	{
		private static StringTableRegistry Table(params (string key, string template)[] entries)
		{
			var table = new Dictionary<string, string>();

			foreach (var (key, template) in entries)
			{
				table[key] = template;
			}

			var registry = new StringTableRegistry(new LocaleSettings("en"));
			registry.LoadAll(new LocalisationInfo("en",
				new Dictionary<string, IReadOnlyDictionary<string, string>> { ["en"] = table }));
			return registry;
		}

		[Test]
		public void ZeroArgumentsReturnsTemplateVerbatim()
		{
			var provider = new LocalisedTextProvider(
				Table(("k", "Press Space")), "k", Array.Empty<IValueProvider>());

			Assert.AreEqual("Press Space", provider.Get(TriggerContext.Empty));
		}

		[Test]
		public void FillsPlaceholderWithArgumentValue()
		{
			var provider = new LocalisedTextProvider(
				Table(("k", "Score: {0}")), "k", new IValueProvider[] { new ValueProvider<int>(7) });

			Assert.AreEqual("Score: 7", provider.Get(TriggerContext.Empty));
		}

		[Test]
		public void FillsMultiplePlaceholdersInOrder()
		{
			var provider = new LocalisedTextProvider(
				Table(("k", "{0} vs {1}")), "k",
				new IValueProvider[] { new ValueProvider<int>(3), new ValueProvider<int>(5) });

			Assert.AreEqual("3 vs 5", provider.Get(TriggerContext.Empty));
		}

		[Test]
		public void EscapedBracesAreEmittedLiterally()
		{
			var provider = new LocalisedTextProvider(
				Table(("k", "{{literal}} {0}")), "k", new IValueProvider[] { new ValueProvider<int>(3) });

			Assert.AreEqual("{literal} 3", provider.Get(TriggerContext.Empty));
		}

		[Test]
		public void MissingKeyRendersMarker()
		{
			var provider = new LocalisedTextProvider(
				Table(("other", "x")), "absent", Array.Empty<IValueProvider>());

			Assert.AreEqual("#absent#", provider.Get(TriggerContext.Empty));
		}

		// Only the string table is exercised by a pure !text tree, so the other context registries are unused.
		private static ResolutionContext ContextWith(StringTableRegistry strings) =>
			new(Variables: null!, Expressions: null!, Assets: null!, Strings: strings,
				Scope: null!, EntityTransforms: null!, EntityQuery: null!, Clock: null!);

		[Test]
		public void NestedTextArgumentResolvesAndComposes()
		{
			var ctx = ContextWith(Table(("outer", "Placing: {0}"), ("inner", "Pulse Tower")));

			// A !text nested as an argument resolves as object (LocalisedTextSource<object>), mirroring how
			// the transform builds a !text argument.
			var inner = new LocalisedTextSource<object>("inner", Array.Empty<IValueSourceArg>());
			var outer = new LocalisedTextSource<string>("outer", new IValueSourceArg[] { inner });

			Assert.AreEqual("Placing: Pulse Tower", outer.Resolve(ctx).Get(TriggerContext.Empty));
		}

		[Test]
		public void DeeplyNestedTextArgumentsResolve()
		{
			var ctx = ContextWith(Table(("a", "[{0}]"), ("b", "<{0}>"), ("c", "leaf")));

			var c = new LocalisedTextSource<object>("c", Array.Empty<IValueSourceArg>());
			var b = new LocalisedTextSource<object>("b", new IValueSourceArg[] { c });
			var a = new LocalisedTextSource<string>("a", new IValueSourceArg[] { b });

			Assert.AreEqual("[<leaf>]", a.Resolve(ctx).Get(TriggerContext.Empty));
		}
	}
}
