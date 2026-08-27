using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.Shell.Navigation
{
	/// <summary>
	/// Where a <see cref="ScreenId"/> turns into a prefab (UIPLAN 3.1). Adding a screen to the shell is a
	/// prefab, a row here, and — if it needs services nothing else does — a line in <c>ShellInstaller</c>.
	/// </summary>
	/// <remarks>
	/// The row carries a title as well as a prefab. It is the one piece of copy the navigator itself needs: the
	/// back control's label is the title of the entry beneath the top of the stack (UIPLAN 3.3), so it belongs
	/// to whichever screen is under there, not to the one drawing the button.
	/// </remarks>
	[CreateAssetMenu(fileName = "ScreenCatalog", menuName = "Assembler/Shell/Screen Catalog")]
	public sealed class ScreenCatalog : ScriptableObject
	{
		[SerializeField] private Entry[] entries = Array.Empty<Entry>();

		private Dictionary<ScreenId, Entry>? _byId;

		/// <summary>The row for <paramref name="id"/>, or null when the catalog has none.</summary>
		public Entry? Find(ScreenId id)
		{
			_byId ??= entries
				.Where(entry => entry.View != null)
				.GroupBy(entry => entry.Id)
				.ToDictionary(group => group.Key, group => group.First());

			return _byId.TryGetValue(id, out var found) ? found : null;
		}

		/// <summary>The title of <paramref name="id"/>, or the id's own name when the catalog has no row.</summary>
		public string TitleOf(ScreenId id)
		{
			var entry = Find(id);

			return entry is null || string.IsNullOrEmpty(entry.Title) ? id.ToString() : entry.Title;
		}

		/// <summary>Every complaint about this catalog, as a line of text each. Empty when it is sound.</summary>
		public IReadOnlyList<string> Validate()
		{
			var complaints = new List<string>();

			complaints.AddRange(entries
				.Where(entry => entry.View == null)
				.Select(entry => $"{entry.Id} names no prefab."));

			complaints.AddRange(entries
				.GroupBy(entry => entry.Id)
				.Where(group => group.Count() > 1)
				.Select(group => $"{group.Key} appears {group.Count()} times; only the first is reachable."));

			complaints.AddRange(Enum
				.GetValues(typeof(ScreenId))
				.Cast<ScreenId>()
				.Where(id => entries.All(entry => entry.Id != id))
				.Select(id => $"{id} has no row, so pushing it will fail."));

			return complaints;
		}

		private void OnEnable()
		{
			_byId = null;
		}

		private void OnValidate()
		{
			_byId = null;
		}

		/// <summary>One screen: its id, the prefab that draws it, and what the back button calls it.</summary>
		[Serializable]
		public sealed class Entry
		{
			[SerializeField] private ScreenId id;

			[Tooltip("The screen prefab's root ScreenView.")]
			[SerializeField] private ScreenView? view;

			[Tooltip("What this screen is called — in its own header, and on the back button of anything above it.")]
			[SerializeField] private string title = string.Empty;

			public ScreenId Id => id;

			public ScreenView? View => view;

			public string Title => title;
		}
	}
}
