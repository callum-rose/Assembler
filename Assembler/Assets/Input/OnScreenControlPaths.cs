using Assembler.Parsing.Controls;

namespace Assembler.Input
{
	/// <summary>
	/// Derives the Input System control path an on-screen widget should synthesise into, from the action's
	/// <c>mobile</c> binding. The widget is just another physical input source feeding the same action, so its
	/// path is whatever the <c>mobile</c> group binds that action to. Shared by <see cref="OnScreenControlsValidator"/>
	/// (which turns a failure here into a descriptive error) and the build-time overlay renderer, so the
	/// single-simple-path rule lives in one place.
	/// </summary>
	public static class OnScreenControlPaths
	{
		/// <summary>The platform group on-screen widgets always derive their path from.</summary>
		public const string MobileGroup = "mobile";

		/// <summary>
		/// Resolves the single simple <c>mobile</c> control path for <paramref name="control"/>'s action.
		/// Returns false when the action has no <c>mobile</c> binding, or that binding is a composite / not a
		/// single simple path.
		/// </summary>
		public static bool TryResolvePath(ControlsInfo controls, OnScreenControlInfo control, out string path)
		{
			path = string.Empty;

			if (!controls.Bindings.TryGetValue(MobileGroup, out var byAction)
				|| !byAction.TryGetValue(control.Action, out var bindings)
				|| bindings.Count != 1)
			{
				return false;
			}

			var binding = bindings[0];
			if (binding.IsComposite || string.IsNullOrEmpty(binding.Path))
			{
				return false;
			}

			path = binding.Path!;
			return true;
		}
	}
}
