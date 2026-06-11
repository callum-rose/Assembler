namespace Assembler.Deserialisation.Dtos
{
	/// <summary>
	/// A <c>!if { Condition, Then, Else }</c> conditional value — picks between two value sources based on a
	/// boolean <c>Condition</c>, all three resolved live each read (only the selected branch is read). A mapping
	/// tag, so it does not derive from the scalar-<c>Id</c> <see cref="RefDto"/> base. Collapses "set X to A if
	/// cond else B" into a single value source instead of two condition-gated setters.
	/// </summary>
	public sealed record ConditionalRefDto
	{
		public object? Condition { get; init; }
		public object? Then { get; init; }
		public object? Else { get; init; }
	}
}
