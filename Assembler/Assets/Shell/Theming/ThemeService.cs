using System;

namespace Assembler.Shell.Theming
{
	/// <inheritdoc cref="IThemeService"/>
	public sealed class ThemeService : IThemeService
	{
		private ShellTheme _current;

		public ThemeService(ShellTheme theme)
		{
			if (theme == null)
			{
				throw new ArgumentNullException(nameof(theme));
			}

			_current = theme;
		}

		public ShellTheme Current => _current;

		// Seeded with a no-op so raising never needs a null check and the event's type matches the interface's
		// exactly under nullable reference types.
		public event Action Changed = delegate { };

		public void SetTheme(ShellTheme theme)
		{
			if (theme == null)
			{
				throw new ArgumentNullException(nameof(theme));
			}

			if (theme == _current)
			{
				return;
			}

			_current = theme;
			Changed.Invoke();
		}
	}
}
