namespace Assembler.Deserialisation.Dtos
{
	/// <summary>
	/// A <c>!query { Kind, EntityTag, Origin, MaxRange }</c> spatial lookup — a pull-style, one-off perception
	/// query (e.g. inside a transition condition) that resolves against the live entity index. <c>Kind</c>
	/// selects the query verb (e.g. <c>NearestId</c>, <c>NearestPosition</c>); dispatching on it keeps the tag
	/// extensible — a new query type is one service call + one switch arm, not a new YAML tag. A mapping tag, so
	/// it does not derive from the scalar-<c>Id</c> <see cref="RefDto"/> base.
	/// </summary>
	public sealed record EntityQueryRefDto
	{
		public string? Kind { get; init; }
		public string? EntityTag { get; init; }
		public object? Origin { get; init; }
		public object? MaxRange { get; init; }
	}
}
