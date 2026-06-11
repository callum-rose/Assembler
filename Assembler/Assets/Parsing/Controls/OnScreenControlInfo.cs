using UnityEngine;

namespace Assembler.Parsing.Controls
{
	/// <summary>Which on-screen widget a <see cref="OnScreenControlInfo"/> renders.</summary>
	public enum OnScreenControlKind
	{
		/// <summary>An analogue thumbstick, driving a <c>value</c>/<c>vector2</c> action.</summary>
		Joystick,

		/// <summary>A four-way directional pad, driving a <c>value</c>/<c>vector2</c> action.</summary>
		DPad,

		/// <summary>A single press button, driving a <c>button</c> action.</summary>
		Button
	}

	/// <summary>
	/// The parsed, validated form of one <c>Controls.OnScreen</c> entry: which on-screen widget to render, the
	/// action it drives, and where to place it. The control path the widget synthesises into is not stored here —
	/// it's derived from the action's <c>mobile</c> binding at build time (see <c>OnScreenControlPaths</c>).
	/// </summary>
	/// <param name="Kind">The widget to render (joystick/d-pad/button).</param>
	/// <param name="Action">The action id the widget drives; must be declared under <c>Controls.Actions</c>.</param>
	/// <param name="Anchor">Which screen corner/edge the widget is positioned relative to.</param>
	/// <param name="Offset">Pixel offset inward from the anchored corner (z ignored).</param>
	/// <param name="Size">Widget size in pixels (z ignored).</param>
	/// <param name="Label">Optional caption (button only). Null when unspecified.</param>
	public sealed record OnScreenControlInfo(
		OnScreenControlKind Kind,
		string Action,
		TextAnchor Anchor,
		Vector3 Offset,
		Vector3 Size,
		string? Label);
}
