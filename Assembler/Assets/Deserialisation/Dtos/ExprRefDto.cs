namespace Assembler.Deserialisation.Dtos
{
	/// <summary>
	/// A <c>!expr { Do, With }</c> call site. <c>Do</c> is either the name of a declared
	/// (named) expression or an anonymous inline C# body (e.g. <c>-arg0</c>, <c>arg0 * 2</c>);
	/// <c>With</c> supplies the operands, bound positionally to <c>arg0</c>, <c>arg1</c>, … for
	/// inline bodies, or to the named expression's declared parameters.
	/// </summary>
	public sealed record ExprRefDto
	{
		public string? Do { get; init; }
		public object[]? With { get; init; }
	}
}
