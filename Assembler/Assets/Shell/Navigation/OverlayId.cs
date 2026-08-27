namespace Assembler.Shell.Navigation
{
	/// <summary>
	/// The shell's overlays. Not screens and never in the back stack (UIPLAN 3.6) — an overlay is something that
	/// happens <em>over</em> where you are, and going back from one means closing it, not leaving the page.
	/// </summary>
	/// <remarks>
	/// The pause sheet, the result slip and the launch overlay join this list with game integration.
	/// </remarks>
	public enum OverlayId
	{
		/// <summary>A plain sheet with a heading, a line of copy and a way out.</summary>
		Notice = 0
	}
}
