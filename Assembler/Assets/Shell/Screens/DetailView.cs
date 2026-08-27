using System;
using Assembler.Shell.Controls;
using Assembler.Shell.Navigation;
using TMPro;
using UnityEngine;

namespace Assembler.Shell.Screens
{
	/// <summary>
	/// One game's page. Re-binds on every entry (UIPLAN 3.4) — the same instance serves every game, so nothing
	/// it shows may outlive the argument it was opened with.
	/// </summary>
	public sealed class DetailView : ScreenView<DetailPresenter>
	{
		[Header("Story")]
		[SerializeField] private TMP_Text headline = null!;
		[SerializeField] private TMP_Text kicker = null!;

		[Header("Controls")]
		[SerializeField] private LetterpressButton playButton = null!;
		[SerializeField] private LetterpressButton nextButton = null!;

		private string _gameId = string.Empty;

		/// <summary>The reader asked to play. Carries the id the page was bound with.</summary>
		public event Action<string> PlayRequested = delegate { };

		/// <summary>The reader asked for the next edition. Carries the id the page was bound with.</summary>
		public event Action<string> NextRequested = delegate { };

		/// <summary>Draws one game's page.</summary>
		public void Bind(string gameId, string headlineText, string kickerText)
		{
			_gameId = gameId;
			headline.SetText(headlineText);
			kicker.SetText(kickerText);
		}

		private void Awake()
		{
			playButton.OnClick.AddListener(OnPlayClicked);
			nextButton.OnClick.AddListener(OnNextClicked);
		}

		private void OnPlayClicked()
		{
			PlayRequested.Invoke(_gameId);
		}

		private void OnNextClicked()
		{
			NextRequested.Invoke(_gameId);
		}
	}
}
