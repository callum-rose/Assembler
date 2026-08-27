using System;

namespace Assembler.Shell.Navigation
{
	/// <summary>
	/// A screen view that names its presenter. The navigator reads
	/// <see cref="ScreenView.PresenterType"/> off the instantiated view and builds one through DI, passing the
	/// view itself as an additional argument (UIPLAN 1.6) — so the presenter's constructor reads
	/// <c>(FeedView view, INavigator navigator, …)</c>, with everything but the view resolved.
	/// </summary>
	/// <remarks>
	/// The pairing lives here rather than on the catalog on purpose: which presenter drives a view is a fact
	/// about the view, so it is checked by the compiler instead of being a serialised type name that goes stale
	/// the first time a class is renamed. That keeps <see cref="ScreenCatalog"/> the plain id → prefab map
	/// UIPLAN 3.1 asks for.
	/// </remarks>
	/// <typeparam name="TPresenter">
	/// The presenter for this screen. Its constructor must take this view by its concrete type — the argument is
	/// matched on exactly that type, not on a base class.
	/// </typeparam>
	public abstract class ScreenView<TPresenter> : ScreenView where TPresenter : class, IScreenPresenter
	{
		public sealed override Type PresenterType => typeof(TPresenter);
	}
}
