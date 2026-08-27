using Assembler.Shell.Motion;
using Assembler.Shell.Navigation;
using EasyDI.Unity.LifecycleHooks;

namespace Assembler.Shell.Composition
{
	/// <summary>
	/// Opens the paper on the front page. The shell's one entry point: everything after this is something the
	/// reader asked for.
	/// </summary>
	/// <remarks>
	/// An <see cref="IStartable"/> rather than a <c>MonoBehaviour</c> in the scene, so that what starts the shell
	/// is registered in the same place as everything else the shell is made of — and so the first push happens
	/// after the whole object graph is built, not at whatever point in the scene's <c>Start</c> order a component
	/// happened to sit.
	/// </remarks>
	public sealed class ShellStartup : IStartable
	{
		private readonly INavigator _navigator;

		public ShellStartup(INavigator navigator)
		{
			_navigator = navigator;
		}

		public void Start()
		{
			_navigator.Push(ScreenId.Feed).Forget(nameof(ShellStartup));
		}
	}
}
