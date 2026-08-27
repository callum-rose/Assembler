using System;
using DG.Tweening;
using UnityEngine;

namespace Assembler.Shell.Motion
{
	/// <summary>
	/// The two <see cref="Awaitable"/>s that come up wherever the shell awaits motion: one that is already
	/// finished, and one made from a tween that may not exist.
	/// </summary>
	public static class Awaitables
	{
		/// <summary>An <see cref="Awaitable"/> that has already finished.</summary>
		/// <remarks>
		/// What a transition returns when it had nothing to animate. Returning this rather than null keeps every
		/// call site a plain <c>await</c> — a screen with no fade and a screen mid-fade are awaited the same way.
		/// </remarks>
		public static Awaitable Completed()
		{
			var source = new AwaitableCompletionSource();
			source.SetResult();

			return source.Awaitable;
		}

		/// <summary>
		/// An <see cref="Awaitable"/> for <paramref name="tween"/>, or a finished one when there is no tween.
		/// </summary>
		public static Awaitable Of(Tween? tween)
		{
			return tween is null ? Completed() : tween.ToAwaitable();
		}

		/// <summary>
		/// Starts <paramref name="awaitable"/> and stops caring, logging rather than losing anything it throws.
		/// </summary>
		/// <remarks>
		/// For the one shape this comes up in: a click handler. A <see cref="UnityEngine.Events.UnityEvent"/>
		/// cannot return an <see cref="Awaitable"/>, so the navigation it starts has to be let go of somewhere —
		/// and an exception dropped inside an <c>async void</c> is unhandled and can take a player build down
		/// with it. Doing it here means one <c>try</c> instead of one per handler.
		/// </remarks>
		public static async void Forget(this Awaitable awaitable, object? context = null)
		{
			try
			{
				await awaitable;
			}
			catch (Exception exception)
			{
				string where = context is null ? string.Empty : $"{context}: ";
				Debug.LogError($"{where}an awaited shell operation failed. {exception}");
			}
		}
	}
}
