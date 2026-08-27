namespace Assembler.Shell.Controls
{
	/// <summary>
	/// How heavily a <see cref="Rule"/> is struck. The three weights the newspaper uses, and no others — a rule
	/// whose thickness is a free number is a rule that drifts out of step with the ones beside it.
	/// </summary>
	public enum RuleWeight
	{
		/// <summary>The hairline between cells and rows.</summary>
		Hairline = 0,

		/// <summary>The heavy rule under the masthead.</summary>
		Heavy = 1,

		/// <summary>Two hairlines with a hairline-and-a-half of paper between them — a section header's rule.</summary>
		Double = 2
	}
}
