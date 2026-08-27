using System;
using Assembler.Shell.Controls;
using Assembler.Shell.Navigation;
using TMPro;
using UnityEngine;

namespace Assembler.Shell.Screens
{
	/// <summary>
	/// The front page. Passive: it draws what it is handed and reports what was tapped, and knows nothing about
	/// the catalogue, the stack, or what any of its buttons mean (UIPLAN 4.1).
	/// </summary>
	/// <remarks>
	/// <b><see cref="LeadSelected"/> carries the id the view was bound with</b> (UIPLAN 4.2). That is the rule
	/// that keeps the presenter stateless: an event that said only "the lead was tapped" would force the
	/// presenter to remember which game the lead currently is, and the stateless claim would rot from there.
	/// </remarks>
	public sealed class FeedView : ScreenView<FeedPresenter>
	{
		[Header("Lead")]
		[SerializeField] private TMP_Text headline = null!;
		[SerializeField] private LetterpressButton playButton = null!;

		[Header("Elsewhere")]
		[SerializeField] private LetterpressButton archiveButton = null!;
		[SerializeField] private LetterpressButton settingsButton = null!;

		private string _leadGameId = string.Empty;

		/// <summary>The lead story was chosen. Carries the id it was bound with.</summary>
		public event Action<string> LeadSelected = delegate { };

		/// <summary>The reader asked for the archive.</summary>
		public event Action ArchiveRequested = delegate { };

		/// <summary>The reader asked for the settings page.</summary>
		public event Action SettingsRequested = delegate { };

		/// <summary>Draws the lead story.</summary>
		public void Bind(string gameId, string headlineText)
		{
			_leadGameId = gameId;
			headline.SetText(headlineText);
		}

		private void Awake()
		{
			playButton.OnClick.AddListener(OnPlayClicked);
			archiveButton.OnClick.AddListener(OnArchiveClicked);
			settingsButton.OnClick.AddListener(OnSettingsClicked);
		}

		private void OnPlayClicked()
		{
			LeadSelected.Invoke(_leadGameId);
		}

		private void OnArchiveClicked()
		{
			ArchiveRequested.Invoke();
		}

		private void OnSettingsClicked()
		{
			SettingsRequested.Invoke();
		}
	}
}
