using System;
using Assembler.Shell.Controls;
using Assembler.Shell.Navigation;
using UnityEngine;

namespace Assembler.Shell.Screens
{
	/// <summary>
	/// The settings page. Theme, the hidden dev row and the folio land in phase 5; for now it carries the one
	/// control that proves the overlay layer is wired.
	/// </summary>
	public sealed class SettingsView : ScreenView<SettingsPresenter>
	{
		[SerializeField] private LetterpressButton aboutButton = null!;

		/// <summary>The reader asked for the about sheet.</summary>
		public event Action AboutRequested = delegate { };

		private void Awake()
		{
			aboutButton.OnClick.AddListener(OnAboutClicked);
		}

		private void OnAboutClicked()
		{
			AboutRequested.Invoke();
		}
	}
}
