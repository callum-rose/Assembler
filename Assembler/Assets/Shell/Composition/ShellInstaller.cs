using Assembler.Shell.Theming;
using EasyDI.Registering;
using EasyDI.Unity.LifetimeScopes;
using UnityEngine;

namespace Assembler.Shell.Composition
{
	/// <summary>
	/// Registers the shell's services into <see cref="ShellLifetimeScope"/>. Theme and config assets, and the
	/// authored canvas, arrive as serialized fields — the installer is where the scene's authored objects meet
	/// the object graph.
	/// </summary>
	public sealed class ShellInstaller : MonoInstaller
	{
		[SerializeField] private ShellTheme theme = null!;
		[SerializeField] private ShellConfig config = null!;
		[SerializeField] private ShellRoot shellRoot = null!;

		public override void Install(IObjectRegistry registry)
		{
			if (theme == null || config == null || shellRoot == null)
			{
				Debug.LogError(
					$"{nameof(ShellInstaller)} on '{name}' has an unassigned field — the shell cannot start.",
					this);
				return;
			}

			var themeService = new ThemeService(theme);

			// The one place the static accessor is bound. Leaf binder components read the theme through it
			// because they run in edit mode, where no scope exists; everything with a constructor takes
			// IThemeService from here instead.
			Theme.Bind(themeService);

			registry.RegisterInstance<IThemeService>(themeService);
			registry.RegisterInstance<ShellConfig>(config);
			registry.RegisterInstance<ShellRoot>(shellRoot);
		}
	}
}
