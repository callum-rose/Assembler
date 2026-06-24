using System.Collections.Generic;

namespace Assembler.Deserialisation.Dtos
{
	/// <summary>
	/// A <c>!expr { Do, With }</c> call site. <c>Do</c> is either the name of a declared
	/// (named) expression or an anonymous inline C# body (e.g. <c>-velocity</c>, <c>score * 2</c>);
	/// <c>With</c> is a <em>map</em> of <c>name: value</c> operands. For an inline body each key is
	/// the parameter name referenced in the body; for a named call each key matches one of the
	/// expression's declared <c>ArgumentNames</c>. Declaration order is preserved.
	///
	/// The remaining fields apply only to inline bodies (they mirror the <c>Expressions:</c>
	/// declaration block). They are optional — operand and return types are otherwise inferred —
	/// and are logged-and-ignored if present on a named call (the named expression already
	/// declares them):
	/// <list type="bullet">
	///   <item><c>ReturnType</c> — the body's return type; required when the use-site type can't
	///   be inferred (e.g. an <c>object</c>-typed spawner/template <c>Parameters:</c> slot).</item>
	///   <item><c>ArgumentTypes</c> — explicit operand types, positional to <c>With</c>'s declaration
	///   order, overriding per-operand inference.</item>
	///   <item><c>RegisterTypes</c> / <c>RegisterTypeStatics</c> — extra types / static-method
	///   sources to make available to the body, exactly as in a declared expression.</item>
	/// </list>
	/// </summary>
	public sealed record ExprRefDto
	{
		public string? Do { get; init; }
		public IReadOnlyList<ExprArgDto>? With { get; init; }
		public string? ReturnType { get; init; }
		public string[]? ArgumentTypes { get; init; }
		public string[]? RegisterTypes { get; init; }
		public string[]? RegisterTypeStatics { get; init; }
	}

	/// <summary>A single named operand of a <c>!expr</c> <c>With</c> map: the parameter
	/// <see cref="Name"/> and its (still-raw) <see cref="Value"/>.</summary>
	public sealed record ExprArgDto(string Name, object? Value);
}
