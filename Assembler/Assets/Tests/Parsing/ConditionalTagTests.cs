using System.Linq;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Parsing
{
	public class ConditionalTagTests
	{
		private static GameInfo Parse(string yaml) =>
			Transformer.Transform(new GameFileParser().Parse(yaml));

		private static ValueSource<Vector3> PositionSourceOf(GameInfo info, string entityId) =>
			info.Entities.First(e => e.Id == entityId).InitialPosition;

		[Test]
		public void MappingIfTagBecomesConditionalSource()
		{
			var info = Parse(@"
Entities:
  spawner:
    Position: !if
      Condition: !var mirrored
      Then: !vec { X: 1, Y: 2, Z: 0 }
      Else: !vec { X: -1, Y: 2, Z: 0 }
");
			var source = PositionSourceOf(info, "spawner");

			Assert.IsInstanceOf<ConditionalSource<Vector3>>(source);
			var conditional = (ConditionalSource<Vector3>)source;
			Assert.IsInstanceOf<ValueReferenceSource<bool>>(conditional.Condition);
			Assert.AreEqual("mirrored", ((ValueReferenceSource<bool>)conditional.Condition).VariableId);
			Assert.IsInstanceOf<ConstantSource<Vector3>>(conditional.Then);
			Assert.IsInstanceOf<ConstantSource<Vector3>>(conditional.Else);
		}

		[Test]
		public void ConditionMayBeAnExpression()
		{
			var info = Parse(@"
Entities:
  spawner:
    Position: !if
      Condition: !expr
        Do: 'arg0 > 0'
        With:
          - !var score
      Then: !vec { X: 1, Y: 0, Z: 0 }
      Else: !vec { X: 0, Y: 0, Z: 0 }
");
			var source = PositionSourceOf(info, "spawner");

			Assert.IsInstanceOf<ConditionalSource<Vector3>>(source);
			Assert.IsInstanceOf<ExpressionSource<bool>>(((ConditionalSource<Vector3>)source).Condition);
		}

		[Test]
		public void MissingConditionThrows()
		{
			var yaml = @"
Entities:
  spawner:
    Position: !if
      Then: !vec { X: 1, Y: 0, Z: 0 }
      Else: !vec { X: 0, Y: 0, Z: 0 }
";
			Assert.Catch(() => Parse(yaml));
		}

		[Test]
		public void MissingElseThrows()
		{
			var yaml = @"
Entities:
  spawner:
    Position: !if
      Condition: !var mirrored
      Then: !vec { X: 1, Y: 0, Z: 0 }
";
			Assert.Catch(() => Parse(yaml));
		}
	}
}
