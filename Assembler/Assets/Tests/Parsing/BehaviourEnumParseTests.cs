using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;
using UnityEngine;
using AnimationInfo = Assembler.Parsing.Info.Behaviours.AnimationInfo;

namespace Tests.Parsing
{
	public class BehaviourEnumParseTests
	{
		[Test]
		public void ParseIsCaseInsensitive()
		{
			Assert.AreEqual(Easing.OutBack, BehaviourEnums.Parse<Easing>("OUTBACK"));
			Assert.AreEqual(CameraProjection.Orthographic, BehaviourEnums.Parse<CameraProjection>("Orthographic"));
			Assert.AreEqual(PrimitiveType.Sphere, BehaviourEnums.Parse<PrimitiveType>("sphere"));
		}

		[Test]
		public void ParseIgnoresSpacesAndDashes()
		{
			Assert.AreEqual(Easing.InOutSine, BehaviourEnums.Parse<Easing>(" in out sine "));
			Assert.AreEqual(TextAnchor.UpperLeft, BehaviourEnums.Parse<TextAnchor>("upper-left"));
			Assert.AreEqual(TextAnchor.MiddleCenter, BehaviourEnums.Parse<TextAnchor>("middle center"));
		}

		[Test]
		public void ParseAcceptsAllEnumKinds()
		{
			Assert.AreEqual(LayoutDirection.Horizontal, BehaviourEnums.Parse<LayoutDirection>("horizontal"));
			Assert.AreEqual(CameraFollowMode.ThreeD, BehaviourEnums.Parse<CameraFollowMode>("3d"));
			Assert.AreEqual(ButtonPhase.Hold, BehaviourEnums.Parse<ButtonPhase>("hold"));
		}

		[Test]
		public void ParseThrowsOnUnknownValue()
		{
			Assert.Throws<ParsingException>(() => BehaviourEnums.Parse<Easing>("wobble"));
			Assert.Throws<ParsingException>(() => BehaviourEnums.Parse<PrimitiveType>("blob"));
			Assert.Throws<ParsingException>(() => BehaviourEnums.Parse<CameraProjection>("isometric"));
		}

		private const string ColliderYaml = @"
Entities:
  e:
    Behaviours:
      body:
        Type: box collider
        Properties:
          Fit: bounds
";

		[Test]
		public void ColliderFitParsesToItsMember()
		{
			var box = (BoxColliderInfo)ParseHelper.ParseGame(ColliderYaml).Entities[0].Behaviours[0];

			Assert.AreEqual(ColliderFit.Bounds, ((ConstantSource<ColliderFit>)box.Fit).Value);
		}

		[Test]
		public void OmittedColliderFitFallsBackToNone()
		{
			var yaml = @"
Entities:
  e:
    Behaviours:
      body:
        Type: box collider
        Properties:
          Size: !vec { X: 1, Y: 1, Z: 1 }
";
			var box = (BoxColliderInfo)ParseHelper.ParseGame(yaml).Entities[0].Behaviours[0];

			Assert.AreEqual(ColliderFit.None, ((ConstantSource<ColliderFit>)box.Fit).Value);
		}

		[Test]
		public void UnknownColliderFitThrowsListingTheValidValues()
		{
			var yaml = @"
Entities:
  e:
    Behaviours:
      body:
        Type: sphere collider
        Properties:
          Fit: snug
";
			var error = Assert.Throws<ParsingException>(() => ParseHelper.ParseGame(yaml));

			StringAssert.Contains("none, bounds", error!.Message);
		}

		[Test]
		public void ColliderFitPartsPointsAtThePartCollidersBehaviour()
		{
			// 'parts' was an earlier spelling of the per-part mode before it became its own behaviour;
			// a descriptor written against that spelling should be told where the mode went.
			var yaml = @"
Entities:
  e:
    Behaviours:
      body:
        Type: box collider
        Properties:
          Fit: parts
";
			var error = Assert.Throws<ParsingException>(() => ParseHelper.ParseGame(yaml));

			StringAssert.Contains("part colliders", error!.Message);
		}

		[Test]
		public void PartCollidersParsesWithNoShapeProperty()
		{
			// The shape of each collider comes from the visual, so there is nothing to author beyond the
			// trigger flag and the physics-material properties it shares with the other collider behaviours.
			var yaml = @"
Entities:
  e:
    Behaviours:
      body:
        Type: part colliders
        Properties:
          IsTrigger: true
";
			var parts = (PartColliderInfo)ParseHelper.ParseGame(yaml).Entities[0].Behaviours[0];

			Assert.IsTrue(((ConstantSource<bool>)parts.IsTrigger).Value);
		}

		private const string AnimationYaml = @"
Entities:
  e:
    Behaviours:
      anim:
        Type: animation
        Properties:
          Animate: move
          End: !vec { X: 1, Y: 0, Z: 0 }
          Duration: 1
";

		[Test]
		public void OmittedEnumPropertyTakesItsDefault()
		{
			var anim = (AnimationInfo)ParseHelper.ParseGame(AnimationYaml).Entities[0].Behaviours[0];

			Assert.AreEqual(Easing.InOutSine, ((ConstantSource<Easing>)anim.Steps[0].Easing).Value);
		}

		[Test]
		public void PresentEnumLiteralParsesToItsMember()
		{
			var yaml = @"
Entities:
  e:
    Behaviours:
      anim:
        Type: animation
        Properties:
          Animate: move
          End: !vec { X: 1, Y: 0, Z: 0 }
          Duration: 1
          Easing: outBack
";
			var anim = (AnimationInfo)ParseHelper.ParseGame(yaml).Entities[0].Behaviours[0];

			Assert.AreEqual(Easing.OutBack, ((ConstantSource<Easing>)anim.Steps[0].Easing).Value);
		}

		[Test]
		public void InvalidEnumLiteralThrowsAtTransform()
		{
			var yaml = @"
Entities:
  e:
    Behaviours:
      shape:
        Type: primitive
        Properties:
          Shape: dodecahedron
";
			Assert.Throws<ParsingException>(() => ParseHelper.ParseGame(yaml));
		}
	}
}
