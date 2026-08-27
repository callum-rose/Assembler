using Assembler.Shell.Navigation;

namespace Assembler.Shell.Screens
{
	/// <summary>
	/// What the detail screen is opened with: which game's page to show (UIPLAN 3.4).
	/// </summary>
	/// <remarks>
	/// The id is a string for now. It becomes whatever the manifest calls a game when the model lands in
	/// phase 4; that is a change to one field and its call sites, which is the reason a screen's argument is a
	/// record of its own rather than a loose parameter.
	/// </remarks>
	public sealed record DetailParams(string GameId) : IScreenParams;
}
