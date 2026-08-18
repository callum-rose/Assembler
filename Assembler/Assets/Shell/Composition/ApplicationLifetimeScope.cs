using EasyDI.Unity.LifetimeScopes;

namespace Assembler.Shell.Composition
{
	/// <summary>
	/// The top of the scope chain: services that outlive every scene and every game session. EasyDI instantiates
	/// its prefab before the first scene loads and keeps it in <c>DontDestroyOnLoad</c>, so it is the composition
	/// root proper — the prefab is named on the <c>EasyDISettings</c> asset.
	/// </summary>
	/// <remarks>
	/// Empty for now. The shell's own services register a layer down in <see cref="ShellLifetimeScope"/>, and a
	/// per-game-session scope nests below that when game integration lands.
	/// </remarks>
	public sealed class ApplicationLifetimeScope : RootLifetimeScope
	{
	}
}
