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

		private static LocalisedTextSource<string> TextSourceOf(GameInfo info) =>
			(LocalisedTextSource<string>)((TextLabelInfo)info.Entities[0].Behaviours[0]).Text;

		[Test]
		public void ScalarTextTagBecomesLocalisedSourceWithNoArguments()
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
		public void LocalisationBlockTransformsIntoLocalisationInfo()
		{
			var yaml = @"
Localisation:
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

			Assert.AreEqual("en", info.Localisation.DefaultLocale);
			Assert.IsTrue(info.Localisation.Locales.ContainsKey("en"));
			Assert.IsTrue(info.Localisation.Locales.ContainsKey("fr"));
			Assert.AreEqual("Apple", info.Localisation.Locales["en"]["a"]);
			Assert.AreEqual("Pomme", info.Localisation.Locales["fr"]["a"]);
		}

		[Test]
		public void MissingLocalisationBlockYieldsEmptyInfo()
		{
			var info = Parse(@"
Entities:
  hud:
    Behaviours: {}
");

			Assert.AreEqual(0, info.Localisation.Locales.Count);
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
