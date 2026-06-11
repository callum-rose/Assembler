using System;
using System.Collections.Generic;
using Assembler.Deserialisation;
using Assembler.Input;
using Assembler.Parsing.Controls;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Input
{
	public class OnScreenControlsTests
	{
		private const string Yaml = @"
Controls:
  Actions:
    move: { Type: value, ValueType: vector2 }
    fire: { Type: button, Phase: down }
  Bindings:
    mobile:
      move: [ ""<Gamepad>/leftStick"" ]
      fire: [ ""<Gamepad>/buttonSouth"" ]
  OnScreen:
    - Type: joystick
      Action: move
      Anchor: lower-left
      Offset: { X: 220, Y: 220 }
      Size:   { X: 320, Y: 320 }
    - Type: button
      Action: fire
      Label: Fire
      Anchor: lower-right
";

		private static ControlsInfo Transform()
		{
			var dto = new GameFileParser().Parse(Yaml);
			return ControlsTransformer.Transform(dto.Controls);
		}

		// --- Transformer ---------------------------------------------------------------------------------

		[Test]
		public void ParsesOnScreenControls()
		{
			var controls = Transform();

			Assert.AreEqual(2, controls.OnScreen.Count);

			var joystick = controls.OnScreen[0];
			Assert.AreEqual(OnScreenControlKind.Joystick, joystick.Kind);
			Assert.AreEqual("move", joystick.Action);
			Assert.AreEqual(TextAnchor.LowerLeft, joystick.Anchor);
			Assert.AreEqual(new Vector3(220f, 220f, 0f), joystick.Offset);
			Assert.AreEqual(new Vector3(320f, 320f, 0f), joystick.Size);

			var button = controls.OnScreen[1];
			Assert.AreEqual(OnScreenControlKind.Button, button.Kind);
			Assert.AreEqual("fire", button.Action);
			Assert.AreEqual(TextAnchor.LowerRight, button.Anchor);
			Assert.AreEqual("Fire", button.Label);
		}

		[Test]
		public void DefaultsAnchorAndSizeWhenOmitted()
		{
			var controls = Transform();
			var button = controls.OnScreen[1];

			// Size omitted → default 200x200; Offset omitted → zero.
			Assert.AreEqual(new Vector3(200f, 200f, 0f), button.Size);
			Assert.AreEqual(Vector3.zero, button.Offset);
		}

		// --- Validator -----------------------------------------------------------------------------------

		[Test]
		public void Validator_Passes_ForWellFormedControls() =>
			Assert.DoesNotThrow(() => OnScreenControlsValidator.Validate(Transform()));

		[Test]
		public void Validator_Throws_WhenActionUndeclared()
		{
			var controls = ControlsWith(
				actions: new Dictionary<string, ActionInfo>(),
				mobile: Bindings(("ghost", BindingInfo.Simple("<Gamepad>/leftStick"))),
				onScreen: Joystick("ghost"));

			Assert.Throws<InvalidOperationException>(() => OnScreenControlsValidator.Validate(controls));
		}

		[Test]
		public void Validator_Throws_WhenNoMobileBinding()
		{
			var controls = ControlsWith(
				actions: ValueAction("move"),
				mobile: new Dictionary<string, IReadOnlyList<BindingInfo>>(),
				onScreen: Joystick("move"));

			Assert.Throws<InvalidOperationException>(() => OnScreenControlsValidator.Validate(controls));
		}

		[Test]
		public void Validator_Throws_WhenBindingIsComposite()
		{
			var composite = BindingInfo.CompositeOf("2DVector", new Dictionary<string, string>
			{
				["Up"] = "<Keyboard>/w"
			});

			var controls = ControlsWith(
				actions: ValueAction("move"),
				mobile: Bindings(("move", composite)),
				onScreen: Joystick("move"));

			Assert.Throws<InvalidOperationException>(() => OnScreenControlsValidator.Validate(controls));
		}

		[Test]
		public void Validator_Throws_WhenKindMismatchesActionKind()
		{
			// A joystick (requires a value action) pointed at a button action.
			var controls = ControlsWith(
				actions: new Dictionary<string, ActionInfo>
				{
					["fire"] = new("fire", ActionKind.Button, ButtonPhase.Down, null)
				},
				mobile: Bindings(("fire", BindingInfo.Simple("<Gamepad>/buttonSouth"))),
				onScreen: Joystick("fire"));

			Assert.Throws<InvalidOperationException>(() => OnScreenControlsValidator.Validate(controls));
		}

		// --- Path derivation -----------------------------------------------------------------------------

		[Test]
		public void TryResolvePath_ReturnsBoundMobilePath()
		{
			var controls = Transform();

			Assert.IsTrue(OnScreenControlPaths.TryResolvePath(controls, controls.OnScreen[0], out var movePath));
			Assert.AreEqual("<Gamepad>/leftStick", movePath);

			Assert.IsTrue(OnScreenControlPaths.TryResolvePath(controls, controls.OnScreen[1], out var firePath));
			Assert.AreEqual("<Gamepad>/buttonSouth", firePath);
		}

		[Test]
		public void TryResolvePath_FailsWithoutMobileBinding()
		{
			var controls = ControlsWith(
				actions: ValueAction("move"),
				mobile: new Dictionary<string, IReadOnlyList<BindingInfo>>(),
				onScreen: Joystick("move"));

			Assert.IsFalse(OnScreenControlPaths.TryResolvePath(controls, controls.OnScreen[0], out _));
		}

		// --- Helpers -------------------------------------------------------------------------------------

		private static Dictionary<string, ActionInfo> ValueAction(string name) => new()
		{
			[name] = new(name, ActionKind.Value, ButtonPhase.Hold, "vector2")
		};

		private static Dictionary<string, IReadOnlyList<BindingInfo>> Bindings(params (string action, BindingInfo binding)[] entries)
		{
			var map = new Dictionary<string, IReadOnlyList<BindingInfo>>();
			foreach (var (action, binding) in entries)
			{
				map[action] = new[] { binding };
			}

			return map;
		}

		private static List<OnScreenControlInfo> Joystick(string action) => new()
		{
			new OnScreenControlInfo(OnScreenControlKind.Joystick, action, TextAnchor.LowerLeft, Vector3.zero, Vector3.one, null)
		};

		private static ControlsInfo ControlsWith(
			Dictionary<string, ActionInfo> actions,
			Dictionary<string, IReadOnlyList<BindingInfo>> mobile,
			List<OnScreenControlInfo> onScreen)
		{
			var bindings = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<BindingInfo>>>
			{
				["mobile"] = mobile
			};

			return new ControlsInfo(actions, bindings, onScreen);
		}
	}
}
