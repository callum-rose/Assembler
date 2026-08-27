using UnityEngine;

namespace Assembler.Shell.Navigation
{
	/// <summary>
	/// The shell's back stack (UIPLAN 3.3). A real stack, not a history log: <see cref="Push"/> puts a screen
	/// on top of the one you came from, <see cref="Pop"/> takes you back to it, and <see cref="Replace"/> swaps
	/// the top without deepening.
	/// </summary>
	/// <remarks>
	/// Overlays — the pause sheet, the result slip, the launch overlay — are not screens and never appear here.
	/// They live on <see cref="IOverlayService"/> (UIPLAN 3.6).
	/// </remarks>
	public interface INavigator
	{
		/// <summary>The screen on top, or null before the first push.</summary>
		ScreenId? Current { get; }

		/// <summary>The screen underneath the top — what going back would return to. Null at the root.</summary>
		ScreenId? Beneath { get; }

		/// <summary>How deep the stack is.</summary>
		int Depth { get; }

		/// <summary>Whether there is anything to go back to.</summary>
		bool CanPop { get; }

		/// <summary>Whether a transition is running. Requests made during one are refused, not queued.</summary>
		bool IsTransitioning { get; }

		/// <summary>Puts <paramref name="id"/> on top of the stack. On an empty stack it becomes the root.</summary>
		Awaitable Push(ScreenId id, IScreenParams? parameters = null);

		/// <summary>Takes the top screen off. A no-op at the root — the stack is never left empty.</summary>
		Awaitable Pop();

		/// <summary>Swaps the top screen for <paramref name="id"/>, leaving the depth unchanged.</summary>
		Awaitable Replace(ScreenId id, IScreenParams? parameters = null);
	}
}
