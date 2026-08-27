using System;

namespace Assembler.Shell.Screens
{
	/// <summary>
	/// The stand-in catalogue phase 3's screens are wired against. There is no shelf service yet and no stats
	/// store, so the navigation has to be proved against something — this is that something, and phase 4 deletes
	/// it when the real model arrives.
	/// </summary>
	/// <remarks>
	/// Deliberately not in the shell's <c>Copy</c> class (UIPLAN 5.8), which does not exist yet and will hold
	/// copy the app actually ships. None of this is copy the app ships.
	/// </remarks>
	internal static class Placeholder
	{
		public const string LeadGameId = "edition-014";

		public const string LeadHeadline = "The lead story goes here";

		public const string NoticeTitle = "About";

		public const string NoticeBody =
			"A placeholder sheet, standing in for the pause sheet and the result slip until there is a game to " +
			"pause. Tap outside it, or the button, to close it.";

		/// <summary>The archive's stand-in rows, newest first — the ordering UIPLAN 11.1 asks for.</summary>
		public static readonly string[] Editions =
		{
			"edition-014",
			"edition-013",
			"edition-012",
			"edition-011"
		};

		/// <summary>The edition after <paramref name="gameId"/>, wrapping — the detail page's "next" button.</summary>
		public static string Next(string gameId)
		{
			int index = Array.IndexOf(Editions, gameId);

			return Editions[(index + 1 + Editions.Length) % Editions.Length];
		}
	}
}
