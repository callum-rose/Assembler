using System.Collections.Generic;
using System.Linq;
using Assembler.Input;
using Assembler.Parsing.Controls;
using Assembler.Parsing.Info.Behaviours;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Tests.Input
{
	public class InputActionBuilderTests
	{
		private static ControlsInfo SampleControls()
		{
			var actions = new Dictionary<string, ActionInfo>
			{
				["move-up"] = new("move-up", ActionKind.Button, ButtonPhase.Hold, null),
				["aim"] = new("aim", ActionKind.Value, ButtonPhase.Hold, "vector2")
			};

			var desktop = new Dictionary<string, IReadOnlyList<BindingInfo>>
			{
				["move-up"] = new[] { BindingInfo.Simple("<Keyboard>/w") },
				["aim"] = new[]
				{
					BindingInfo.CompositeOf("2DVector", new Dictionary<string, string>
					{
						["Up"] = "<Keyboard>/w",
						["Down"] = "<Keyboard>/s",
						["Left"] = "<Keyboard>/a",
						["Right"] = "<Keyboard>/d"
					})
				}
			};

			var gamepad = new Dictionary<string, IReadOnlyList<BindingInfo>>
			{
				["move-up"] = new[] { BindingInfo.Simple("<Gamepad>/buttonSouth") }
			};

			var bindings = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<BindingInfo>>>
			{
				["desktop"] = desktop,
				["gamepad"] = gamepad
			};

			return new ControlsInfo(actions, bindings);
		}

		[Test]
		public void BuildsActionsWithCorrectTypes()
		{
			var asset = InputActionBuilder.Build(SampleControls(), "desktop");

			try
			{
				Assert.IsNotNull(asset.FindActionMap(InputActionBuilder.MapName));
				Assert.AreEqual(InputActionType.Button, asset.FindAction("move-up").type);
				Assert.AreEqual(InputActionType.Value, asset.FindAction("aim").type);
			}
			finally
			{
				Object.DestroyImmediate(asset);
			}
		}

		[Test]
		public void TagsBindingsWithPlatformGroup()
		{
			var asset = InputActionBuilder.Build(SampleControls(), "desktop");

			try
			{
				var moveUp = asset.FindAction("move-up");

				// move-up is bound on both desktop and gamepad.
				Assert.AreEqual(2, moveUp.bindings.Count);

				var desktopBinding = moveUp.bindings.First(b => b.groups == "desktop");
				Assert.AreEqual("<Keyboard>/w", desktopBinding.path);

				var gamepadBinding = moveUp.bindings.First(b => b.groups == "gamepad");
				Assert.AreEqual("<Gamepad>/buttonSouth", gamepadBinding.path);
			}
			finally
			{
				Object.DestroyImmediate(asset);
			}
		}

		[Test]
		public void MasksToActivePlatformGroup()
		{
			var desktopAsset = InputActionBuilder.Build(SampleControls(), "desktop");
			var gamepadAsset = InputActionBuilder.Build(SampleControls(), "gamepad");

			try
			{
				var desktopMask = desktopAsset.FindActionMap(InputActionBuilder.MapName).bindingMask;
				var gamepadMask = gamepadAsset.FindActionMap(InputActionBuilder.MapName).bindingMask;

				Assert.IsTrue(desktopMask.HasValue);
				Assert.AreEqual("desktop", desktopMask!.Value.groups);

				Assert.IsTrue(gamepadMask.HasValue);
				Assert.AreEqual("gamepad", gamepadMask!.Value.groups);
			}
			finally
			{
				Object.DestroyImmediate(desktopAsset);
				Object.DestroyImmediate(gamepadAsset);
			}
		}

		[Test]
		public void AddsCompositeBindingParts()
		{
			var asset = InputActionBuilder.Build(SampleControls(), "desktop");

			try
			{
				var aim = asset.FindAction("aim");

				// One composite head + four parts.
				Assert.AreEqual(5, aim.bindings.Count);
				Assert.IsTrue(aim.bindings.Any(b => b.isComposite));
				Assert.IsTrue(aim.bindings.Any(b => b.path == "<Keyboard>/w" && b.groups == "desktop"));
			}
			finally
			{
				Object.DestroyImmediate(asset);
			}
		}
	}
}
