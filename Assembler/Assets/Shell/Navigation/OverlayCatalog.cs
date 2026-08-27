using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.Shell.Navigation
{
	/// <summary>
	/// Where an <see cref="OverlayId"/> turns into a prefab. The screens' catalog with the titles left off —
	/// an overlay is never underneath anything, so nothing ever has to name it.
	/// </summary>
	[CreateAssetMenu(fileName = "OverlayCatalog", menuName = "Assembler/Shell/Overlay Catalog")]
	public sealed class OverlayCatalog : ScriptableObject
	{
		[SerializeField] private Entry[] entries = Array.Empty<Entry>();

		private Dictionary<OverlayId, Entry>? _byId;

		/// <summary>The row for <paramref name="id"/>, or null when the catalog has none.</summary>
		public Entry? Find(OverlayId id)
		{
			_byId ??= entries
				.Where(entry => entry.View != null)
				.GroupBy(entry => entry.Id)
				.ToDictionary(group => group.Key, group => group.First());

			return _byId.TryGetValue(id, out var found) ? found : null;
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
				.GetValues(typeof(OverlayId))
				.Cast<OverlayId>()
				.Where(id => entries.All(entry => entry.Id != id))
				.Select(id => $"{id} has no row, so showing it will fail."));

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

		/// <summary>One overlay: its id and the prefab that draws it.</summary>
		[Serializable]
		public sealed class Entry
		{
			[SerializeField] private OverlayId id;

			[Tooltip("The overlay prefab's root OverlayView.")]
			[SerializeField] private OverlayView? view;

			public OverlayId Id => id;

			public OverlayView? View => view;
		}
	}
}
