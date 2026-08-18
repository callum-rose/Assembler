using System;

namespace Assembler.Shell.Theming
{
	/// <summary>
	/// Owns the theme the shell is currently wearing and announces swaps. Everything with a constructor takes
	/// this through DI; only the leaf binder components reach for the static <see cref="Theme"/> accessor.
	/// </summary>
	public interface IThemeService
	{
		/// <summary>The theme in force.</summary>
		ShellTheme Current { get; }

		/// <summary>Raised after <see cref="Current"/> changes, so bound graphics repaint.</summary>
		event Action Changed;

		/// <summary>Swaps the theme and repaints. A no-op when the theme is already in force.</summary>
		void SetTheme(ShellTheme theme);
	}
}
