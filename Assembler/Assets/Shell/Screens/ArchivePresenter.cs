using Assembler.Shell.Motion;
using Assembler.Shell.Navigation;

namespace Assembler.Shell.Screens
{
	/// <summary>
	/// Fills the index and opens whichever edition was chosen.
	/// </summary>
	public sealed class ArchivePresenter : ScreenPresenter
	{
		private readonly ArchiveView _view;
		private readonly INavigator _navigator;

		public ArchivePresenter(ArchiveView view, INavigator navigator)
		{
			_view = view;
			_navigator = navigator;
		}

		protected override void Enter()
		{
			_view.Bind(Placeholder.Editions);
			_view.EditionSelected += OnEditionSelected;
		}

		protected override void Exit()
		{
			_view.EditionSelected -= OnEditionSelected;
		}

		private void OnEditionSelected(string gameId)
		{
			_navigator.Push(ScreenId.Detail, new DetailParams(gameId)).Forget(nameof(ArchivePresenter));
		}
	}
}
