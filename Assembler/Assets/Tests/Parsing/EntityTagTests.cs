using System.Linq;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
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

			Assert.AreEqual("leader", source.EntityId);
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
	}
}
