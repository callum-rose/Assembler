using TMPro;
using UnityEngine;

namespace Assembler.Shell.Controls
{
	/// <summary>
	/// The rubric that opens a section — a short capitalised title, an optional count on the right, and the
	/// double rule beneath that says a new part of the page has started.
	/// </summary>
	/// <remarks>
	/// Fixed height, like every other piece of chrome: the double rule and the paddings around the title are
	/// authored on the prefab, and the title clamps rather than growing its own header (UIPLAN 6.2). Its bind
	/// surface is one call, so a presenter never reaches past it into a label.
	/// </remarks>
	[DisallowMultipleComponent]
	[RequireComponent(typeof(RectTransform))]
	[AddComponentMenu("Assembler/Shell/Section Header")]
	public sealed class SectionHeader : MonoBehaviour
	{
		[SerializeField] private TMP_Text title = null!;

		[Tooltip("The quiet count on the right — '6 EDITIONS'. Hidden when nothing is bound to it.")]
		[SerializeField] private TMP_Text? caption;

		/// <summary>The section's title.</summary>
		public string Title
		{
			get => title == null ? string.Empty : title.text;
			set => Bind(value, Caption);
		}

		/// <summary>The quiet count on the right, or empty when there is none.</summary>
		public string Caption => caption == null ? string.Empty : caption.text;

		/// <summary>Writes both halves of the header. A null or empty caption hides the right-hand label.</summary>
		public void Bind(string title, string? caption = null)
		{
			if (this.title != null)
			{
				this.title.SetText(title);
			}

			if (this.caption == null)
			{
				return;
			}

			bool hasCaption = !string.IsNullOrEmpty(caption);
			this.caption.SetText(caption ?? string.Empty);
			this.caption.gameObject.SetActive(hasCaption);
		}
	}
}
