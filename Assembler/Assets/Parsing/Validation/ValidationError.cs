namespace Assembler.Parsing.Validation
{
	/// <summary>
	/// A single validation failure. <see cref="Path"/> locates the offending node in the
	/// game tree (e.g. "entities/player/behaviours/move"), <see cref="Problem"/> states what is
	/// wrong, and <see cref="Fix"/> tells the author how to resolve it.
	/// </summary>
	public sealed record ValidationError(string Path, string Problem, string Fix)
	{
		public override string ToString() =>
			string.IsNullOrEmpty(Path)
				? $"{Problem}  Fix: {Fix}"
				: $"{Path}: {Problem}  Fix: {Fix}";
	}
}
