using System.Linq;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;

namespace Tests.Parsing
{
	public class EntityTagTests
	{
		private static GameInfo Parse(string yaml) =>
			Transformer.Transform(new GameFileParser().Parse(yaml));

		private static EntityPropertySource<UnityEngine.Vector3> PositionSourceOf(GameInfo info, string entityId) =>
			(EntityPropertySource<UnityEngine.Vector3>)info.Entities.First(e => e.Id == entityId).InitialPosition;

		[Test]
		public void MappingEntityTagBecomesEntityPropertySource()
		{
			var info = Parse(@"
Entities:
  follower:
    Position: !entity { Id: leader, Property: Position }
  leader:
    Position: !vec { X: 1, Y: 2, Z: 3 }
");
			var source = PositionSourceOf(info, "follower");

			Assert.AreEqual("leader", source.EntityId.Id);
			Assert.AreEqual(EntityProperty.Position, source.Property);
		}

		[Test]
		public void RotationAndScalePropertiesParse()
		{
			var info = Parse(@"
Entities:
  reads rotation:
    Position: !entity { Id: leader, Property: Rotation }
  reads scale:
    Position: !entity { Id: leader, Property: Scale }
  leader:
    Position: !vec { X: 0, Y: 0, Z: 0 }
");
			Assert.AreEqual(EntityProperty.Rotation, PositionSourceOf(info, "reads rotation").Property);
			Assert.AreEqual(EntityProperty.Scale, PositionSourceOf(info, "reads scale").Property);
		}

		[Test]
		public void PropertyNameIsCaseInsensitive()
		{
			var info = Parse(@"
Entities:
  follower:
    Position: !entity { Id: leader, Property: position }
  leader:
    Position: !vec { X: 0, Y: 0, Z: 0 }
");
			Assert.AreEqual(EntityProperty.Position, PositionSourceOf(info, "follower").Property);
		}

		[Test]
		public void UnknownPropertyThrows()
		{
			var yaml = @"
Entities:
  follower:
    Position: !entity { Id: leader, Property: Spin }
  leader:
    Position: !vec { X: 0, Y: 0, Z: 0 }
";
			Assert.Throws<ParsingException>(() => Parse(yaml));
		}

		[Test]
		public void EntityTagInNonVector3ContextThrows()
		{
			var yaml = @"
Entities:
  hud:
    Behaviours:
      label:
        Type: text label
        Properties:
          Text: hi
          FontSize: !entity { Id: hud, Property: Position }
";
			Assert.Throws<ParsingException>(() => Parse(yaml));
		}

		[Test]
		public void MissingPropertyKeyThrows()
		{
			var yaml = @"
Entities:
  follower:
    Position: !entity { Id: leader }
  leader:
    Position: !vec { X: 0, Y: 0, Z: 0 }
";
			Assert.Catch(() => Parse(yaml));
		}

		[Test]
		public void OmittedIdBindsToEnclosingEntity()
		{
			// !entity with no Id is the self shorthand: a direct entity behaviour reading its own transform
			// resolves to that entity's id at instantiation (issue #400).
			var info = Parse(@"
Entities:
  spinner:
    Behaviours:
      translate:
        Type: translate
        Properties:
          Displacement: !entity { Property: Rotation }
");
			var translate = (TranslateInfo)info.Entities.First(e => e.Id == "spinner").Behaviours[0];
			var source = (EntityPropertySource<UnityEngine.Vector3>)translate.Displacement;

			Assert.AreEqual("spinner", source.EntityId.Id);
			Assert.IsInstanceOf<LiteralEntityId>(source.EntityId);
			Assert.IsNull(source.EntityId.PendingParameter);
			Assert.AreEqual(EntityProperty.Rotation, source.Property);
		}

		[Test]
		public void EmptyIdStringThrows()
		{
			// An explicit empty id is an authoring mistake, distinct from omitting Id entirely.
			var yaml = @"
Entities:
  follower:
    Position: !entity { Id: '', Property: Position }
";
			Assert.Catch(() => Parse(yaml));
		}
	}
}
