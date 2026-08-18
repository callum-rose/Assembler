namespace Assembler.Shell.Theming
{
	/// <summary>
	/// How a <see cref="TextStyle"/> cases the string it is given. Kept separate from bold/italic so that a
	/// style's case reads as a typographic decision rather than as a bit in TextMeshPro's font-style mask.
	/// </summary>
	public enum TextCase
	{
		/// <summary>Render the string as authored.</summary>
		AsTyped = 0,

		/// <summary>Upper case — the shell's rubrics, folio and section headers.</summary>
		UpperCase = 1,

		/// <summary>Lower case.</summary>
		LowerCase = 2,

		/// <summary>Small capitals.</summary>
		SmallCaps = 3
	}
}
