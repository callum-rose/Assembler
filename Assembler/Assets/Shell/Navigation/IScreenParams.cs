namespace Assembler.Shell.Navigation
{
	/// <summary>
	/// What a screen is pushed <em>with</em>. A marker: the point is that a screen's argument is a type of its
	/// own (<c>DetailParams</c>, not a loose string or a dictionary), so a push that names the wrong thing fails
	/// to compile rather than at the top of the screen it opened.
	/// </summary>
	/// <remarks>
	/// Implementations are immutable records. A screen re-binds from its params on every entry (UIPLAN 3.4), so
	/// they are read many times and written once.
	/// </remarks>
	public interface IScreenParams
	{
	}
}
