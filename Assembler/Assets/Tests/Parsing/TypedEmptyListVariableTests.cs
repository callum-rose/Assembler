using System.Collections.Generic;
using System.Linq;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Parsing
{
	/// <summary>
	/// Verifies that a Variable can be declared as an empty list using a typed
	/// YAML tag (e.g. <c>!colour []</c>) and consumed by a typed-list behaviour
	/// such as <c>colour list add</c>. Regression coverage for issue #25.
	/// </summary>
	public class TypedEmptyListVariableTests
	{
		[Test]
		public void EmptyColourListVariable_RegistersAsTypedListAndIsUsableByColourListAdd()
		{
			var yaml = @"
Variables:
  board: !colour []
Entities:
  painter:
    Behaviours:
      add_red:
        Type: colour list add
        Properties:
          List: !var board
          Value: !colour { R: 1, G: 0, B: 0, A: 1 }
";

			var parser = new GameFileParser();
			var gameDto = parser.Parse(yaml);
			var gameInfo = Transformer.Transform(gameDto);

			// The variable should be registered as a TypedListValue<Color>, not an
			// untyped List<object> (which is what bare `[]` would produce and is
			// the bug described in #25).
			var board = gameInfo.Variables.Single(v => v.Id == "board");
			Assert.IsInstanceOf<TypedListValue>(board.Value);
			var typed = (TypedListValue)board.Value;
			Assert.AreEqual(typeof(Color), typed.ElementType);
			Assert.AreEqual(0, typed.Items.Count);

			// The behaviour's List source must resolve to a ValueReferenceSource<IList<Color>>
			// rather than failing inside Transformer.Convert.
			var painter = gameInfo.Entities.Single();
			var addRed = painter.Behaviours.Single() as ListAddInfo<Color>;
			Assert.IsNotNull(addRed);
			Assert.IsInstanceOf<ValueReferenceSource<IList<Color>>>(addRed!.List);
			Assert.AreEqual("board", ((ValueReferenceSource<IList<Color>>)addRed.List).VariableId);

			// The runtime VariableRegistry should hand out a real, mutable List<Color>.
			var registry = new VariableRegistry();

			foreach (var value in gameInfo.Variables)
			{
				registry.Register(value);
			}

			var provider = registry.Get<IList<Color>>("board");
			Assert.IsNotNull(provider.Value);
			Assert.AreEqual(0, provider.Value.Count);

			provider.Value.Add(Color.red);
			Assert.AreEqual(1, registry.Get<IList<Color>>("board").Value.Count);
			Assert.AreEqual(Color.red, registry.Get<IList<Color>>("board").Value[0]);
		}

		[Test]
		public void UntypedEmptyListVariable_ThrowsWithGuidanceTowardTypedTag()
		{
			var yaml = @"
Variables:
  board: []
";

			var parser = new GameFileParser();
			var gameDto = parser.Parse(yaml);

			var ex = Assert.Throws<ParsingException>(() => Transformer.Transform(gameDto));
			StringAssert.Contains("!colour []", ex!.Message);
		}
	}
}
