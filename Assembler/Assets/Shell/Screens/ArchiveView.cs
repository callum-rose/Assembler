using System;
using System.Collections.Generic;
using Assembler.Shell.Controls;
using Assembler.Shell.Navigation;
using UnityEngine;

namespace Assembler.Shell.Screens
{
	/// <summary>
	/// The index of every edition. A column of rows, each of which reports the id it was bound with.
	/// </summary>
	/// <remarks>
	/// The rows are authored, fixed and stateless — they take an id and a label and hold nothing else — which is
	/// what makes swapping the column for a virtualised list a change here and nowhere else
	/// (<see href="https://github.com/callum-rose/Assembler/issues/574">#574</see>).
	/// </remarks>
	public sealed class ArchiveView : ScreenView<ArchivePresenter>
	{
		[Tooltip("The authored rows, top to bottom. Any beyond the bound count are hidden.")]
		[SerializeField] private LetterpressButton[] rows = Array.Empty<LetterpressButton>();

		private readonly List<string> _ids = new();

		/// <summary>An edition was chosen. Carries the id its row was bound with.</summary>
		public event Action<string> EditionSelected = delegate { };

		/// <summary>Draws one row per edition, hiding whatever is left over.</summary>
		public void Bind(IReadOnlyList<string> editionIds)
		{
			_ids.Clear();

			for (var i = 0; i < rows.Length; i++)
			{
				bool used = i < editionIds.Count;
				rows[i].gameObject.SetActive(used);

				if (!used)
				{
					continue;
				}

				_ids.Add(editionIds[i]);
				rows[i].Text = editionIds[i];
			}
		}

		private void Awake()
		{
			for (var i = 0; i < rows.Length; i++)
			{
				// Captured per row: the listener has to know which row it belongs to, and the loop variable
				// would otherwise be read at click time rather than at wiring time.
				int index = i;
				rows[i].OnClick.AddListener(() => OnRowClicked(index));
			}
		}

		private void OnRowClicked(int index)
		{
			if (index < _ids.Count)
			{
				EditionSelected.Invoke(_ids[index]);
			}
		}
	}
}
