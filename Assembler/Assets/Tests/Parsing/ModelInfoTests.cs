using System;
using System.Collections.Generic;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Parsing
{
	public class ModelInfoTests
	{
		private static TransformContext EmptyContext() =>
			new(new List<ValueInfo>(),
				new Dictionary<string, AssemblerValue>(),
				new Dictionary<string, ExpressionInfo>(),
				new Dictionary<string, Type>(),
				new Dictionary<Type, System.Reflection.MethodInfo>(),
				new InlineExpressionAccumulator(),
				RecordSchemaRegistry.Empty);

		private static ModelInfo Create(IReadOnlyDictionary<string, AssemblerValue> props) =>
			ModelInfo.Create("body", Array.Empty<ListenerInfo>(), props, EmptyContext());

		private static AssemblerValue Parts(params IReadOnlyDictionary<string, AssemblerValue>[] parts) =>
			new ListValue(Array.ConvertAll(parts, p => (AssemblerValue)new DictValue(p)));

		private static Dictionary<string, AssemblerValue> Part(string shape) =>
			new() { ["Shape"] = new StringValue(shape) };

		[Test]
		public void Create_ParsesEveryPartField()
		{
			var part = Part("cylinder");
			part["Anchor"] = new StringValue("bottom-left");
			part["Mirror"] = new StringValue("xz");
			part["Name"] = new StringValue("Leg");
			part["Size"] = new Vector3Value(new Vector3(1f, 3f, 1f));

			var info = Create(new Dictionary<string, AssemblerValue> { ["Parts"] = Parts(part) });

			Assert.AreEqual(1, info.Parts.Count);
			Assert.AreEqual(new Vector3(-1f, -1f, 0f), info.Parts[0].Anchor);
			Assert.AreEqual(new ConstantSource<ShapeKind>(ShapeKind.Cylinder), info.Parts[0].Shape);
			Assert.AreEqual(new ConstantSource<MirrorAxis>(MirrorAxis.XZ), info.Parts[0].Mirror);
			Assert.AreEqual(new ConstantSource<string>("Leg"), info.Parts[0].Name);
		}

		[Test]
		public void Create_OmittedPartFields_AreNoneSoTheBehaviourSuppliesTheDefault()
		{
			var info = Create(new Dictionary<string, AssemblerValue> { ["Parts"] = Parts(Part("cube")) });
			var part = info.Parts[0];

			Assert.AreEqual(None<Vector3>.Instance, part.Position);
			Assert.AreEqual(None<Vector3>.Instance, part.Rotation);
			Assert.AreEqual(None<Vector3>.Instance, part.Size);
			Assert.AreEqual(None<Color>.Instance, part.Colour);
			Assert.AreEqual(None<string>.Instance, part.Name);
			Assert.AreEqual(None<Color>.Instance, info.Colour, "an omitted model Colour must not become a constant.");
			Assert.AreEqual(Vector3.zero, part.Anchor, "an omitted Anchor centres every axis.");
			Assert.AreEqual(new ConstantSource<MirrorAxis>(MirrorAxis.None), part.Mirror);
		}

		[Test]
		public void Create_WithoutParts_PointsAtPrimitive()
		{
			var error = Assert.Throws<ParsingException>(() => Create(new Dictionary<string, AssemblerValue>()))!;

			Assert.That(error.Message, Does.Contain("model 'body'"));
			Assert.That(error.Message, Does.Contain("primitive"));
		}

		[Test]
		public void Create_WithMalformedParts_NamesTheOffendingPart()
		{
			Assert.Throws<ParsingException>(() => Create(new Dictionary<string, AssemblerValue>
			{
				["Parts"] = new StringValue("cube")
			}), "a scalar Parts is not a list of maps.");

			Assert.Throws<ParsingException>(() => Create(new Dictionary<string, AssemblerValue>
			{
				["Parts"] = new ListValue(Array.Empty<AssemblerValue>())
			}), "an empty Parts list is an authoring error, not a no-op model.");

			var error = Assert.Throws<ParsingException>(() => Create(new Dictionary<string, AssemblerValue>
			{
				["Parts"] = new ListValue(new AssemblerValue[]
				{
					new DictValue(Part("cube")),
					new StringValue("sphere")
				})
			}))!;
			Assert.That(error.Message, Does.Contain("part 1"), "the error should name the offending index.");
		}

		[Test]
		public void Create_PartWithoutShape_Throws()
		{
			var error = Assert.Throws<ParsingException>(() => Create(new Dictionary<string, AssemblerValue>
			{
				["Parts"] = Parts(new Dictionary<string, AssemblerValue>
				{
					["Size"] = new Vector3Value(Vector3.one)
				})
			}))!;

			Assert.That(error.Message, Does.Contain("Shape"));
			Assert.That(error.Message, Does.Contain("part 0"));
		}

		[Test]
		public void Create_BoundAnchor_IsRejectedWithAClearMessage()
		{
			var part = Part("cube");
			part["Anchor"] = new VarRef("anchor name");

			var error = Assert.Throws<ParsingException>(() => Create(
				new Dictionary<string, AssemblerValue> { ["Parts"] = Parts(part) }))!;

			Assert.That(error.Message, Does.Contain("Anchor must be a literal token"));
		}

		[Test]
		public void Create_UnknownAnchorToken_Throws()
		{
			var part = Part("cube");
			part["Anchor"] = new StringValue("middle");

			Assert.Throws<ParsingException>(() => Create(
				new Dictionary<string, AssemblerValue> { ["Parts"] = Parts(part) }));
		}
	}
}
