using Assembler.Parsing.Info.Behaviours;

namespace Assembler.Input
{
	/// <summary>
	/// A single abstract input action declared under <c>Controls.Actions</c>. Carries only its semantic shape
	/// (button vs value, and — for buttons — which phase fires); the physical inputs that feed it live in
	/// <see cref="BindingInfo"/> grouped by platform.
	/// </summary>
	/// <param name="Name">The action id, e.g. <c>move-left-up</c>.</param>
	/// <param name="Kind">Whether the action is a button or an analogue value.</param>
	/// <param name="Phase">For button actions, when it fires (hold/down/up). Ignored for value actions.</param>
	/// <param name="ValueType">For value actions, the value shape (e.g. <c>vector2</c>). Null for buttons.</param>
	public sealed record ActionInfo(string Name, ActionKind Kind, ButtonPhase Phase, string? ValueType);
}
