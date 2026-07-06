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
			Assert.AreEqual("mover", source.EntityId.Id);
			Assert.IsNull(source.EntityId.PendingParameter);
			Assert.AreEqual(EntityProperty.Position, source.Property);
		}

		[Test]
		public void TemplateBehaviourCanReferenceOwnTransformViaOmittedId()
		{
			// !entity with the Id omitted is the self shorthand — equivalent to Id: !parameter self_id but
			// without the parameter plumbing — and binds to the instantiated entity's id (issue #400).
			var yaml = @"
Templates:
  mover_template:
    Behaviours:
      translate:
        Type: translate
        Properties:
          Displacement: !entity { Property: Position }
Entities:
  mover:
    Template: { Id: mover_template }
";

			var gameInfo = Transformer.Transform(new GameFileParser().Parse(yaml));

			var translate = (TranslateInfo)gameInfo.Entities[0].Behaviours[0];
			var source = (EntityPropertySource<Vector3>)translate.Displacement;

			Assert.AreEqual("mover", source.EntityId.Id);
			Assert.IsInstanceOf<LiteralEntityId>(source.EntityId);
			Assert.IsNull(source.EntityId.PendingParameter);
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

			Assert.AreEqual("leader", source.EntityId.Id);
			Assert.IsNull(source.EntityId.PendingParameter);
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
		public void EntityInheritsTemplatePosition()
		{
			var yaml = @"
Templates:
  base:
    Position: !vec { X: 5, Y: 3 }
Entities:
  entity_a:
    Template: { Id: base }
";

			var gameInfo = Transformer.Transform(new GameFileParser().Parse(yaml));

			var entity = gameInfo.Entities[0];
			var constantSource = entity.InitialPosition as ConstantSource<Vector3>;
			Assert.IsNotNull(constantSource, "Expected ConstantSource for inherited position");
			Assert.AreEqual(new Vector3(5, 3, 0), constantSource.Value);
		}

		[Test]
		public void EntityInheritsTemplateRotation()
		{
			var yaml = @"
Templates:
  base:
    Rotation: !vec { X: 0, Y: 0, Z: 45 }
Entities:
  entity_a:
    Template: { Id: base }
";

			var gameInfo = Transformer.Transform(new GameFileParser().Parse(yaml));

			var entity = gameInfo.Entities[0];
			var constantSource = entity.InitialRotation as ConstantSource<Vector3>;
			Assert.IsNotNull(constantSource, "Expected ConstantSource for inherited rotation");
			Assert.AreEqual(new Vector3(0, 0, 45), constantSource.Value);
		}

		[Test]
		public void EntityExplicitPositionOverridesTemplate()
		{
			var yaml = @"
Templates:
  base:
    Position: !vec { X: 5, Y: 3 }
Entities:
  entity_a:
    Template: { Id: base }
    Position: !vec { X: 10, Y: 0 }
";

			var gameInfo = Transformer.Transform(new GameFileParser().Parse(yaml));

			var entity = gameInfo.Entities[0];
			var constantSource = entity.InitialPosition as ConstantSource<Vector3>;
			Assert.IsNotNull(constantSource);
			Assert.AreEqual(new Vector3(10, 0, 0), constantSource.Value);
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
