namespace Assembler.Deserialisation.Dtos
{
	/// <summary>
	/// A <c>!entity { Id, Property }</c> reference — reads a named property (Position, Rotation,
	/// Scale) off an entity's transform by id. A mapping tag, so it does not derive from the
	/// scalar-<c>Id</c> <see cref="RefDto"/> base. <see cref="Id"/> is typed <c>object?</c> (rather
	/// than <c>string?</c>) so it can carry a <c>!parameter</c> tag (a <see cref="ParamRefDto"/>) and
	/// not just a literal id — letting a template behaviour read its own transform via
	/// <c>!entity { Id: !parameter self_id, Property: … }</c>.
	/// </summary>
	public sealed record EntityRefDto
	{
		public object? Id { get; init; }
		public string? Property { get; init; }
	}
}
