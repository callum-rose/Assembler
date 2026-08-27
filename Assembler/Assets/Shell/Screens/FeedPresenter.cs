using Assembler.Shell.Motion;
using Assembler.Shell.Navigation;

namespace Assembler.Shell.Screens
{
	/// <summary>
	/// Routes the front page's taps into the stack. Stateless — the only thing it would have wanted to remember,
	/// which game the lead is, comes back on the event that asks for it.
	/// </summary>
	public sealed class FeedPresenter : ScreenPresenter
	{
		private readonly FeedView _view;
		private readonly INavigator _navigator;

		public FeedPresenter(FeedView view, INavigator navigator)
		{
			_view = view;
			_navigator = navigator;
		}

		protected override void Enter()
		{
			_view.Bind(Placeholder.LeadGameId, Placeholder.LeadHeadline);

			_view.LeadSelected += OnLeadSelected;
			_view.ArchiveRequested += OnArchiveRequested;
			_view.SettingsRequested += OnSettingsRequested;
		}

		protected override void Exit()
		{
			_view.LeadSelected -= OnLeadSelected;
			_view.ArchiveRequested -= OnArchiveRequested;
			_view.SettingsRequested -= OnSettingsRequested;
		}

		// Push, not a launch: the detail page is where the controls are taught, and a direct launch trades one
		// tap for a confused first ten seconds in an unfamiliar game (UIPLAN 11.6).
		private void OnLeadSelected(string gameId)
		{
			_navigator.Push(ScreenId.Detail, new DetailParams(gameId)).Forget(nameof(FeedPresenter));
		}

		private void OnArchiveRequested()
		{
			_navigator.Push(ScreenId.Archive).Forget(nameof(FeedPresenter));
		}

		private void OnSettingsRequested()
		{
			_navigator.Push(ScreenId.Settings).Forget(nameof(FeedPresenter));
		}
	}
}
