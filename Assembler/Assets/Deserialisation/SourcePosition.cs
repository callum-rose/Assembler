namespace Assembler.Deserialisation
{
	/// <summary>
	/// A node's 1-based line/column in the source YAML, captured during deserialisation so parse-time
	/// errors raised further down the pipeline (e.g. <c>ReferenceValidator</c>) can cite where in the
	/// file the offending node was authored. <see cref="None"/> is the null-object for an unknown
	/// position (a node synthesised after deserialisation, or one whose converter didn't capture it).
	/// </summary>
	public sealed record SourcePosition(int Line, int Column)
	{
		public static readonly SourcePosition None = new(0, 0);

		public bool IsKnown => Line > 0;

		public override string ToString() => IsKnown ? $"line {Line}, column {Column}" : "unknown position";
	}
}
