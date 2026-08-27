using UnityEngine;

namespace Assembler.Shell
{
	/// <summary>
	/// The shell's canvas and its three layers. Authored once in <c>Bootstrap.unity</c>; the navigator, the
	/// overlay API and the game strip all find their parent through this rather than by name lookup.
	/// </summary>
	/// <remarks>
	/// Layer order is <see cref="ScreenHost"/> → <see cref="GameStrip"/> → <see cref="OverlayHost"/>, bottom to
	/// top by sibling order. The strip is chrome drawn over a running game, and an overlay — the pause sheet, the
	/// result slip — has to cover the strip as well as the screen beneath it.
	/// </remarks>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(Canvas))]
	[AddComponentMenu("Assembler/Shell/Shell Root")]
	public sealed class ShellRoot : MonoBehaviour
	{
		[SerializeField] private Canvas rootCanvas = null!;
		[SerializeField] private ShellHost screenHost = null!;
		[SerializeField] private ShellHost gameStrip = null!;
		[SerializeField] private ShellHost overlayHost = null!;

		/// <summary>The one screen-space-overlay canvas everything in the shell draws into.</summary>
		public Canvas RootCanvas => rootCanvas;

		/// <summary>Where screens live. Deactivated while a game is playing.</summary>
		public ShellHost ScreenHost => screenHost;

		/// <summary>Where the in-game chrome strip lives.</summary>
		public ShellHost GameStrip => gameStrip;

		/// <summary>Where sheets, slips and the launch overlay live. Never in the back stack.</summary>
		public ShellHost OverlayHost => overlayHost;
	}
}
