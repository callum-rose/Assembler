using System.Linq;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Parsing
{
	public class TemplateTests
	{
		[Test]
		public void TestTemplateExpansion()
		{
			var yaml = @"
Templates:
  - Id: paddle_template
    Behaviours:
      - Type: translate
        Properties:
          Displacement: !parameter speed
Entities:
  - Id: left paddle
    Template: paddle_template
    Parameters:
      speed: !vec { X: 0, Y: 2 }
";

			var parser = new GameFileParser();
			var gameDto = parser.Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);

			Assert.AreEqual(1, gameInfo.Entities.Count);
			var paddle = gameInfo.Entities[0];
			Assert.AreEqual("left paddle", paddle.Id);
			Assert.AreEqual(1, paddle.Behaviours.Count);

			var translate = paddle.Behaviours[0] as TranslateInfo;
			Assert.IsNotNull(translate);

			// Check if displacement was resolved from parameter
			var constantSource = translate.Displacement as ConstantSource<Vector3>;
			Assert.IsNotNull(constantSource);
			Assert.AreEqual(new Vector3(0, 2, 0), constantSource.Value);
		}

		[Test]
		public void TestTemplateOverride()
		{
			var yaml = @"
Templates:
  - Id: paddle_template
    Position: !vec { X: 0, Y: 0 }
    Tags: [ paddle ]
Entities:
  - Id: right paddle
    Template: paddle_template
    Position: !vec { X: 5, Y: 0 }
    Tags: [ right_paddle ]
";

			var parser = new GameFileParser();
			var gameDto = parser.Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);

			Assert.AreEqual(1, gameInfo.Entities.Count);
			var paddle = gameInfo.Entities[0];
			var constantSource = paddle.InitialPosition as ConstantSource<Vector3>;
			Assert.IsNotNull(constantSource);
			Assert.AreEqual(new Vector3(5, 0, 0), constantSource.Value);
			Assert.Contains("right_paddle", paddle.Tags.ToList());
			Assert.IsFalse(paddle.Tags.Contains("paddle"));
		}
	}
}