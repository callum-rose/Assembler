using System;
using UnityEngine;

namespace Assembler.Shell.Theming
{
	/// <summary>
	/// Static reach for the theme — sanctioned heresy, and deliberately narrow. Only the leaf binder components
	/// (<see cref="Binders.ThemeColor"/>, <see cref="Binders.TextStyleBinder"/>) use it, because they run in edit
	/// mode where no composition root exists and a MonoBehaviour has no constructor to inject through. Anything
	/// with a constructor takes <see cref="IThemeService"/> from DI instead.
	/// </summary>
	public static class Theme
	{
		private static IThemeService? _service;

		/// <summary>Raised whenever the bound service swaps theme, or a different service is bound.</summary>
		public static event Action Changed = delegate { };

		/// <summary>
		/// The bound service. Falls back to one built from <see cref="ShellTheme.DefaultResourcePath"/> so that
		/// binders preview correctly in the editor with no scene running.
		/// </summary>
		public static IThemeService Service
		{
			get
			{
				if (_service is null)
				{
					Bind(new ThemeService(LoadFallbackTheme()));
				}

				return _service!;
			}
		}

		/// <summary>The theme in force.</summary>
		public static ShellTheme Current => Service.Current;

		/// <summary>
		/// Points the accessor at the composition root's service. Called once by <c>ShellInstaller</c>; binding
		/// a second service replaces the first and repaints.
		/// </summary>
		public static void Bind(IThemeService service)
		{
			if (service is null)
			{
				throw new ArgumentNullException(nameof(service));
			}

			if (ReferenceEquals(service, _service))
			{
				return;
			}

			if (_service is not null)
			{
				_service.Changed -= Raise;
			}

			_service = service;
			_service.Changed += Raise;
			Raise();
		}

		private static void Raise()
		{
			Changed.Invoke();
		}

		// A theme is always available: without the Resources asset the shell wears an empty theme, whose roles
		// resolve to magenta and whose styles warn. Loud on screen beats a null reference in every binder.
		private static ShellTheme LoadFallbackTheme()
		{
			var theme = Resources.Load<ShellTheme>(ShellTheme.DefaultResourcePath);

			if (theme != null)
			{
				return theme;
			}

			Debug.LogWarning(
				$"No ShellTheme at Resources/{ShellTheme.DefaultResourcePath}. The shell is running on an empty " +
				"theme — run 'Assembler > Shell > Create Shell Assets' to author one.");

			var placeholder = ScriptableObject.CreateInstance<ShellTheme>();
			placeholder.name = "ShellTheme (missing)";
			placeholder.hideFlags = HideFlags.HideAndDontSave;
			return placeholder;
		}
	}
}
