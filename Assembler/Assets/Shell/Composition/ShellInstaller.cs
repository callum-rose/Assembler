using Assembler.Shell.Navigation;
using Assembler.Shell.Theming;
using EasyDI.LifecycleHooks;
using EasyDI.Registering;
using EasyDI.Unity.LifetimeScopes;
using UnityEngine;

namespace Assembler.Shell.Composition
{
	/// <summary>
	/// Registers the shell's services into <see cref="ShellLifetimeScope"/>. Theme, config and catalog assets,
	/// and the authored canvas, arrive as serialized fields — the installer is where the scene's authored
	/// objects meet the object graph.
	/// </summary>
	public sealed class ShellInstaller : MonoInstaller
	{
		[Header("Assets")]
		[SerializeField] private ShellTheme theme = null!;
		[SerializeField] private ShellConfig config = null!;
		[SerializeField] private ScreenCatalog screenCatalog = null!;
		[SerializeField] private OverlayCatalog overlayCatalog = null!;

		[Header("Scene")]
		[SerializeField] private ShellRoot shellRoot = null!;

		public override void Install(IObjectRegistry registry)
		{
			if (theme == null || config == null || screenCatalog == null || overlayCatalog == null ||
				shellRoot == null)
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
			registry.RegisterInstance<ScreenCatalog>(screenCatalog);
			registry.RegisterInstance<OverlayCatalog>(overlayCatalog);
			registry.RegisterInstance<ShellRoot>(shellRoot);

			// Registered as their interfaces only. A presenter that reached for ScreenNavigator rather than
			// INavigator would be reaching past the seam the whole pattern is built on, so the concrete type is
			// simply not resolvable.
			registry.RegisterSingleton<ScreenNavigator>().As<INavigator>();
			registry.RegisterSingleton<OverlayService>().As<IOverlayService>();

			registry.RegisterLifecycleHook<ShellStartup>();
		}
	}
}
