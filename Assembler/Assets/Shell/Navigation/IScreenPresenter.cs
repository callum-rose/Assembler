namespace Assembler.Shell.Navigation
{
	/// <summary>
	/// The <b>P</b> of the shell's MVP: a plain C# class routing between the model's events and its view's, with
	/// no state of its own (UIPLAN 4.1). One is built per screen instance, by the navigator, through DI.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The navigator calls <see cref="Enter"/> before the screen fades in and <see cref="Exit"/> after it has
	/// faded out — so a screen is correct by the time it is visible, and stays correct while it is leaving.
	/// </para>
	/// <para>
	/// Subscriptions belong between the two. A cached screen spends most of its life deactivated (UIPLAN 3.2),
	/// and a presenter still listening to <c>CatalogChanged</c> from off-screen is a rebuild of a page nobody is
	/// looking at.
	/// </para>
	/// </remarks>
	public interface IScreenPresenter
	{
		/// <summary>Binds the view and subscribes. Called on every entry, not only the first.</summary>
		void Enter(IScreenParams? parameters);

		/// <summary>Unsubscribes. Called after the screen has finished leaving.</summary>
		void Exit();
	}
}
