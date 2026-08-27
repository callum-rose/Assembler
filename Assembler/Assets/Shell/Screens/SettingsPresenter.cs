using Assembler.Shell.Motion;
using Assembler.Shell.Navigation;
using Assembler.Shell.Overlays;

namespace Assembler.Shell.Screens
{
	/// <summary>
	/// Opens the about sheet — and, in doing so, is the one call site in phase 3 that goes through the overlay
	/// layer rather than the stack.
	/// </summary>
	public sealed class SettingsPresenter : ScreenPresenter
	{
		private readonly SettingsView _view;
		private readonly IOverlayService _overlays;

		public SettingsPresenter(SettingsView view, IOverlayService overlays)
		{
			_view = view;
			_overlays = overlays;
		}

		protected override void Enter()
		{
			_view.AboutRequested += OnAboutRequested;
		}

		protected override void Exit()
		{
			_view.AboutRequested -= OnAboutRequested;
		}

		private void OnAboutRequested()
		{
			_overlays
				.Show<NoticeOverlay>(OverlayId.Notice, notice => notice.Bind(Placeholder.NoticeTitle, Placeholder.NoticeBody))
				.Forget(nameof(SettingsPresenter));
		}
	}
}
