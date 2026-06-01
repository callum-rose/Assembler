using System.Collections.Generic;
using System.Linq;

namespace Assembler.Input
{
	/// <summary>
	/// Maps a resolved <see cref="InputPlatform"/> to the binding-group key that should actually be consulted,
	/// walking a fallback chain when the game declares no bindings for the exact platform
	/// (<c>mobile → desktop</c>, <c>console → gamepad → desktop</c>, and finally <c>desktop</c>). The chain only
	/// chooses <em>which</em> group is masked in; <see cref="ControlsValidator"/> still hard-fails if the chosen
	/// group leaves any used action unbound.
	/// </summary>
	public static class PlatformFallback
	{
		public static IReadOnlyList<string> Chain(InputPlatform platform) => platform switch
		{
			InputPlatform.Mobile => new[] { "mobile", "desktop" },
			InputPlatform.Console => new[] { "console", "gamepad", "desktop" },
			InputPlatform.Gamepad => new[] { "gamepad", "desktop" },
			_ => new[] { "desktop" }
		};

		/// <summary>
		/// The binding group to mask in for <paramref name="platform"/>: the first group in the fallback chain
		/// for which the game declares any bindings, or the chain's final entry (always <c>desktop</c>) when none
		/// match — leaving validation to surface the unbound actions.
		/// </summary>
		public static string ResolveGroup(InputPlatform platform, ControlsInfo controls)
		{
			var chain = Chain(platform);
			return chain.FirstOrDefault(controls.Bindings.ContainsKey) ?? chain[chain.Count - 1];
		}
	}
}
