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
	/// physical input per platform) to listeners. This is the single input-event source for gameplay: a button
	/// action fires on its phase (hold ⇒ every frame held, down ⇒ on press, up ⇒ on release), and a value action
	/// emits axis/x/y every frame. Physical keys, mouse buttons, mouse position/scroll, and gamepad controls are
	/// all expressed as bindings on the action rather than as separate trigger types.
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
		// Set by the started/canceled callback (which fires in the Input System's own player-loop step, before the
		// clock ticks) and flushed in Update so the emission is tagged with — and replayed on — the same frame it
		// actually affects gameplay. Emitting straight from the callback would tag it one tick early.
		private bool _pendingButtonEmit;

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

		// started/canceled fire before the clock ticks, so defer the emission to Update (see _pendingButtonEmit)
		// rather than notifying here — tag-and-replay on the frame it lands, not the previous one.
		private void OnButtonEvent(InputAction.CallbackContext _)
		{
			if (isActiveAndEnabled && !IsReplaying)
			{
				_pendingButtonEmit = true;
			}
		}

		private void Update()
		{
			// While replaying, don't read the live action at all; ReplayDriver re-emits the captured contexts. Not
			// required for correctness (NotifyListeners suppresses during replay) — an optimisation that also skips
			// the per-frame value-context allocation.
			if (IsReplaying)
			{
				return;
			}

			var action = Data.Action;

			if (Data.Kind is ActionKind.Value)
			{
				Emit(action.ReadValue<Vector2>());
				return;
			}

			// Button down/up: flush the callback-deferred emission on this frame. Hold: fire every frame pressed.
			if (Data.Phase is ButtonPhase.Hold)
			{
				if (action.IsPressed())
				{
					NotifyListeners(TriggerContext.Empty);
				}
			}
			else if (_pendingButtonEmit)
			{
				_pendingButtonEmit = false;
				NotifyListeners(TriggerContext.Empty);
			}
		}

		// Exposed for unit testing: live device polling is impractical to drive in a unit test, so the
		// value-forwarding shape (axis/x/y) is verified through this seam.
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
