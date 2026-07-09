namespace Assembler.Deserialisation
{
	/// <summary>
	/// Where a node was authored in the source YAML, threaded from deserialisation so parse-time errors
	/// raised further down the pipeline (e.g. <c>ReferenceValidator</c>) can cite the offending location.
	/// A discriminated union: <see cref="KnownSourcePosition"/> carries a 1-based line/column, while
	/// <see cref="Unknown"/> stands in for a node synthesised after deserialisation, or one whose converter
	/// didn't capture a position. Consumers match on the case rather than branching on a flag.
	/// </summary>
	public abstract record SourcePosition
	{
		public static readonly SourcePosition Unknown = new UnknownSourcePosition();

		public static SourcePosition At(int line, int column) => new KnownSourcePosition(line, column);
	}

	public sealed record KnownSourcePosition(int Line, int Column) : SourcePosition
	{
		public override string ToString() => $"line {Line}, column {Column}";
	}

	public sealed record UnknownSourcePosition : SourcePosition
	{
		public override string ToString() => "unknown position";
	}
}
