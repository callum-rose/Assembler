using System.Linq;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;

namespace Tests.Parsing
{
	public class RigidbodyTagTests
	{
		private static RigidbodyPropertySource<UnityEngine.Vector3> VelocitySourceOf(GameInfo info, string entityId) =>
			(RigidbodyPropertySource<UnityEngine.Vector3>)info.Entities.First(e => e.Id == entityId).InitialPosition;

		[Test]
		public void MappingRigidbodyTagBecomesRigidbodyPropertySource()
		{
			var info = ParseHelper.ParseGame(@"
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
		public void AngularVelocityPositionAndRotationPropertiesParse()
		{
			var info = ParseHelper.ParseGame(@"
Entities:
  reads angular:
    Position: !rigidbody { Id: leader, Property: AngularVelocity }
  reads position:
    Position: !rigidbody { Id: leader, Property: Position }
  reads rotation:
    Position: !rigidbody { Id: leader, Property: Rotation }
  leader:
    Position: !vec { X: 0, Y: 0, Z: 0 }
");
			Assert.AreEqual(RigidbodyProperty.AngularVelocity, VelocitySourceOf(info, "reads angular").Property);
			Assert.AreEqual(RigidbodyProperty.Position, VelocitySourceOf(info, "reads position").Property);
			Assert.AreEqual(RigidbodyProperty.Rotation, VelocitySourceOf(info, "reads rotation").Property);
		}

		[Test]
		public void PropertyNameIsCaseInsensitive()
		{
			var info = ParseHelper.ParseGame(@"
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
			Assert.Throws<ParsingException>(() => ParseHelper.ParseGame(yaml));
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
			Assert.Throws<ParsingException>(() => ParseHelper.ParseGame(yaml));
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
			Assert.Catch(() => ParseHelper.ParseGame(yaml));
		}

		[Test]
		public void RigidbodyTagNestsAsObjectArgumentOfText()
		{
			// A !rigidbody used as a !text argument is untyped, so it parses as RigidbodyPropertySource<object>
			// (mirroring how a nested !text parses as LocalisedTextSource<object>) — issue #523.
			var info = ParseHelper.ParseGame(@"
Entities:
  hud:
    Behaviours:
      label:
        Type: text label
        Properties:
          Text: !text { Key: hud.vel, Arguments: [ !rigidbody { Id: hud, Property: Velocity } ] }
");
			var text = (LocalisedTextSource<string>)((TextLabelInfo)info.Entities[0].Behaviours[0]).Text;
			var nested = (RigidbodyPropertySource<object>)text.Arguments[0];

			Assert.AreEqual("hud", nested.EntityId);
			Assert.AreEqual(RigidbodyProperty.Velocity, nested.Property);
		}
	}
}
