using EasyDI.Unity.LifetimeScopes;

namespace Assembler.Shell.Composition
{
	/// <summary>
	/// The shell's layer of the scope chain, authored in <c>Bootstrap.unity</c> alongside the shell canvas it
	/// serves. Holds everything that lives as long as the shell does — the theme service, the catalogue, the
	/// stats store, the navigator — registered by <see cref="ShellInstaller"/>.
	/// </summary>
	public sealed class ShellLifetimeScope : LifetimeScope<ApplicationLifetimeScope>
	{
		// The application scope lives in DontDestroyOnLoad, and reparenting to it would drag this scope out of
		// Bootstrap with it — leaving a scope that outlives the scene objects its installer holds references to.
		// This scope belongs to the scene it is authored in; only its resolver chains upwards.
		protected override bool DoParentTransformToParentScope => false;
	}
}
