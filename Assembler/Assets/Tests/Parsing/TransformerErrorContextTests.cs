using Assembler.Deserialisation;
using Assembler.Parsing;
using NUnit.Framework;

namespace Tests.Parsing
{
	public class TransformerErrorContextTests
	{
		[Test]
		public void Convert_FailureOnVariable_IncludesVariableNameInMessage()
		{
			// An untagged YAML sequence deserialises to List<object>, which is not
			// one of the typed-list cases handled by Transformer.Convert and so
			// triggers the conversion failure.
			var yaml = @"
Variables:
  troublesome_list: [ 1, ""mixed"" ]
";

			var parser = new GameFileParser();
			var gameDto = parser.Parse(yaml);

			var ex = Assert.Throws<ParsingException>(() => Transformer.Transform(gameDto));
			StringAssert.Contains("troublesome_list", ex!.Message);
		}

		[Test]
		public void Convert_FailureOnConstant_IncludesConstantNameInMessage()
		{
			var yaml = @"
Constants:
  bad_const: [ 1, 2, 3, ""four"" ]
";

			var parser = new GameFileParser();
			var gameDto = parser.Parse(yaml);

			var ex = Assert.Throws<ParsingException>(() => Transformer.Transform(gameDto));
			StringAssert.Contains("bad_const", ex!.Message);
		}
	}
}
