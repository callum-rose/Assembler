using System.Collections.Generic;

namespace Assembler.Deserialisation.Dtos
{
	/// <summary>
	/// Raw deserialised <c>Controls</c> section: named abstract actions plus per-platform bindings that map
	/// physical inputs onto them. Lives in the leaf Deserialisation assembly because <see cref="GameDto"/>
	/// references it; the <c>Assembler.Input</c> assembly turns it into validated info types.
	/// </summary>
	public sealed record ControlsDto
	{
		public Dictionary<string, ActionDto>? Actions { get; init; }

		/// <summary>Platform key → action id → list of bindings (each a path string or a composite mapping).</summary>
		public Dictionary<string, Dictionary<string, List<BindingDto>>>? Bindings { get; init; }

		/// <summary>
		/// On-screen touch widgets (joystick/d-pad/button) rendered only when the active platform is Mobile.
		/// Each names an existing action; the control path is derived from that action's <c>mobile</c> binding.
		/// Plain mappings, so no custom type converter is needed.
		/// </summary>
		public List<OnScreenControlDto>? OnScreen { get; init; }
	}

	/// <summary>
	/// One on-screen touch widget declared under <c>Controls.OnScreen</c>. Drives an existing <see cref="Action"/>
	/// by synthesising input at the path that action is bound to under <c>mobile</c>; the widget kind
	/// (<see cref="Type"/>) and the action kind must agree (joystick/dpad ⇒ value, button ⇒ button).
	/// </summary>
	public sealed record OnScreenControlDto
	{
		public string? Type { get; init; }     // joystick | dpad | button
		public string? Action { get; init; }   // an action declared in Actions
		public string? Anchor { get; init; }   // TextAnchor name, e.g. lower-left
		public string? Label { get; init; }    // optional caption (button only)
		public VecDto? Offset { get; init; }    // px from the anchored corner (z ignored)
		public VecDto? Size { get; init; }      // widget size in px (z ignored)
	}

	/// <summary>A single declared action: its kind and (for buttons) which phase fires.</summary>
	public sealed record ActionDto
	{
		public string? Type { get; init; }       // button | value
		public string? Phase { get; init; }      // hold | down | up   (button only)
		public string? ValueType { get; init; }  // e.g. vector2        (value only)
	}

	/// <summary>
	/// One binding for an action. Read by <c>BindingTypeConverter</c>: a YAML scalar becomes a plain control
	/// <see cref="Path"/>; a YAML mapping with a <c>Composite</c> key becomes a composite plus its named
	/// <see cref="Parts"/> (e.g. Up/Down/Left/Right for a <c>2DVector</c>).
	/// </summary>
	public sealed record BindingDto
	{
		public string? Path { get; init; }
		public string? Composite { get; init; }
		public Dictionary<string, string>? Parts { get; init; }
	}
}
