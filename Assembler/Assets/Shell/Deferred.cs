using System;
using UnityEngine;

namespace Assembler.Shell
{
	/// <summary>
	/// Runs a component's re-layout <em>after</em> the editor callback that asked for it has returned.
	/// </summary>
	/// <remarks>
	/// Unity forbids resizing a <see cref="RectTransform"/> from inside <c>OnValidate</c>: the resize raises
	/// <c>OnRectTransformDimensionsChange</c> through <c>SendMessage</c>, which is not allowed during validation,
	/// and the console fills with a warning per rect instead. Anything that both validates and re-lays-out has to
	/// step out of the callback first, which is all this does.
	/// </remarks>
	internal static class Deferred
	{
		/// <summary>Runs <paramref name="action"/> once, out of band, as long as <paramref name="owner"/> survives.</summary>
		public static void Run(MonoBehaviour owner, Action action)
		{
#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				// delayCall's list is drained after it fires, so subscribing on every validation does not
				// accumulate handlers.
				UnityEditor.EditorApplication.delayCall += () =>
				{
					// The component may have been deleted, or its prefab contents unloaded, in between.
					if (owner != null)
					{
						action();
					}
				};

				return;
			}
#endif

			action();
		}
	}
}
