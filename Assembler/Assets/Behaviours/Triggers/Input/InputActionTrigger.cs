using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assembler.Behaviours.Triggers.Input
{
	/// <summary>
	/// Relays an abstract input action (declared in the descriptor's <c>Controls</c> section and bound to a
	/// physical input per platform) to listeners. A drop-in replacement for the raw key triggers: a button action
	/// behaves like the key hold/down/up triggers depending on its phase, and a value action behaves like the axis
	/// trigger, emitting axis/x/y every frame.
	/// </summary>
	/// <remarks>
	/// Properties:
	///   Action: Name of the abstract action to listen for (must be declared under Controls.Actions).
	/// Outputs:
	///   axis [Vector2]: For value actions, the current (x, y) value of the action each frame.
	///   x [float]: For value actions, the current x component.
	///   y [float]: For value actions, the current y component.
	/// </remarks>
	public class InputActionTrigger : InputTrigger<InputActionTriggerData>
	{
		private bool _wired;

		// OnEnable runs on AddComponent (before Initialise sets Data), so the real wiring is deferred to here and
		// re-run by OnEnable only once Data exists.
		protected override void OnInitialise(InputActionTriggerData data)
		{
			if (isActiveAndEnabled)
			{
				Wire();
			}
		}

		private void OnEnable()
		{
			if (Data != null)
			{
				Wire();
			}
		}

		private void OnDisable()
		{
			Unwire();
		}

		private void Wire()
		{
			var action = Data.Action;

			if (action == null || _wired)
			{
				return;
			}

			action.Enable();

			if (Data.Kind == ActionKind.Button)
			{
				switch (Data.Phase)
				{
					case ButtonPhase.Down:
						action.started += OnButtonEvent;
						break;
					case ButtonPhase.Up:
						action.canceled += OnButtonEvent;
						break;
				}
			}

			_wired = true;
		}

		private void Unwire()
		{
			var action = Data?.Action;

			if (action == null || !_wired)
			{
				return;
			}

			action.started -= OnButtonEvent;
			action.canceled -= OnButtonEvent;
			action.Disable();

			_wired = false;
		}

		private void OnButtonEvent(InputAction.CallbackContext _) => NotifyListeners(TriggerContext.Empty);

		private void Update()
		{
			var action = Data?.Action;

			if (action == null)
			{
				return;
			}

			if (Data.Kind == ActionKind.Value)
			{
				Emit(action.ReadValue<Vector2>());
				return;
			}

			// Button + hold: fire every frame the control is pressed (≡ KeyHoldTrigger). Down/up are event-driven.
			if (Data.Phase == ButtonPhase.Hold && action.IsPressed())
			{
				NotifyListeners(TriggerContext.Empty);
			}
		}

		// Exposed for unit testing: live device polling is impractical to drive in a unit test, so the
		// value-forwarding shape (axis/x/y, mirroring AxisTrigger) is verified through this seam.
		internal void Emit(Vector2 value) => NotifyListeners(BuildValueContext(value));

		internal static TriggerContext BuildValueContext(Vector2 value) =>
			TriggerContext.Empty.With(b =>
			{
				b["axis"] = value;
				b["x"] = value.x;
				b["y"] = value.y;
			});
	}
}
