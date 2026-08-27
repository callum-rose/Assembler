using UnityEngine;

namespace Assembler.Shell.Theming
{
	/// <summary>
	/// One named colour slot of the shell palette. Every graphic in the shell paints from a role rather than a
	/// literal colour, so a second <see cref="ShellTheme"/> asset (dark mode) re-skins the app without touching a
	/// prefab.
	/// </summary>
	/// <remarks>
	/// A role is an asset, not an enum member — see <see cref="ScriptableEnum"/> for why. The palette's members
	/// live under <c>Assets/Shell/Theming/Roles</c> and are authored by <c>Assembler &gt; Shell &gt; Create Shell
	/// Assets</c>; a new role is a new asset plus a row on the theme, with no code change and no number for a
	/// prefab to have already serialised.
	/// </remarks>
	[CreateAssetMenu(fileName = "NewColourRole", menuName = "Assembler/Shell/Colour Role")]
	public sealed class ColorRole : ScriptableEnum
	{
	}
}
