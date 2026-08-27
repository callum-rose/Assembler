using Assembler.Shell.Controls;
using Assembler.Shell.Navigation;
using TMPro;
using UnityEngine;

namespace Assembler.Shell.Overlays
{
	/// <summary>
	/// A sheet with a heading, a paragraph and a way out. The overlay layer's proof of life, and the shape the
	/// pause sheet and the result slip take when they land in phase 6.
	/// </summary>
	public sealed class NoticeOverlay : OverlayView
	{
		[Header("Notice")]
		[SerializeField] private TMP_Text title = null!;
		[SerializeField] private TMP_Text body = null!;
		[SerializeField] private LetterpressButton closeButton = null!;

		/// <summary>Draws the notice. Called before the sheet rises, never after.</summary>
		public void Bind(string titleText, string bodyText)
		{
			title.SetText(titleText);
			body.SetText(bodyText);
		}

		private void Awake()
		{
			closeButton.OnClick.AddListener(RequestDismiss);
		}
	}
}
