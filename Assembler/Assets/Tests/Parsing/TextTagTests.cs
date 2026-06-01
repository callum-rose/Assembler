using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;

namespace Tests.Parsing
{
	public class TextTagTests
	{
		private static GameInfo Parse(string yaml) =>
			Transformer.Transform(new GameFileParser().Parse(yaml));

		private static LocalizedTextSource<string> TextSourceOf(GameInfo info) =>
			(LocalizedTextSource<string>)((TextLabelInfo)info.Entities[0].Behaviours[0]).Text;

		[Test]
		public void ScalarTextTagBecomesLocalizedSourceWithNoArguments()
		{
			var yaml = @"
Entities:
  hud:
    Behaviours:
      label:
        Type: text label
        Properties:
          Text: !text menu.start
";
			var source = TextSourceOf(Parse(yaml));

			Assert.AreEqual("menu.start", source.Key);
			Assert.AreEqual(0, source.Arguments.Count);
		}

		[Test]
		public void MappingTextTagCarriesKeyAndArguments()
		{
			var yaml = @"
Variables:
  score: 0
Entities:
  hud:
    Behaviours:
      label:
        Type: text label
        Properties:
          Text: !text { Key: hud.score, Arguments: [ !var score ] }
";
			var source = TextSourceOf(Parse(yaml));

			Assert.AreEqual("hud.score", source.Key);
			Assert.AreEqual(1, source.Arguments.Count);
		}

		[Test]
		public void LocalizationBlockTransformsIntoLocalizationInfo()
		{
			var yaml = @"
Localization:
  DefaultLocale: en
  Locales:
    en:
      a: Apple
    fr:
      a: Pomme
Entities:
  hud:
    Behaviours: {}
";
			var info = Parse(yaml);

			Assert.AreEqual("en", info.Localization.DefaultLocale);
			Assert.IsTrue(info.Localization.Locales.ContainsKey("en"));
			Assert.IsTrue(info.Localization.Locales.ContainsKey("fr"));
			Assert.AreEqual("Apple", info.Localization.Locales["en"]["a"]);
			Assert.AreEqual("Pomme", info.Localization.Locales["fr"]["a"]);
		}

		[Test]
		public void MissingLocalizationBlockYieldsEmptyInfo()
		{
			var info = Parse(@"
Entities:
  hud:
    Behaviours: {}
");

			Assert.AreEqual(0, info.Localization.Locales.Count);
		}

		[Test]
		public void TextTagInNonStringContextThrows()
		{
			var yaml = @"
Entities:
  hud:
    Behaviours:
      label:
        Type: text label
        Properties:
          Text: hi
          FontSize: !text some.key
";
			Assert.Throws<ParsingException>(() => Parse(yaml));
		}
	}
}
