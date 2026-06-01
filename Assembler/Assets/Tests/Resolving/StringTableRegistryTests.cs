using System.Collections.Generic;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;

namespace Tests.Resolving
{
	public class StringTableRegistryTests
	{
		private static LocalizationInfo Info() => new(
			"en",
			new Dictionary<string, IReadOnlyDictionary<string, string>>
			{
				["en"] = new Dictionary<string, string> { ["greet"] = "Hello", ["only_en"] = "EnglishOnly" },
				["fr"] = new Dictionary<string, string> { ["greet"] = "Bonjour" }
			});

		private static StringTableRegistry Registry(string current, string fallback)
		{
			var registry = new StringTableRegistry(new LocaleSettings(current, fallback));
			registry.LoadAll(Info());
			return registry;
		}

		[Test]
		public void ResolvesFromCurrentLocale()
		{
			Assert.AreEqual("Bonjour", Registry("fr", "en").GetTemplate("greet"));
		}

		[Test]
		public void FallsBackToFallbackLocaleWhenKeyMissingInCurrent()
		{
			Assert.AreEqual("EnglishOnly", Registry("fr", "en").GetTemplate("only_en"));
		}

		[Test]
		public void MissingKeyReturnsVisibleMarker()
		{
			Assert.AreEqual("#nope#", Registry("en", "en").GetTemplate("nope"));
		}

		[Test]
		public void SwitchingLocaleChangesOutput()
		{
			Assert.AreEqual("Hello", Registry("en", "en").GetTemplate("greet"));
			Assert.AreEqual("Bonjour", Registry("fr", "en").GetTemplate("greet"));
		}
	}
}
