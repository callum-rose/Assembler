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
  paddle_template:
    Behaviours:
      translate:
        Type: translate
        Properties:
          Displacement: !parameter speed
Entities:
  left paddle:
    Template:
      Id: paddle_template
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
		public void TemplateBehaviourCanReferenceOwnTransformViaSelfId()
		{
			var yaml = @"
Templates:
  mover_template:
    Behaviours:
      translate:
        Type: translate
        Properties:
          Displacement: !entity { Id: !parameter self_id, Property: Position }
Entities:
  mover:
    Template: { Id: mover_template }
";

			var gameInfo = Transformer.Transform(new GameFileParser().Parse(yaml));

			var translate = (TranslateInfo)gameInfo.Entities[0].Behaviours[0];
			var source = (EntityPropertySource<Vector3>)translate.Displacement;

			// self_id substituted in at instantiation, so the source reads the instance's own transform.
			Assert.AreEqual("mover", source.EntityId);
			Assert.IsNull(source.EntityIdParameter);
			Assert.AreEqual(EntityProperty.Position, source.Property);
		}

		[Test]
		public void TemplateBehaviourEntityParameterResolvesToSuppliedId()
		{
			// !entity { Id: !parameter ... } also threads a non-self parameter through, resolved from
			// the supplied template parameters (mirroring listeners' EntityId: !parameter <name>).
			var yaml = @"
Templates:
  follower_template:
    Behaviours:
      translate:
        Type: translate
        Properties:
          Displacement: !entity { Id: !parameter target, Property: Position }
Entities:
  follower:
    Template:
      Id: follower_template
      Parameters:
        target: leader
";

			var gameInfo = Transformer.Transform(new GameFileParser().Parse(yaml));

			var translate = (TranslateInfo)gameInfo.Entities[0].Behaviours[0];
			var source = (EntityPropertySource<Vector3>)translate.Displacement;

			Assert.AreEqual("leader", source.EntityId);
			Assert.IsNull(source.EntityIdParameter);
		}

		[Test]
		public void TemplateBehaviourEntityParameterMissingThrows()
		{
			var yaml = @"
Templates:
  follower_template:
    Behaviours:
      translate:
        Type: translate
        Properties:
          Displacement: !entity { Id: !parameter target, Property: Position }
Entities:
  follower:
    Template: { Id: follower_template }
";

			Assert.Throws<ParsingException>(() => Transformer.Transform(new GameFileParser().Parse(yaml)));
		}

		[Test]
		public void TestTemplateOverride()
		{
			var yaml = @"
Templates:
  paddle_template:
    Position: !vec { X: 0, Y: 0 }
    Tags: [ paddle ]
Entities:
  right paddle:
    Template: { Id: paddle_template }
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
