using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Parsing
{
	public class ListGetAtTests
	{
		[Test]
		public void IntListGetAt_ParsesToListGetAtInfo()
		{
			AssertParsesToInfo(typeof(ListGetAtInfo<int>),
				"int list get at",
				"items", "!int [ 1, 2, 3 ]");
		}

		[Test]
		public void FloatListGetAt_ParsesToListGetAtInfo()
		{
			AssertParsesToInfo(typeof(ListGetAtInfo<float>),
				"float list get at",
				"items", "!float [ 1.0, 2.0, 3.0 ]");
		}

		[Test]
		public void BoolListGetAt_ParsesToListGetAtInfo()
		{
			AssertParsesToInfo(typeof(ListGetAtInfo<bool>),
				"bool list get at",
				"items", "!bool [ true, false ]");
		}

		[Test]
		public void StringListGetAt_ParsesToListGetAtInfo()
		{
			AssertParsesToInfo(typeof(ListGetAtInfo<string>),
				"string list get at",
				"items", "!string [ a, b, c ]");
		}

		[Test]
		public void VectorListGetAt_ParsesToListGetAtInfo()
		{
			AssertParsesToInfo(typeof(ListGetAtInfo<Vector3>),
				"vector list get at",
				"items", "!vec [ { X: 1, Y: 0 }, { X: 0, Y: 1 } ]");
		}

		[Test]
		public void ColourListGetAt_ParsesToListGetAtInfo()
		{
			AssertParsesToInfo(typeof(ListGetAtInfo<Color>),
				"colour list get at",
				"items", "!colour [ '#ff0000', '#00ff00' ]");
		}

		private static void AssertParsesToInfo(System.Type expectedInfoType,
			string behaviourType,
			string listVariableId,
			string listLiteral)
		{
			var yaml = $@"
Variables:
  {listVariableId}: {listLiteral}
Entities:
  reader:
    Behaviours:
      get:
        Type: {behaviourType}
        Properties:
          List: !var {listVariableId}
          Index: 0
";

			var parser = new GameFileParser();
			var gameDto = parser.Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);

			Assert.AreEqual(1, gameInfo.Entities.Count);
			var entity = gameInfo.Entities[0];
			Assert.AreEqual(1, entity.Behaviours.Count);
			Assert.IsInstanceOf(expectedInfoType, entity.Behaviours[0]);
		}
	}
}
