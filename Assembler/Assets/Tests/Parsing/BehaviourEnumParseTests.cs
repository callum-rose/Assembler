using Assembler.Deserialisation;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Parsing
{
	public class BehaviourEnumParseTests
	{
		private static GameInfo Parse(string yaml) =>
			Transformer.Transform(new GameFileParser().Parse(yaml));

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

		private const string AnimationYaml = @"
Entities:
  e:
    Behaviours:
      anim:
        Type: move animation
        Properties:
          End: !vec { X: 1, Y: 0, Z: 0 }
          Duration: 1
";

		[Test]
		public void OmittedEnumPropertyTakesItsDefault()
		{
			var anim = (MoveAnimationInfo)Parse(AnimationYaml).Entities[0].Behaviours[0];

			Assert.AreEqual(Easing.InOutSine, ((ConstantSource<Easing>)anim.Easing).Value);
		}

		[Test]
		public void PresentEnumLiteralParsesToItsMember()
		{
			var yaml = @"
Entities:
  e:
    Behaviours:
      anim:
        Type: move animation
        Properties:
          End: !vec { X: 1, Y: 0, Z: 0 }
          Duration: 1
          Easing: outBack
";
			var anim = (MoveAnimationInfo)Parse(yaml).Entities[0].Behaviours[0];

			Assert.AreEqual(Easing.OutBack, ((ConstantSource<Easing>)anim.Easing).Value);
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
			Assert.Throws<ParsingException>(() => Parse(yaml));
		}
	}
}
