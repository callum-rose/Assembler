namespace Assembler.Parsing.Info.Behaviours
{
	/// <summary>
	/// What an abstract input action produces. Shared between the controls layer (<c>Assembler.Input</c>),
	/// the resolved <c>InputActionTriggerData</c>, and the relay MonoBehaviour, so it lives in the neutral
	/// parsing assembly that all of them reference.
	/// </summary>
	public enum ActionKind
	{
		/// <summary>A digital button: fires on press/release/hold (see <see cref="ButtonPhase"/>).</summary>
		Button,

		/// <summary>An analogue value (e.g. a stick or keyboard composite): emits axis/x/y every frame.</summary>
		Value
	}

	/// <summary>When a <see cref="ActionKind.Button"/> action fires. Ignored for value actions.</summary>
	public enum ButtonPhase
	{
		/// <summary>Fires every frame the control is held.</summary>
		Hold,

		/// <summary>Fires once on the frame the control is pressed.</summary>
		Down,

		/// <summary>Fires once on the frame the control is released.</summary>
		Up
	}
}
