namespace Assembler.Shell.Theming
{
	/// <summary>
	/// The named colour slots of the shell palette. Every graphic in the shell paints from a role rather than a
	/// literal colour, so a second <see cref="ShellTheme"/> asset (dark mode) re-skins the app without touching a
	/// prefab.
	/// </summary>
	/// <remarks>
	/// The numbers are explicit and permanent: a role is serialised by value into every prefab and scene that
	/// binds it, so reordering the enum would silently repaint the app. Append new roles at the end.
	/// </remarks>
	public enum ColorRole
	{
		/// <summary>The page ground the whole shell sits on.</summary>
		Paper = 0,

		/// <summary>Raised surfaces: sheets, cards, inputs.</summary>
		Surface = 1,

		/// <summary>Inset surfaces: the search field, segmented controls.</summary>
		Sunk = 2,

		/// <summary>Primary text.</summary>
		Ink = 3,

		/// <summary>Body text — a step down from <see cref="Ink"/>.</summary>
		InkSecondary = 4,

		/// <summary>Metadata and captions — the quietest text.</summary>
		InkTertiary = 5,

		/// <summary>Hairline rules between cells and rows.</summary>
		Rule = 6,

		/// <summary>The heavy rules: under the masthead, under a section header.</summary>
		RuleHard = 7,

		/// <summary>The masthead red.</summary>
		Accent = 8,

		/// <summary>The demoted second accent.</summary>
		AccentSecondary = 9,

		/// <summary>Text and glyphs drawn on top of <see cref="Accent"/>.</summary>
		OnAccent = 10,

		/// <summary>Positive verdicts.</summary>
		Good = 11,

		/// <summary>Negative verdicts and failures.</summary>
		Bad = 12,

		/// <summary>Staging-channel entries, visible only in dev mode.</summary>
		Staging = 13,

		/// <summary>The ground a piece of game art sits on before it loads.</summary>
		ArtBackground = 14,

		/// <summary>The plate of a letterpress button.</summary>
		ButtonFace = 15,

		/// <summary>Text on a letterpress button.</summary>
		ButtonInk = 16,

		/// <summary>The hard ledge a letterpress element casts — the depth that a press consumes.</summary>
		Offset = 17
	}
}
