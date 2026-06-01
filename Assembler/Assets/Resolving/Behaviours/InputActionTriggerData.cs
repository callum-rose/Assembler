using Assembler.Parsing.Info.Behaviours;
using UnityEngine.InputSystem;

namespace Assembler.Resolving.Behaviours
{
	/// <summary>
	/// Resolved data for the <c>input action</c> trigger. Carries the resolved action name, its semantic kind and
	/// button phase, and the live Unity <see cref="InputAction"/> built from the descriptor's controls for the
	/// active platform. <see cref="Action"/> is nullable so unit tests can exercise the notify path without a
	/// device-backed action.
	/// </summary>
	public sealed class InputActionTriggerData : TriggerData
	{
		public string ActionName { get; }
		public ActionKind Kind { get; }
		public ButtonPhase Phase { get; }
		public InputAction Action { get; }

		public InputActionTriggerData(string id, string actionName, ActionKind kind, ButtonPhase phase, InputAction action)
			: base(id)
		{
			ActionName = actionName;
			Kind = kind;
			Phase = phase;
			Action = action;
		}
	}
}
