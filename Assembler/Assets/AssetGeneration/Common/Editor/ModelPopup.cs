#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.EditorCommon
{
	/// <summary>
	/// A model-id dropdown over a fixed list of known ids that keeps any previously-saved custom
	/// id selectable by prepending it when it isn't already in the list. Shared by every generation
	/// window that offers a provider's known models (image providers, Meshy) — see
	/// <see cref="AnthropicModelSelector"/> for the refreshable Anthropic variant.
	/// </summary>
	public static class ModelPopup
	{
		/// <summary>
		/// Draw the popup and return the selected id. <paramref name="current"/> is returned unchanged
		/// when <paramref name="models"/> is empty.
		/// </summary>
		public static string Draw(string label, string current, IReadOnlyList<string> models, string? tooltip = null)
		{
			var known = models as string[] ?? models.ToArray();
			var options = known.Contains(current) || string.IsNullOrEmpty(current)
				? known
				: new[] { current }.Concat(known).ToArray();
			if (options.Length == 0)
			{
				return current;
			}

			var index = Mathf.Max(0, Array.IndexOf(options, current));
			index = EditorGUILayout.Popup(
				new GUIContent(label, tooltip), index, options.Select(m => new GUIContent(m)).ToArray());
			return options[index];
		}
	}
}
