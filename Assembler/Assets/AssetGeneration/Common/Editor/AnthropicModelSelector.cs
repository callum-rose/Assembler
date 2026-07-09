#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.EditorCommon
{
	/// <summary>
	/// The refreshable Claude model dropdown shared by the vision windows (Eye Placement, Image
	/// Facing Direction): a bold label + "Refresh" header, then a popup over the fetched model ids
	/// that always keeps the current selection selectable (so a stored/typed id survives before any
	/// refresh). The model list is fetched via an injected delegate so this stays free of a direct
	/// <c>Assembler.Anthropic</c> dependency; callers pass <c>AnthropicClient.ListModelsAsync</c>.
	/// </summary>
	public sealed class AnthropicModelSelector
	{
		private readonly List<string> _models = new();

		/// <summary>
		/// Draw the header + popup and return the selected id, persisting a change to
		/// <paramref name="modelPrefKey"/>. <paramref name="onRefresh"/> is invoked when Refresh is pressed
		/// (the caller owns the async fetch + status/repaint — see <see cref="RefreshAsync"/>).
		/// </summary>
		public string Draw(string label, string current, string modelPrefKey, Action onRefresh)
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
				if (GUILayout.Button("Refresh", GUILayout.Width(70)))
				{
					onRefresh();
				}
			}

			var options = new List<string>(_models);
			if (!options.Contains(current))
			{
				options.Insert(0, current);
			}

			var index = options.IndexOf(current);
			using (var scope = new EditorGUI.ChangeCheckScope())
			{
				var picked = EditorGUILayout.Popup(index, options.ToArray());
				if (scope.changed && picked >= 0 && picked < options.Count)
				{
					current = options[picked];
					EditorPrefs.SetString(modelPrefKey, current);
				}
			}

			return current;
		}

		/// <summary>
		/// Replace the known model list via <paramref name="fetch"/> and return a status string. The
		/// caller is expected to have validated the key and to wrap this in its own try/catch + repaint.
		/// </summary>
		public async Task<string> RefreshAsync(string apiKey, Func<string, Task<IReadOnlyList<string>>> fetch)
		{
			var ids = await fetch(apiKey);
			_models.Clear();
			_models.AddRange(ids);
			return _models.Count > 0 ? $"Loaded {_models.Count} models." : "No models returned.";
		}
	}
}
