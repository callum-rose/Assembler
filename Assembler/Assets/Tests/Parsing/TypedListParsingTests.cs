using System.Collections.Generic;
using System.Linq;
using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Parsing
{
	// Issue 206: a typed-list Variable/Constant can be seeded with initial elements, not just
	// declared empty (`!vec []`). These lock in the populated form end-to-end — transform to a
	// TypedListValue, then resolve to a concrete List<T> through the VariableRegistry — for the
	// vector and scalar element kinds (the record kind is covered by RecordParsingTests).
	public class TypedListParsingTests
	{
		private static GameInfo Transform(string yaml) => Transformer.Transform(new GameFileParser().Parse(yaml));

		[Test]
		public void PopulatedVecList_ResolvesToVectorListWithElements()
		{
			var info = Transform(@"
Variables:
  route: !vec [ { X: 1, Y: 0 }, { X: 2, Y: 0, Z: 3 } ]
");

			var value = info.Variables.Single(v => v.Id == "route").Value;
			Assert.IsInstanceOf<TypedListValue>(value);

			var typed = (TypedListValue)value;
			Assert.AreEqual(typeof(Vector3), typed.ElementType);
			Assert.AreEqual(2, typed.Items.Count);
			Assert.IsInstanceOf<Vector3Value>(typed.Items[0]);

			var registry = new VariableRegistry();
			registry.Register(info.Variables.Single(v => v.Id == "route"));

			var route = registry.Get<List<Vector3>>("route").Get();
			Assert.AreEqual(2, route.Count);
			Assert.AreEqual(new Vector3(1, 0, 0), route[0]);
			Assert.AreEqual(new Vector3(2, 0, 3), route[1]);
		}

		[Test]
		public void PopulatedIntList_ResolvesToIntListWithElements()
		{
			var info = Transform(@"
Variables:
  spawn weights: !int [ 5, 10, 15 ]
");

			var typed = (TypedListValue)info.Variables.Single(v => v.Id == "spawn weights").Value;
			Assert.AreEqual(typeof(int), typed.ElementType);
			Assert.AreEqual(3, typed.Items.Count);

			var registry = new VariableRegistry();
			registry.Register(info.Variables.Single(v => v.Id == "spawn weights"));

			Assert.AreEqual(new List<int> { 5, 10, 15 }, registry.Get<List<int>>("spawn weights").Get());
		}

		[Test]
		public void PopulatedStringList_ResolvesToStringListWithElements()
		{
			var info = Transform(@"
Constants:
  level names: !string [ intro, boss, finale ]
");

			var registry = new VariableRegistry();
			registry.Register(info.Variables.Single(v => v.Id == "level names"));

			Assert.AreEqual(new List<string> { "intro", "boss", "finale" },
				registry.Get<List<string>>("level names").Get());
		}

		[Test]
		public void EmptyVecList_ResolvesToEmptyVectorList()
		{
			var info = Transform(@"
Variables:
  occupied: !vec []
");

			var typed = (TypedListValue)info.Variables.Single(v => v.Id == "occupied").Value;
			Assert.AreEqual(typeof(Vector3), typed.ElementType);
			Assert.AreEqual(0, typed.Items.Count);

			var registry = new VariableRegistry();
			registry.Register(info.Variables.Single(v => v.Id == "occupied"));

			Assert.IsEmpty(registry.Get<List<Vector3>>("occupied").Get());
		}
	}
}
