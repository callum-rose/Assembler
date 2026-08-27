using Assembler.Shell.Motion;
using Assembler.Shell.Navigation;
using UnityEngine;

namespace Assembler.Shell.Screens
{
	/// <summary>
	/// Binds one game's page from the argument it was opened with, and routes its two buttons.
	/// </summary>
	/// <remarks>
	/// The next-edition button <see cref="INavigator.Replace"/>s rather than pushing (UIPLAN 3.3): reading three
	/// games one after another should not bury the page you started from under three detail pages.
	/// </remarks>
	public sealed class DetailPresenter : ScreenPresenter<DetailParams>
	{
		private readonly DetailView _view;
		private readonly INavigator _navigator;

		public DetailPresenter(DetailView view, INavigator navigator)
		{
			_view = view;
			_navigator = navigator;
		}

		protected override void Enter(DetailParams? parameters)
		{
			string gameId = parameters?.GameId ?? Placeholder.LeadGameId;
			_view.Bind(gameId, Placeholder.LeadHeadline, gameId.ToUpperInvariant());

			_view.PlayRequested += OnPlayRequested;
			_view.NextRequested += OnNextRequested;
		}

		protected override void Exit()
		{
			_view.PlayRequested -= OnPlayRequested;
			_view.NextRequested -= OnNextRequested;
		}

		// Launching is phase 6's: there is no session contract to hand a game to yet.
		private void OnPlayRequested(string gameId)
		{
			Debug.Log($"{nameof(DetailPresenter)}: play '{gameId}' — game integration lands in phase 6.");
		}

		private void OnNextRequested(string gameId)
		{
			var next = new DetailParams(Placeholder.Next(gameId));
			_navigator.Replace(ScreenId.Detail, next).Forget(nameof(DetailPresenter));
		}
	}
}
