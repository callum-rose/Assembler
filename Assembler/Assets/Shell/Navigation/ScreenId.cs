namespace Assembler.Shell.Navigation
{
	/// <summary>
	/// The shell's screens. A push names one of these; <see cref="ScreenCatalog"/> is where it turns into a
	/// prefab.
	/// </summary>
	/// <remarks>
	/// A plain C# enum rather than an asset-backed one like <see cref="Theming.ColorRole"/>. The argument for
	/// assets was that a colour role is serialised into hundreds of prefabs, so a reorder must not repaint the
	/// app; a screen id is named from <em>code</em> and serialised into exactly one asset, the catalog. The
	/// explicit numbers keep even that one asset safe to reorder around.
	/// </remarks>
	public enum ScreenId
	{
		/// <summary>The front page: the lead story, the card grid, the folio.</summary>
		Feed = 0,

		/// <summary>One game's page: kicker, description, how to play, stats, play button.</summary>
		Detail = 1,

		/// <summary>The index of every edition, searchable.</summary>
		Archive = 2,

		/// <summary>Theme, the hidden dev row, and the folio.</summary>
		Settings = 3
	}
}
