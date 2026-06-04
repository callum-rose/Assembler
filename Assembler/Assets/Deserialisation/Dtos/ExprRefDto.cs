namespace Assembler.Deserialisation.Dtos
{
	/// <summary>
	/// A <c>!expr { Do, With }</c> call site. <c>Do</c> is either the name of a declared
	/// (named) expression or an anonymous inline C# body (e.g. <c>-arg0</c>, <c>arg0 * 2</c>);
	/// <c>With</c> supplies the operands, bound positionally to <c>arg0</c>, <c>arg1</c>, … for
	/// inline bodies, or to the named expression's declared parameters.
	///
	/// The remaining fields apply only to inline bodies (they mirror the <c>Expressions:</c>
	/// declaration block). They are optional — operand and return types are otherwise inferred —
	/// and are logged-and-ignored if present on a named call (the named expression already
	/// declares them):
	/// <list type="bullet">
	///   <item><c>ReturnType</c> — the body's return type; required when the use-site type can't
	///   be inferred (e.g. an <c>object</c>-typed spawner/template <c>Parameters:</c> slot).</item>
	///   <item><c>ArgumentTypes</c> — explicit types for <c>arg0</c>, <c>arg1</c>, … (positional to
	///   <c>With</c>), overriding per-operand inference.</item>
	///   <item><c>RegisterTypes</c> / <c>RegisterTypeStatics</c> — extra types / static-method
	///   sources to make available to the body, exactly as in a declared expression.</item>
	/// </list>
	/// </summary>
	public sealed record ExprRefDto
	{
		public string? Do { get; init; }
		public object[]? With { get; init; }
		public string? ReturnType { get; init; }
		public string[]? ArgumentTypes { get; init; }
		public string[]? RegisterTypes { get; init; }
		public string[]? RegisterTypeStatics { get; init; }
	}
}
