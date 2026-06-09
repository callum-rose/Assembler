using System.Linq;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using NUnit.Framework;

namespace Tests.Parsing
{
	public class RigidbodyTagTests
	{
		private static GameInfo Parse(string yaml) =>
			Transformer.Transform(new GameFileParser().Parse(yaml));

		private static RigidbodyPropertySource<UnityEngine.Vector3> VelocitySourceOf(GameInfo info, string entityId) =>
			(RigidbodyPropertySource<UnityEngine.Vector3>)info.Entities.First(e => e.Id == entityId).InitialPosition;

		[Test]
		public void MappingRigidbodyTagBecomesRigidbodyPropertySource()
		{
			var info = Parse(@"
Entities:
  follower:
    Position: !rigidbody { Id: leader, Property: Velocity }
  leader:
    Position: !vec { X: 1, Y: 2, Z: 3 }
");
			var source = VelocitySourceOf(info, "follower");

			Assert.AreEqual("leader", source.EntityId);
			Assert.AreEqual(RigidbodyProperty.Velocity, source.Property);
		}

		[Test]
		public void AngularVelocityAndPositionPropertiesParse()
		{
			var info = Parse(@"
Entities:
  reads angular:
    Position: !rigidbody { Id: leader, Property: AngularVelocity }
  reads position:
    Position: !rigidbody { Id: leader, Property: Position }
  leader:
    Position: !vec { X: 0, Y: 0, Z: 0 }
");
			Assert.AreEqual(RigidbodyProperty.AngularVelocity, VelocitySourceOf(info, "reads angular").Property);
			Assert.AreEqual(RigidbodyProperty.Position, VelocitySourceOf(info, "reads position").Property);
		}

		[Test]
		public void PropertyNameIsCaseInsensitive()
		{
			var info = Parse(@"
Entities:
  follower:
    Position: !rigidbody { Id: leader, Property: velocity }
  leader:
    Position: !vec { X: 0, Y: 0, Z: 0 }
");
			Assert.AreEqual(RigidbodyProperty.Velocity, VelocitySourceOf(info, "follower").Property);
		}

		[Test]
		public void UnknownPropertyThrows()
		{
			var yaml = @"
Entities:
  follower:
    Position: !rigidbody { Id: leader, Property: Spin }
  leader:
    Position: !vec { X: 0, Y: 0, Z: 0 }
";
			Assert.Throws<ParsingException>(() => Parse(yaml));
		}

		[Test]
		public void RigidbodyTagInNonVector3ContextThrows()
		{
			var yaml = @"
Entities:
  hud:
    Behaviours:
      label:
        Type: text label
        Properties:
          Text: hi
          FontSize: !rigidbody { Id: hud, Property: Velocity }
";
			Assert.Throws<ParsingException>(() => Parse(yaml));
		}

		[Test]
		public void MissingPropertyKeyThrows()
		{
			var yaml = @"
Entities:
  follower:
    Position: !rigidbody { Id: leader }
  leader:
    Position: !vec { X: 0, Y: 0, Z: 0 }
";
			Assert.Catch(() => Parse(yaml));
		}
	}
}
