using System;
using UnityEngine;

namespace Assembler.Parsing.Info.Behaviours
{
	/// <summary>DOTween ease applied by the transform animations. Mirrors the names of
	/// <c>DG.Tweening.Ease</c> so the behaviour can map straight across.</summary>
	public enum Easing
	{
		Linear,
		InSine, OutSine, InOutSine,
		InQuad, OutQuad, InOutQuad,
		InCubic, OutCubic, InOutCubic,
		InQuart, OutQuart, InOutQuart,
		InQuint, OutQuint, InOutQuint,
		InExpo, OutExpo, InOutExpo,
		InCirc, OutCirc, InOutCirc,
		InElastic, OutElastic, InOutElastic,
		InBack, OutBack, InOutBack,
		InBounce, OutBounce, InOutBounce,
		Flash, InFlash, OutFlash, InOutFlash
	}

	/// <summary>How a <c>ui container</c> lays out its children. The layout directions
	/// (Vertical/Horizontal) add a uGUI layout group; None/Manual/Free add none.</summary>
	public enum LayoutDirection
	{
		Vertical,
		Horizontal,
		None,
		Manual,
		Free
	}

	/// <summary>The projection a <c>camera</c> uses.</summary>
	public enum CameraProjection
	{
		Perspective,
		Orthographic
	}

	/// <summary>Which rig a <c>camera follow</c> uses: a 2D screen-space framing rig or a 3D
	/// world-offset rig.</summary>
	public enum CameraFollowMode
	{
		TwoD,
		ThreeD
	}

	/// <summary>The kind of scene <c>light</c> to create. Maps onto the realtime subset of
	/// <c>UnityEngine.LightType</c> (the non-realtime Area/Disc types are intentionally excluded).</summary>
	public enum LightKind
	{
		Directional,
		Point,
		Spot
	}

	/// <summary>
	/// Parses the fixed-set "string enum" behaviour properties into concrete enums. Parsing is
	/// case-, space- and dash-insensitive and throws a <see cref="ParsingException"/> on an
	/// unrecognised value (rather than silently falling back), so typos surface at transform time.
	/// </summary>
	public static class BehaviourEnums
	{
		public static TEnum Parse<TEnum>(string raw) where TEnum : struct, Enum
		{
			var normalised = raw.Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("-", string.Empty);

			object parsed =
				typeof(TEnum) == typeof(Easing) ? ParseEasing(normalised, raw) :
				typeof(TEnum) == typeof(LayoutDirection) ? ParseLayoutDirection(normalised, raw) :
				typeof(TEnum) == typeof(PrimitiveType) ? ParsePrimitiveType(normalised, raw) :
				typeof(TEnum) == typeof(TextAnchor) ? ParseTextAnchor(normalised, raw) :
				typeof(TEnum) == typeof(CameraProjection) ? ParseCameraProjection(normalised, raw) :
				typeof(TEnum) == typeof(CameraFollowMode) ? ParseCameraFollowMode(normalised, raw) :
				typeof(TEnum) == typeof(LightKind) ? ParseLightKind(normalised, raw) :
				typeof(TEnum) == typeof(ButtonPhase) ? ParseButtonPhase(normalised, raw) :
				throw new ParsingException($"No enum parser registered for type '{typeof(TEnum)}'");

			return (TEnum)parsed;
		}

		private static Easing ParseEasing(string s, string raw) =>
			s switch
			{
				"linear" => Easing.Linear,
				"insine" => Easing.InSine,
				"outsine" => Easing.OutSine,
				"inoutsine" => Easing.InOutSine,
				"inquad" => Easing.InQuad,
				"outquad" => Easing.OutQuad,
				"inoutquad" => Easing.InOutQuad,
				"incubic" => Easing.InCubic,
				"outcubic" => Easing.OutCubic,
				"inoutcubic" => Easing.InOutCubic,
				"inquart" => Easing.InQuart,
				"outquart" => Easing.OutQuart,
				"inoutquart" => Easing.InOutQuart,
				"inquint" => Easing.InQuint,
				"outquint" => Easing.OutQuint,
				"inoutquint" => Easing.InOutQuint,
				"inexpo" => Easing.InExpo,
				"outexpo" => Easing.OutExpo,
				"inoutexpo" => Easing.InOutExpo,
				"incirc" => Easing.InCirc,
				"outcirc" => Easing.OutCirc,
				"inoutcirc" => Easing.InOutCirc,
				"inelastic" => Easing.InElastic,
				"outelastic" => Easing.OutElastic,
				"inoutelastic" => Easing.InOutElastic,
				"inback" => Easing.InBack,
				"outback" => Easing.OutBack,
				"inoutback" => Easing.InOutBack,
				"inbounce" => Easing.InBounce,
				"outbounce" => Easing.OutBounce,
				"inoutbounce" => Easing.InOutBounce,
				"flash" => Easing.Flash,
				"inflash" => Easing.InFlash,
				"outflash" => Easing.OutFlash,
				"inoutflash" => Easing.InOutFlash,
				_ => throw new ParsingException(
					$"Unknown easing '{raw}'. Valid values: linear, inSine, outSine, inOutSine, inQuad, outQuad, " +
					"inOutQuad, inCubic, outCubic, inOutCubic, inQuart, outQuart, inOutQuart, inQuint, outQuint, " +
					"inOutQuint, inExpo, outExpo, inOutExpo, inCirc, outCirc, inOutCirc, inElastic, outElastic, " +
					"inOutElastic, inBack, outBack, inOutBack, inBounce, outBounce, inOutBounce, flash, inFlash, " +
					"outFlash, inOutFlash")
			};

		private static LayoutDirection ParseLayoutDirection(string s, string raw) =>
			s switch
			{
				"vertical" => LayoutDirection.Vertical,
				"horizontal" => LayoutDirection.Horizontal,
				"none" => LayoutDirection.None,
				"manual" => LayoutDirection.Manual,
				"free" => LayoutDirection.Free,
				_ => throw new ParsingException(
					$"Unknown layout direction '{raw}'. Valid values: vertical, horizontal, none, manual, free")
			};

		private static PrimitiveType ParsePrimitiveType(string s, string raw) =>
			s switch
			{
				"cube" => PrimitiveType.Cube,
				"sphere" => PrimitiveType.Sphere,
				"capsule" => PrimitiveType.Capsule,
				"cylinder" => PrimitiveType.Cylinder,
				"plane" => PrimitiveType.Plane,
				"quad" => PrimitiveType.Quad,
				_ => throw new ParsingException(
					$"Unknown primitive shape '{raw}'. Valid values: cube, sphere, capsule, cylinder, plane, quad")
			};

		// Aliases ported from the former UiLayout.ParseAlignment; dashes are stripped before matching,
		// so "upper-left" arrives as "upperleft".
		private static TextAnchor ParseTextAnchor(string s, string raw) =>
			s switch
			{
				"upperleft" or "topleft" => TextAnchor.UpperLeft,
				"uppercenter" or "topcenter" or "top" => TextAnchor.UpperCenter,
				"upperright" or "topright" => TextAnchor.UpperRight,
				"middleleft" or "left" => TextAnchor.MiddleLeft,
				"middlecenter" or "center" or "centre" => TextAnchor.MiddleCenter,
				"middleright" or "right" => TextAnchor.MiddleRight,
				"lowerleft" or "bottomleft" => TextAnchor.LowerLeft,
				"lowercenter" or "bottomcenter" or "bottom" => TextAnchor.LowerCenter,
				"lowerright" or "bottomright" => TextAnchor.LowerRight,
				_ => throw new ParsingException(
					$"Unknown alignment '{raw}'. Valid values: upper-left, upper-center (top), upper-right, " +
					"middle-left (left), middle-center (center), middle-right (right), lower-left, " +
					"lower-center (bottom), lower-right")
			};

		private static CameraProjection ParseCameraProjection(string s, string raw) =>
			s switch
			{
				"perspective" => CameraProjection.Perspective,
				"orthographic" => CameraProjection.Orthographic,
				_ => throw new ParsingException(
					$"Unknown camera view '{raw}'. Valid values: perspective, orthographic")
			};

		private static CameraFollowMode ParseCameraFollowMode(string s, string raw) =>
			s switch
			{
				"2d" => CameraFollowMode.TwoD,
				"3d" => CameraFollowMode.ThreeD,
				_ => throw new ParsingException(
					$"Unknown camera follow mode '{raw}'. Valid values: 2d, 3d")
			};

		private static LightKind ParseLightKind(string s, string raw) =>
			s switch
			{
				"directional" or "sun" => LightKind.Directional,
				"point" => LightKind.Point,
				"spot" => LightKind.Spot,
				_ => throw new ParsingException(
					$"Unknown light type '{raw}'. Valid values: directional, point, spot")
			};

		private static ButtonPhase ParseButtonPhase(string s, string raw) =>
			s switch
			{
				"hold" => ButtonPhase.Hold,
				"down" => ButtonPhase.Down,
				"up" => ButtonPhase.Up,
				_ => throw new ParsingException(
					$"Unknown button phase '{raw}'. Valid values: hold, down, up")
			};
	}
}
