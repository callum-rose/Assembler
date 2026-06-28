using System;
using Assembler.Parsing.Controls;
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

	/// <summary>Which collider dimension a <c>camera confiner</c> clamps against: a 2D
	/// <c>Collider2D</c> boundary (<c>CinemachineConfiner2D</c>) or a 3D <c>Collider</c> volume
	/// (<c>CinemachineConfiner3D</c>).</summary>
	public enum CameraConfinerMode
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

	/// <summary>Which transform property one <c>animation</c> step tweens. <c>Wait</c> is a pure delay
	/// (an <c>AppendInterval</c>) that animates nothing.</summary>
	public enum AnimationTarget
	{
		Move,
		Rotate,
		Scale,
		Wait
	}

	/// <summary>How an <c>animation</c> step is placed in the DOTween sequence relative to the previous step:
	/// <c>Append</c> after it, <c>Join</c> alongside the previously appended step, or <c>Insert</c> at an
	/// absolute time (<c>At</c>). Mirrors DOTween's <c>Append</c>/<c>Join</c>/<c>Insert</c>.</summary>
	public enum SequenceMode
	{
		Append,
		Join,
		Insert
	}

	/// <summary>How an <c>animation</c> sequence repeats when <c>Loops</c> ≠ 1. Mirrors the realtime subset of
	/// <c>DG.Tweening.LoopType</c>.</summary>
	public enum SequenceLoopType
	{
		Restart,
		Yoyo,
		Incremental
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
				typeof(TEnum) == typeof(CameraConfinerMode) ? ParseCameraConfinerMode(normalised, raw) :
				typeof(TEnum) == typeof(LightKind) ? ParseLightKind(normalised, raw) :
				typeof(TEnum) == typeof(AnimationTarget) ? ParseAnimationTarget(normalised, raw) :
				typeof(TEnum) == typeof(SequenceMode) ? ParseSequenceMode(normalised, raw) :
				typeof(TEnum) == typeof(SequenceLoopType) ? ParseSequenceLoopType(normalised, raw) :
				typeof(TEnum) == typeof(ButtonPhase) ? ParseButtonPhase(normalised, raw) :
				typeof(TEnum) == typeof(OnScreenControlKind) ? ParseOnScreenControlKind(normalised, raw) :
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

		private static CameraConfinerMode ParseCameraConfinerMode(string s, string raw) =>
			s switch
			{
				"2d" => CameraConfinerMode.TwoD,
				"3d" => CameraConfinerMode.ThreeD,
				_ => throw new ParsingException(
					$"Unknown camera confiner mode '{raw}'. Valid values: 2d, 3d")
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

		private static AnimationTarget ParseAnimationTarget(string s, string raw) =>
			s switch
			{
				"move" or "position" => AnimationTarget.Move,
				"rotate" or "rotation" => AnimationTarget.Rotate,
				"scale" => AnimationTarget.Scale,
				"wait" or "delay" or "pause" => AnimationTarget.Wait,
				_ => throw new ParsingException(
					$"Unknown animation target '{raw}'. Valid values: move, rotate, scale, wait")
			};

		private static SequenceMode ParseSequenceMode(string s, string raw) =>
			s switch
			{
				"append" => SequenceMode.Append,
				"join" => SequenceMode.Join,
				"insert" => SequenceMode.Insert,
				_ => throw new ParsingException(
					$"Unknown animation step mode '{raw}'. Valid values: append, join, insert")
			};

		private static SequenceLoopType ParseSequenceLoopType(string s, string raw) =>
			s switch
			{
				"restart" => SequenceLoopType.Restart,
				"yoyo" => SequenceLoopType.Yoyo,
				"incremental" => SequenceLoopType.Incremental,
				_ => throw new ParsingException(
					$"Unknown animation loop type '{raw}'. Valid values: restart, yoyo, incremental")
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

		private static OnScreenControlKind ParseOnScreenControlKind(string s, string raw) =>
			s switch
			{
				"joystick" or "stick" => OnScreenControlKind.Joystick,
				"dpad" or "directionalpad" => OnScreenControlKind.DPad,
				"button" => OnScreenControlKind.Button,
				_ => throw new ParsingException(
					$"Unknown on-screen control type '{raw}'. Valid values: joystick, dpad, button")
			};
	}
}
