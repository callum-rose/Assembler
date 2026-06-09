namespace Assembler.Deserialisation.Dtos
{
	/// <summary>
	/// A <c>!rigidbody { Id, Property }</c> reference — reads a physics property (Velocity,
	/// AngularVelocity, Position) off an entity's <c>Rigidbody</c> by id. A mapping tag, so it does
	/// not derive from the scalar-<c>Id</c> <see cref="RefDto"/> base.
	/// </summary>
	public sealed record RigidbodyRefDto
	{
		public string? Id { get; init; }
		public string? Property { get; init; }
	}
}
