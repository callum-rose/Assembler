namespace Assembler.Shell.Theming
{
	/// <summary>
	/// The named text styles of the shell. A label picks a style, never a font/size/tracking triple, so the
	/// typographic scale lives in one asset.
	/// </summary>
	/// <remarks>
	/// As with <see cref="ColorRole"/> the numbers are explicit and permanent — a style is serialised by value
	/// into every prefab that binds it. Append new styles at the end.
	/// </remarks>
	public enum TextStyleId
	{
		/// <summary>The paper's name, top-left of the masthead.</summary>
		Masthead = 0,

		/// <summary>The folio strip under the masthead: edition number, date, count.</summary>
		Folio = 1,

		/// <summary>The small capitalised title of a pushed screen.</summary>
		ScreenTitle = 2,

		/// <summary>The back-button label, which names the screen beneath the top of the stack.</summary>
		BackLabel = 3,

		/// <summary>The accent-red rubric above a headline.</summary>
		Kicker = 4,

		/// <summary>A lead or detail headline.</summary>
		Headline = 5,

		/// <summary>The byline/date line under a headline.</summary>
		HeadlineMeta = 6,

		/// <summary>Running body copy.</summary>
		Body = 7,

		/// <summary>The drop cap that opens the lead story.</summary>
		DropCap = 8,

		/// <summary>A section header ("MORE EDITIONS").</summary>
		SectionHeader = 9,

		/// <summary>A feed card's headline.</summary>
		CardTitle = 10,

		/// <summary>A feed card's standfirst.</summary>
		CardBody = 11,

		/// <summary>A feed card's meta line.</summary>
		CardMeta = 12,

		/// <summary>An archive row's headline.</summary>
		RowTitle = 13,

		/// <summary>The label on a letterpress button.</summary>
		ButtonLabel = 14,

		/// <summary>A stat band figure.</summary>
		StatValue = 15,

		/// <summary>The caption under a stat band figure.</summary>
		StatLabel = 16,

		/// <summary>Editable or selectable field text: search, settings rows.</summary>
		FieldText = 17,

		/// <summary>The PLAY stamp struck across unplayed lead art.</summary>
		Stamp = 18
	}
}
