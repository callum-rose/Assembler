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
	}
}
