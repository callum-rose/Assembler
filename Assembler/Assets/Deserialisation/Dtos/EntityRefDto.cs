namespace Assembler.Deserialisation.Dtos
{
	/// <summary>
	/// A <c>!entity { Id, Property }</c> reference — reads a named property (Position, Rotation,
	/// Scale) off an entity's transform by id. A mapping tag, so it does not derive from the
	/// scalar-<c>Id</c> <see cref="RefDto"/> base. <see cref="Id"/> is typed <c>object?</c> (rather
	/// than <c>string?</c>) so it can carry a <c>!parameter</c> tag (a <see cref="ParamRefDto"/>) and
	/// not just a literal id. <see cref="Id"/> is also optional: omitting it entirely —
	/// <c>!entity { Property: … }</c> — references the enclosing entity ("self"), which works both in a
	/// direct entity behaviour and inside a template behaviour (where there is no fixed id to name).
	/// </summary>
	public sealed record EntityRefDto
	{
		public object? Id { get; init; }
		public string? Property { get; init; }
	}
}
