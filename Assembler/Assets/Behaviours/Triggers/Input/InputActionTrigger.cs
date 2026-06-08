using System;
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
	///   axis [Vector3]: For value actions, the current (x, y, 0) value of the action each frame.
	///   x [float]: For value actions, the current x component.
	///   y [float]: For value actions, the current y component.
	/// </remarks>
	public class InputActionTrigger : InputTrigger<InputActionTriggerData>
	{
		protected override void OnInitialise(InputActionTriggerData data)
		{
			Wire();
		}

		private void OnEnable()
		{
			// Here to show enabled checkbox
		}

		private void OnDestroy()
		{
			Unwire();
		}

		private void Wire()
		{
			// The action's enabled lifetime is owned by ControlsAssetOwner (the whole asset is enabled for the
			// game's lifetime), not by this behaviour — several behaviours can share one action, so enabling or
			// disabling it per-behaviour would let one consumer's teardown kill input for the others.
			if (Data.Kind is not ActionKind.Button)
			{
				return;
			}

			var action = Data.Action;

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

		private void Unwire()
		{
			// Initialise may never have run (component destroyed before the build's initialisation pass).
			if (Data?.Action is not { } action)
			{
				return;
			}

			action.started -= OnButtonEvent;
			action.canceled -= OnButtonEvent;
		}

		private void OnButtonEvent(InputAction.CallbackContext _)
		{
			if (isActiveAndEnabled)
			{
				NotifyListeners(TriggerContext.Empty);
			}
		}

		private void Update()
		{
			var action = Data.Action;

			if (Data.Kind is ActionKind.Value)
			{
				Emit(action.ReadValue<Vector2>());
				return;
			}

			// Button + hold: fire every frame the control is pressed (≡ KeyHoldTrigger). Down/up are event-driven.
			if (Data.Phase is ButtonPhase.Hold && action.IsPressed())
			{
				NotifyListeners(TriggerContext.Empty);
			}
		}

		// Exposed for unit testing: live device polling is impractical to drive in a unit test, so the
		// value-forwarding shape (axis/x/y, mirroring AxisTrigger) is verified through this seam.
		// The action reads a Vector2 from its binding; it widens to a Vector3 (z = 0) here.
		internal void Emit(Vector3 value) => NotifyListeners(BuildValueContext(value));

		internal static TriggerContext BuildValueContext(Vector3 value) =>
			TriggerContext.New(b =>
			{
				b["axis"] = value;
				b["x"] = value.x;
				b["y"] = value.y;
			});
	}
}
