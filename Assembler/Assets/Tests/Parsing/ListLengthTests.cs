using System.Linq;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Parsing
{
	public class ListLengthTests
	{
		[Test]
		public void IntListLength_BehaviourIsParsed()
		{
			var info = ParseSingleBehaviour(@"
Variables:
  - Id: items
    Value: !int []
  - Id: count
    Value: 0
Entities:
  e:
    Behaviours:
      - Type: int list length
        Id: measure
        Properties:
          List: !var items
          Length: !var count
");
			Assert.IsInstanceOf<ListLengthInfo<int>>(info);
			var typed = (ListLengthInfo<int>)info;
			Assert.AreEqual("measure", typed.Id);
			Assert.IsNotNull(typed.List);
			Assert.IsNotNull(typed.Length);
		}

		[Test]
		public void FloatListLength_BehaviourIsParsed()
		{
			var info = ParseSingleBehaviour(@"
Variables:
  - Id: items
    Value: !float []
  - Id: count
    Value: 0
Entities:
  e:
    Behaviours:
      - Type: float list length
        Id: measure
        Properties:
          List: !var items
          Length: !var count
");
			Assert.IsInstanceOf<ListLengthInfo<float>>(info);
		}

		[Test]
		public void BoolListLength_BehaviourIsParsed()
		{
			var info = ParseSingleBehaviour(@"
Variables:
  - Id: items
    Value: !bool []
  - Id: count
    Value: 0
Entities:
  e:
    Behaviours:
      - Type: bool list length
        Id: measure
        Properties:
          List: !var items
          Length: !var count
");
			Assert.IsInstanceOf<ListLengthInfo<bool>>(info);
		}

		[Test]
		public void StringListLength_BehaviourIsParsed()
		{
			var info = ParseSingleBehaviour(@"
Variables:
  - Id: items
    Value: !string []
  - Id: count
    Value: 0
Entities:
  e:
    Behaviours:
      - Type: string list length
        Id: measure
        Properties:
          List: !var items
          Length: !var count
");
			Assert.IsInstanceOf<ListLengthInfo<string>>(info);
		}

		[Test]
		public void VectorListLength_BehaviourIsParsed()
		{
			var info = ParseSingleBehaviour(@"
Variables:
  - Id: items
    Value: !vec []
  - Id: count
    Value: 0
Entities:
  e:
    Behaviours:
      - Type: vector list length
        Id: measure
        Properties:
          List: !var items
          Length: !var count
");
			Assert.IsInstanceOf<ListLengthInfo<Vector3>>(info);
		}

		[Test]
		public void ColourListLength_BehaviourIsParsed()
		{
			var info = ParseSingleBehaviour(@"
Variables:
  - Id: items
    Value: !colour []
  - Id: count
    Value: 0
Entities:
  e:
    Behaviours:
      - Type: colour list length
        Id: measure
        Properties:
          List: !var items
          Length: !var count
");
			Assert.IsInstanceOf<ListLengthInfo<Color>>(info);
		}

		[Test]
		public void AllListLengthBehaviours_AreRegistered()
		{
			Assert.IsTrue(BehaviourRegistry.All.ContainsKey("int list length"));
			Assert.IsTrue(BehaviourRegistry.All.ContainsKey("float list length"));
			Assert.IsTrue(BehaviourRegistry.All.ContainsKey("bool list length"));
			Assert.IsTrue(BehaviourRegistry.All.ContainsKey("string list length"));
			Assert.IsTrue(BehaviourRegistry.All.ContainsKey("vector list length"));
			Assert.IsTrue(BehaviourRegistry.All.ContainsKey("colour list length"));
		}

		private static BehaviourInfo ParseSingleBehaviour(string yaml)
		{
			var parser = new GameFileParser();
			var gameDto = parser.Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);
			var entity = gameInfo.Entities.Single();
			return entity.Behaviours.Single();
		}
	}
}
