using System.Linq;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Validation;
using NUnit.Framework;

namespace Tests.Parsing
{
	public class ValidationTests
	{
		private static ValidationResult Validate(string yaml)
		{
			var gameDto = new GameFileParser().Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			return GameInfoValidator.Validate(gameInfo);
		}

		[Test]
		public void MissingRequiredValue_IsFlaggedWithFix()
		{
			var yaml = @"
World:
  Dimensionality: 2
Entities:
  player:
    Behaviours:
      pusher:
        Type: add force
";

			var result = Validate(yaml);

			Assert.IsFalse(result.IsValid);
			Assert.AreEqual(1, result.Errors.Count);
			StringAssert.Contains("Force", result.Errors[0].Problem);
			Assert.IsNotEmpty(result.Errors[0].Fix);
		}

		[Test]
		public void IndependentProblems_AreAllAccumulated()
		{
			// No World (Dimensionality defaults to 0 → invalid) AND a missing required Force.
			var yaml = @"
Entities:
  player:
    Behaviours:
      pusher:
        Type: add force
";

			var result = Validate(yaml);

			Assert.IsFalse(result.IsValid);
			Assert.GreaterOrEqual(result.Errors.Count, 2);
			Assert.IsTrue(result.Errors.Any(e => e.Problem.Contains("Dimensionality")));
			Assert.IsTrue(result.Errors.Any(e => e.Problem.Contains("Force")));
		}

		[Test]
		public void UndefinedVariableReference_IsFlagged()
		{
			var yaml = @"
World:
  Dimensionality: 2
Entities:
  player:
    Behaviours:
      pusher:
        Type: add force
        Properties:
          Force: !var ghost
";

			var result = Validate(yaml);

			Assert.IsFalse(result.IsValid);
			Assert.IsTrue(result.Errors.Any(e => e.Problem.Contains("ghost")));
		}

		[Test]
		public void DuplicateAssetId_IsFlagged()
		{
			var yaml = @"
World:
  Dimensionality: 2
Assets:
  - Id: tex
    Type: sprite
    Path: first
  - Id: tex
    Type: sprite
    Path: second
";

			var result = Validate(yaml);

			Assert.IsFalse(result.IsValid);
			Assert.IsTrue(result.Errors.Any(e => e.Problem.Contains("duplicate") && e.Problem.Contains("tex")));
		}

		[Test]
		public void ValidGame_PassesValidation()
		{
			var yaml = @"
World:
  Dimensionality: 2
Entities:
  player:
    Behaviours:
      pusher:
        Type: add force
        Properties:
          Force: !vec { X: 0, Y: 10 }
";

			var result = Validate(yaml);

			Assert.IsTrue(result.IsValid, result.ToString());
		}
	}
}
