using System.Linq;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;

namespace Tests.Parsing
{
	// Regression tests for #118: a quoted numeric YAML scalar (e.g. Key: "1") must be
	// treated as a string, while a bare numeric scalar stays an int.
	public class ScalarQuotingTests
	{
		private static ValueInfo Constant(string yaml)
		{
			var gameDto = new GameFileParser().Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			// Constants and Variables both merge into GameInfo.Variables during transform.
			return gameInfo.Variables.Single();
		}

		[Test]
		public void DoubleQuotedNumber_IsString()
		{
			var value = Constant("Constants:\n  c: \"1\"\n").Value;

			Assert.IsInstanceOf<StringValue>(value);
			Assert.AreEqual("1", ((StringValue)value).Value);
		}

		[Test]
		public void SingleQuotedNumber_IsString()
		{
			var value = Constant("Constants:\n  c: '1'\n").Value;

			Assert.IsInstanceOf<StringValue>(value);
			Assert.AreEqual("1", ((StringValue)value).Value);
		}

		[Test]
		public void BareNumber_StaysInt()
		{
			var value = Constant("Constants:\n  c: 1\n").Value;

			Assert.IsInstanceOf<IntValue>(value);
			Assert.AreEqual(1, ((IntValue)value).Value);
		}

		[Test]
		public void KeyDownTrigger_WithQuotedNumberKey_TransformsToStringConstant()
		{
			var yaml = @"
Entities:
  player:
    Behaviours:
      hotbar one:
        Type: key down trigger
        Properties: { Key: ""1"" }
";

			var gameDto = new GameFileParser().Parse(yaml);

			GameInfo gameInfo = null;
			Assert.DoesNotThrow(() => gameInfo = Transformer.Transform(gameDto));

			var trigger = (KeyDownTriggerInfo)gameInfo.Entities.Single().Behaviours.Single();

			Assert.IsInstanceOf<ConstantSource<string>>(trigger.Key);
			Assert.AreEqual("1", ((ConstantSource<string>)trigger.Key).Value);
		}
	}
}
