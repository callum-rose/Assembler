using Assembler.Parsing.Info.Behaviours;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assembler.Input
{
	/// <summary>
	/// Turns a <see cref="ControlsInfo"/> plus the active platform group into a live Unity
	/// <see cref="InputActionAsset"/>: one action map, one <see cref="InputAction"/> per declared action, every
	/// binding tagged with its platform as the binding group, and a <see cref="InputActionMap.bindingMask"/> set
	/// to the active group so only that platform's bindings are live. Each platform group is also registered as an
	/// <see cref="InputControlScheme"/> so the asset can be inspected/extended like an authored one.
	/// </summary>
	public static class InputActionBuilder
	{
		public const string MapName = "gameplay";

		public static InputActionAsset Build(ControlsInfo controls, string activePlatform)
		{
			var asset = ScriptableObject.CreateInstance<InputActionAsset>();
			asset.name = "GameplayControls";

			// One control scheme per platform group, so MaskByGroup has matching schemes to consult.
			foreach (var platform in controls.Bindings.Keys)
			{
				asset.AddControlScheme(platform);
			}

			var map = asset.AddActionMap(MapName);

			foreach (var (name, action) in controls.Actions)
			{
				var inputAction = action.Kind == ActionKind.Value
					? map.AddAction(name, InputActionType.Value, expectedControlLayout: "Vector2")
					: map.AddAction(name, InputActionType.Button);

				AddBindings(controls, name, inputAction);
			}

			// Only the active platform's bindings should fire; everything else stays inert.
			map.bindingMask = InputBinding.MaskByGroup(activePlatform);

			return asset;
		}

		private static void AddBindings(ControlsInfo controls, string actionName, InputAction inputAction)
		{
			foreach (var (platform, byAction) in controls.Bindings)
			{
				if (!byAction.TryGetValue(actionName, out var bindings))
				{
					continue;
				}

				foreach (var binding in bindings)
				{
					if (binding.Composite != null)
					{
						var composite = inputAction.AddCompositeBinding(binding.Composite);

						foreach (var (partName, path) in binding.Parts)
						{
							composite = composite.With(partName, path, groups: platform);
						}
					}
					else if (binding.Path != null)
					{
						inputAction.AddBinding(binding.Path, groups: platform);
					}
				}
			}
		}
	}
}
