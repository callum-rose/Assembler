using System;
using Assembler.Parsing.Controls;
using Assembler.Parsing.Info.Behaviours;

namespace Assembler.Input
{
	/// <summary>
	/// Hard-fail gate for the descriptor's <c>Controls.OnScreen</c> widgets, run from <c>Builder.Resolve</c>
	/// alongside <see cref="ControlsValidator"/>. Mirrors the "never ship silently-dead input" philosophy: a
	/// declared on-screen widget that can't resolve to a live control path — undeclared action, missing
	/// <c>mobile</c> binding, a composite/multi-path binding where a single simple path is required, or a
	/// widget/action kind mismatch — is a build error, not a no-op at runtime.
	/// </summary>
	public static class OnScreenControlsValidator
	{
		public static void Validate(ControlsInfo controls)
		{
			foreach (var control in controls.OnScreen)
			{
				if (!controls.Actions.TryGetValue(control.Action, out var action))
				{
					throw new InvalidOperationException(
						$"On-screen {control.Kind} references action '{control.Action}', which is not declared in " +
						"the descriptor's Controls.Actions section.");
				}

				if (!controls.Bindings.TryGetValue(OnScreenControlPaths.MobileGroup, out var mobile)
					|| !mobile.TryGetValue(control.Action, out var bindings)
					|| bindings.Count == 0)
				{
					throw new InvalidOperationException(
						$"On-screen {control.Kind} for action '{control.Action}' has no 'mobile' binding. " +
						"Add it under Controls.Bindings.mobile so the widget has a control path to drive.");
				}

				if (bindings.Count != 1 || bindings[0].IsComposite || string.IsNullOrEmpty(bindings[0].Path))
				{
					throw new InvalidOperationException(
						$"On-screen {control.Kind} for action '{control.Action}' requires its 'mobile' binding to be " +
						"a single simple control path (e.g. \"<Gamepad>/leftStick\"), not a composite or multiple bindings.");
				}

				var expectedKind = control.Kind == OnScreenControlKind.Button ? ActionKind.Button : ActionKind.Value;
				if (action.Kind != expectedKind)
				{
					throw new InvalidOperationException(
						$"On-screen {control.Kind} for action '{control.Action}' requires a '{expectedKind}' action, " +
						$"but '{control.Action}' is declared as '{action.Kind}'.");
				}
			}
		}
	}
}
