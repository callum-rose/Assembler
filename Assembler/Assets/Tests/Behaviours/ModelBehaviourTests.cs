using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.Visual;
using Assembler.Parsing;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Behaviours
{
	public class ModelBehaviourTests : BehaviourTestFixture
	{
		// ------------------------------------------------------------------
		// Normalisation — Size is a true world bounding box, not a localScale.
		// ------------------------------------------------------------------

		[Test]
		public void Normalise_HalvesTheHeightOfTallPrimitives()
		{
			Assert.AreEqual(new Vector3(1f, 1.5f, 1f),
				ModelGeometry.Normalise(PrimitiveType.Cylinder, new Vector3(1f, 3f, 1f)),
				"Unity's cylinder is 2 units tall, so a 3-unit-tall part scales to 1.5.");
			Assert.AreEqual(new Vector3(2f, 1f, 2f),
				ModelGeometry.Normalise(PrimitiveType.Capsule, new Vector3(2f, 2f, 2f)),
				"Unity's capsule is 2 units tall, so a 2-unit-tall part scales to 1.");
		}

		[Test]
		public void Normalise_DividesPlaneByTenOnXAndZ()
		{
			Assert.AreEqual(new Vector3(1f, 1f, 2f),
				ModelGeometry.Normalise(PrimitiveType.Plane, new Vector3(10f, 1f, 20f)),
				"Unity's plane is 10 by 10, so a 10x20 part scales to 1x2 (Y is untouched).");
		}

		[Test]
		public void Normalise_LeavesUnitSizedPrimitivesAlone()
		{
			var size = new Vector3(2f, 3f, 4f);

			foreach (var shape in new[] { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Quad })
			{
				Assert.AreEqual(size, ModelGeometry.Normalise(shape, size),
					$"{shape} is already unit-sized, so Size is its localScale unchanged.");
			}
		}

		// ------------------------------------------------------------------
		// Anchor offset — the anchored point on the part lands on Position.
		// ------------------------------------------------------------------

		[Test]
		public void Anchor_OffsetsThePartOnEachAxisIndependently()
		{
			var size = new Vector3(2f, 4f, 6f);

			AssertPartLocalPosition("bottom", size, new Vector3(0f, 2f, 0f));
			AssertPartLocalPosition("left", size, new Vector3(1f, 0f, 0f));
			AssertPartLocalPosition("back", size, new Vector3(0f, 0f, 3f));
			AssertPartLocalPosition("top", size, new Vector3(0f, -2f, 0f));
			AssertPartLocalPosition("right", size, new Vector3(-1f, 0f, 0f));
			AssertPartLocalPosition("front", size, new Vector3(0f, 0f, -3f));
		}

		[Test]
		public void Anchor_CombinesAcrossAxes()
		{
			AssertPartLocalPosition("bottom-left-front", new Vector3(2f, 4f, 6f), new Vector3(1f, 2f, -3f));
		}

		[Test]
		public void Anchor_IsMeasuredFromThePartsPosition()
		{
			var part = NewPart(PrimitiveType.Cube,
				position: new ValueProvider<Vector3>(new Vector3(5f, 0f, 0f)),
				size: new ValueProvider<Vector3>(new Vector3(2f, 4f, 6f)),
				anchor: ModelAnchor.Parse("bottom", "test"));

			var child = BuildModel(part).transform.GetChild(0);

			Assert.AreEqual(new Vector3(5f, 2f, 0f), child.localPosition,
				"the anchor offset is added to Position, not applied instead of it.");
		}

		[Test]
		public void Anchor_IsRotatedWithThePart_SoRotationPivotsAboutIt()
		{
			// A 4-tall post anchored at its foot, tipped 90 degrees about Z: the foot stays at the origin and
			// the post lies along -X, so its centre sits at (-2, 0, 0) rather than (0, 2, 0).
			var part = NewPart(PrimitiveType.Cube,
				rotation: new ValueProvider<Vector3>(new Vector3(0f, 0f, 90f)),
				size: new ValueProvider<Vector3>(new Vector3(1f, 4f, 1f)),
				anchor: ModelAnchor.Parse("bottom", "test"));

			var child = BuildModel(part).transform.GetChild(0);

			Assert.That(Vector3.Distance(child.localPosition, new Vector3(-2f, 0f, 0f)), Is.LessThan(1e-4f),
				$"expected the part to pivot about its anchor, got {child.localPosition}.");
		}

		// ------------------------------------------------------------------
		// Anchor parsing.
		// ------------------------------------------------------------------

		[Test]
		public void AnchorParse_IsOrderAgnostic()
		{
			var expected = new Vector3(-1f, -1f, 1f);

			foreach (var raw in new[] { "bottom-left-front", "left-front-bottom", "front-bottom-left" })
			{
				Assert.AreEqual(expected, ModelAnchor.Parse(raw, "test"), $"'{raw}' should parse to {expected}.");
			}
		}

		[Test]
		public void AnchorParse_CentresOmittedAxes()
		{
			Assert.AreEqual(new Vector3(0f, -1f, 0f), ModelAnchor.Parse("bottom", "test"));
		}

		[Test]
		public void AnchorParse_RejectsTwoTokensForTheSameAxis()
		{
			foreach (var raw in new[] { "left-right", "top-bottom", "back-front", "left-left" })
			{
				var error = Assert.Throws<ParsingException>(() => ModelAnchor.Parse(raw, "model 'm' part 0"))!;
				Assert.That(error.Message, Does.Contain("more than once"),
					$"'{raw}' names one axis twice and should say so.");
				Assert.That(error.Message, Does.Contain("model 'm' part 0"),
					"the error should name the offending behaviour and part.");
			}
		}

		[Test]
		public void AnchorParse_RejectsUnknownAndEmptyTokens()
		{
			Assert.Throws<ParsingException>(() => ModelAnchor.Parse("middle", "test"));
			Assert.Throws<ParsingException>(() => ModelAnchor.Parse("bottom-", "test"));
			Assert.Throws<ParsingException>(() => ModelAnchor.Parse("", "test"));
		}

		// ------------------------------------------------------------------
		// Mirror.
		// ------------------------------------------------------------------

		[Test]
		public void Mirror_X_EmitsOneReflectedTwin()
		{
			var host = BuildModel(MirroredLeg(MirrorAxis.X));

			Assert.AreEqual(2, host.transform.childCount, "mirror x emits the original plus one twin.");
			Assert.AreEqual("Leg", host.transform.GetChild(0).name);
			Assert.AreEqual("Leg (mirrored x)", host.transform.GetChild(1).name);

			var twin = host.transform.GetChild(1);
			Assert.AreEqual(new Vector3(-2f, 0f, 3f), twin.localPosition, "mirror x negates Position.X.");
			AssertEulerAngles(twin, new Vector3(10f, -20f, -30f));
		}

		[Test]
		public void Mirror_Z_EmitsOneReflectedTwin()
		{
			var host = BuildModel(MirroredLeg(MirrorAxis.Z));

			Assert.AreEqual(2, host.transform.childCount, "mirror z emits the original plus one twin.");
			Assert.AreEqual("Leg (mirrored z)", host.transform.GetChild(1).name);

			var twin = host.transform.GetChild(1);
			Assert.AreEqual(new Vector3(2f, 0f, -3f), twin.localPosition, "mirror z negates Position.Z.");
			AssertEulerAngles(twin, new Vector3(-10f, -20f, 30f));
		}

		[Test]
		public void Mirror_XZ_EmitsThreeReflectedTwins()
		{
			var host = BuildModel(MirroredLeg(MirrorAxis.XZ));

			Assert.AreEqual(4, host.transform.childCount, "mirror xz emits the original plus three twins.");
			CollectionAssert.AreEqual(
				new[] { "Leg", "Leg (mirrored x)", "Leg (mirrored z)", "Leg (mirrored xz)" },
				Enumerable.Range(0, 4).Select(i => host.transform.GetChild(i).name).ToArray());

			var both = host.transform.GetChild(3);
			Assert.AreEqual(new Vector3(-2f, 0f, -3f), both.localPosition, "the xz twin negates both axes.");
			AssertEulerAngles(both, new Vector3(-10f, 20f, -30f));
		}

		[Test]
		public void Mirror_FlipsTheAnchorSoTwinsMeetInTheMiddle()
		{
			// A 2-wide part anchored on its left edge sits at +1; its X twin anchors on the right and sits
			// at -1. Both inner faces touch the origin, which is the point of a flipped anchor.
			var part = NewPart(PrimitiveType.Cube,
				size: new ValueProvider<Vector3>(new Vector3(2f, 2f, 2f)),
				anchor: ModelAnchor.Parse("left", "test"),
				mirror: new ValueProvider<MirrorAxis>(MirrorAxis.X));

			var host = BuildModel(part);

			Assert.AreEqual(new Vector3(1f, 0f, 0f), host.transform.GetChild(0).localPosition);
			Assert.AreEqual(new Vector3(-1f, 0f, 0f), host.transform.GetChild(1).localPosition);
		}

		[Test]
		public void Mirror_DoesNotNegateSize()
		{
			var part = NewPart(PrimitiveType.Cube,
				size: new ValueProvider<Vector3>(new Vector3(2f, 3f, 4f)),
				mirror: new ValueProvider<MirrorAxis>(MirrorAxis.XZ));

			var host = BuildModel(part);

			foreach (var child in Children(host))
			{
				Assert.AreEqual(new Vector3(2f, 3f, 4f), child.localScale,
					"a mirrored twin reflects position, rotation and anchor — never scale.");
			}
		}

		// ------------------------------------------------------------------
		// Colour fallback: part -> model -> shared material.
		// ------------------------------------------------------------------

		[Test]
		public void Colour_PartOverridesTheModelWideColour()
		{
			var host = BuildModel(
				new ValueProvider<Color>(Color.blue),
				NewPart(PrimitiveType.Cube, colour: new ValueProvider<Color>(Color.red)));

			Assert.AreEqual(Color.red, ReadBlockColour(host.transform.GetChild(0)));
		}

		[Test]
		public void Colour_FallsBackToTheModelWideColour()
		{
			var host = BuildModel(new ValueProvider<Color>(Color.green), NewPart(PrimitiveType.Cube));

			Assert.AreEqual(Color.green, ReadBlockColour(host.transform.GetChild(0)));
		}

		[Test]
		public void Colour_WithNeitherSet_LeavesTheSharedMaterialUntouched()
		{
			var host = BuildModel(NewPart(PrimitiveType.Cube));

			Assert.IsFalse(host.transform.GetChild(0).GetComponent<MeshRenderer>().HasPropertyBlock(),
				"with no part or model colour the renderer should keep the shared material's own colour.");
		}

		// ------------------------------------------------------------------
		// Live binding — Size and the anchor offset share state.
		// ------------------------------------------------------------------

		[Test]
		public void LiveSize_RecomputesTheAnchorOffset()
		{
			var size = new ValueProvider<Vector3>(new Vector3(1f, 2f, 1f));
			var part = NewPart(PrimitiveType.Cube, size: size, anchor: ModelAnchor.Parse("bottom", "test"));
			var child = BuildModel(part).transform.GetChild(0);

			Assert.AreEqual(new Vector3(0f, 1f, 0f), child.localPosition);

			size.Set(new Vector3(1f, 6f, 1f));

			Assert.AreEqual(new Vector3(1f, 6f, 1f), child.localScale, "the new size should re-scale the mesh.");
			Assert.AreEqual(new Vector3(0f, 3f, 0f), child.localPosition,
				"a bottom-anchored part must re-derive its offset from the new size, not keep the old one.");
		}

		[Test]
		public void LivePosition_KeepsTheAnchorOffset()
		{
			var position = new ValueProvider<Vector3>(Vector3.zero);
			var part = NewPart(PrimitiveType.Cube,
				position: position,
				size: new ValueProvider<Vector3>(new Vector3(1f, 2f, 1f)),
				anchor: ModelAnchor.Parse("bottom", "test"));
			var child = BuildModel(part).transform.GetChild(0);

			position.Set(new Vector3(0f, 5f, 0f));

			Assert.AreEqual(new Vector3(0f, 6f, 0f), child.localPosition,
				"moving the part should keep its anchor offset rather than drop it.");
		}

		// ------------------------------------------------------------------
		// Visual only.
		// ------------------------------------------------------------------

		[Test]
		public void Model_LeavesNoColliderButKeepsEveryMesh()
		{
			var host = BuildModel(
				NewPart(PrimitiveType.Cube),
				NewPart(PrimitiveType.Sphere, mirror: new ValueProvider<MirrorAxis>(MirrorAxis.X)));

			Assert.IsNull(host.GetComponentInChildren<Collider>(),
				"model parts are visual only — the collider CreatePrimitive adds must be stripped.");
			Assert.AreEqual(3, host.GetComponentsInChildren<MeshRenderer>().Length,
				"every part (and every mirrored twin) should still render a mesh.");
		}

		[Test]
		public void Parts_AreNamedByIndexAndShapeWhenUnnamed()
		{
			var host = BuildModel(NewPart(PrimitiveType.Cube), NewPart(PrimitiveType.Cylinder));

			Assert.AreEqual("Part 0 (Cube)", host.transform.GetChild(0).name);
			Assert.AreEqual("Part 1 (Cylinder)", host.transform.GetChild(1).name);
		}

		// ------------------------------------------------------------------
		// Helpers.
		// ------------------------------------------------------------------

		private static ModelPart NewPart(PrimitiveType shape,
			IValueProvider<Vector3>? position = null,
			IValueProvider<Vector3>? rotation = null,
			IValueProvider<Vector3>? size = null,
			IValueProvider<Color>? colour = null,
			IValueProvider<string>? name = null,
			IValueProvider<MirrorAxis>? mirror = null,
			Vector3 anchor = default) =>
			new(new ValueProvider<PrimitiveType>(shape),
				position ?? NullValueProvider<Vector3>.Instance,
				rotation ?? NullValueProvider<Vector3>.Instance,
				size ?? NullValueProvider<Vector3>.Instance,
				colour ?? NullValueProvider<Color>.Instance,
				name ?? NullValueProvider<string>.Instance,
				mirror ?? NullValueProvider<MirrorAxis>.Instance,
				anchor);

		// A 2x4x2 leg at (2,0,3), rotated on all three axes so every mirrored rotation sign is observable.
		// The anchor stays centred here — the anchor flip has its own test, where it is readable on its own.
		private static ModelPart MirroredLeg(MirrorAxis mirror) =>
			NewPart(PrimitiveType.Cube,
				position: new ValueProvider<Vector3>(new Vector3(2f, 0f, 3f)),
				rotation: new ValueProvider<Vector3>(new Vector3(10f, 20f, 30f)),
				size: new ValueProvider<Vector3>(new Vector3(2f, 4f, 2f)),
				name: new ValueProvider<string>("Leg"),
				mirror: new ValueProvider<MirrorAxis>(mirror),
				anchor: Vector3.zero);

		private GameObject BuildModel(params ModelPart[] parts) =>
			BuildModel(NullValueProvider<Color>.Instance, parts);

		private GameObject BuildModel(IValueProvider<Color> colour, params ModelPart[] parts)
		{
			var host = Track(new GameObject("model host"));
			var model = host.AddComponent<Model>();
			model.LiveProperties = host.AddComponent<LivePropertyUpdater>();
			model.Initialise(new ModelData("m", parts, colour), Array.Empty<Listener>());
			return host;
		}

		private void AssertPartLocalPosition(string anchor, Vector3 size, Vector3 expected)
		{
			var part = NewPart(PrimitiveType.Cube,
				size: new ValueProvider<Vector3>(size),
				anchor: ModelAnchor.Parse(anchor, "test"));

			Assert.AreEqual(expected, BuildModel(part).transform.GetChild(0).localPosition,
				$"anchor '{anchor}' on a {size} part should offset the mesh by {expected}.");
		}

		private static void AssertEulerAngles(Transform transform, Vector3 expected)
		{
			var actual = Quaternion.Euler(expected);
			Assert.That(Quaternion.Angle(transform.localRotation, actual), Is.LessThan(1e-3f),
				$"expected euler {expected}, got {transform.localEulerAngles}.");
		}

		private static IEnumerable<Transform> Children(GameObject host) =>
			Enumerable.Range(0, host.transform.childCount).Select(host.transform.GetChild);

		private static Color ReadBlockColour(Transform child)
		{
			var block = new MaterialPropertyBlock();
			child.GetComponent<MeshRenderer>().GetPropertyBlock(block);
			return block.GetColor("_BaseColor");
		}
	}
}
