using UnityEngine;

namespace Assembler.Shell.Theming
{
	/// <summary>
	/// One named text style of the shell. A label picks a style, never a font/size/tracking triple, so the
	/// typographic scale lives in one asset.
	/// </summary>
	/// <remarks>
	/// As with <see cref="ColorRole"/> a style is an asset rather than an enum member — see
	/// <see cref="ScriptableEnum"/>. The members live under <c>Assets/Shell/Theming/TextStyles</c>; what each one
	/// resolves to (font, size, case, tracking, leading, colour role) is authored on the <see cref="ShellTheme"/>.
	/// </remarks>
	[CreateAssetMenu(fileName = "NewTextStyleId", menuName = "Assembler/Shell/Text Style Id")]
	public sealed class TextStyleId : ScriptableEnum
	{
	}
}
