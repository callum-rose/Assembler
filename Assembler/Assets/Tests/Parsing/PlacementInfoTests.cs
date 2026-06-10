using System.Collections.Generic;
using System.Linq;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Parsing
{
	public class PlacementInfoTests
	{
		private static GameInfo Transform(string yaml) =>
			Transformer.Transform(new GameFileParser().Parse(yaml));

		[Test]
		public void LiteralAtYieldsConstantPositionSource()
		{
			var gameInfo = Transform(@"
Templates:
  pill:
    Tags: [ pill ]
Placements:
  border pills:
    Template: pill
    At:
      - !vec { X: 0, Y: 0 }
      - !vec { X: 1, Y: 0 }
      - !vec { X: 2, Y: 0 }
");

			Assert.AreEqual(1, gameInfo.Placements.Count);
			var placement = gameInfo.Placements[0];
			Assert.AreEqual("border pills", placement.Id);
			Assert.AreEqual("pill", placement.TemplateId);

			var constant = placement.Positions as ConstantSource<List<Vector3>>;
			Assert.IsNotNull(constant, "literal At should produce a ConstantSource<List<Vector3>>");
			CollectionAssert.AreEqual(
				new[] { new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(2, 0, 0) },
				constant!.Value);
		}

		[Test]
		public void ExpressionAtYieldsExpressionPositionSource()
		{
			var gameInfo = Transform(@"
Templates:
  pill:
    Tags: [ pill ]
Placements:
  field pills:
    Template: pill
    At: !expr
      ReturnType: vector list
      Do: 'GridPositions(2, 2, 1f, arg0)'
      With: [ !vec { X: 0, Y: 0 } ]
      ArgumentTypes: [ vector ]
");

			var placement = gameInfo.Placements.Single();
			Assert.IsInstanceOf<ExpressionSource<List<Vector3>>>(placement.Positions,
				"!expr At should produce an ExpressionSource<List<Vector3>>");
		}

		[Test]
		public void UnknownTemplateThrows()
		{
			Assert.Throws<ParsingException>(() => Transform(@"
Placements:
  orphans:
    Template: nope
    At:
      - !vec { X: 0, Y: 0 }
"));
		}
	}
}
