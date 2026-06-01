using System.Collections.Generic;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;

namespace Tests.Resolving
{
	public class StringTableRegistryTests
	{
		private static LocalisationInfo Info() => new(
			"en",
			new Dictionary<string, IReadOnlyDictionary<string, string>>
			{
				["en"] = new Dictionary<string, string> { ["greet"] = "Hello", ["only_en"] = "EnglishOnly" },
				["fr"] = new Dictionary<string, string> { ["greet"] = "Bonjour" }
			});

		private static StringTableRegistry Registry(string current)
		{
			var registry = new StringTableRegistry(new LocaleSettings(current));
			registry.LoadAll(Info());
			return registry;
		}

		[Test]
		public void ResolvesFromCurrentLocale()
		{
			Assert.AreEqual("Bonjour", Registry("fr").GetTemplate("greet"));
		}

		[Test]
		public void FallsBackToDefaultLocaleWhenKeyMissingInCurrent()
		{
			Assert.AreEqual("EnglishOnly", Registry("fr").GetTemplate("only_en"));
		}

		[Test]
		public void MissingKeyReturnsVisibleMarker()
		{
			Assert.AreEqual("#nope#", Registry("en").GetTemplate("nope"));
		}

		[Test]
		public void SwitchingLocaleChangesOutput()
		{
			Assert.AreEqual("Hello", Registry("en").GetTemplate("greet"));
			Assert.AreEqual("Bonjour", Registry("fr").GetTemplate("greet"));
		}
	}
}
