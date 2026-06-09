namespace Assembler.Deserialisation.Dtos
{
	/// <summary>
	/// A <c>!entity { Id, Property }</c> reference — reads a named property (Position, Rotation,
	/// Scale) off an entity's transform by id. A mapping tag, so it does not derive from the
	/// scalar-<c>Id</c> <see cref="RefDto"/> base.
	/// </summary>
	public sealed record EntityRefDto
	{
		public string? Id { get; init; }
		public string? Property { get; init; }
	}
}
