namespace Assembler.Resolving
{
	/// <summary>
	/// Marker for a behaviour that needs the shared <see cref="EntityQueryService"/>. The build pipeline sets
	/// <see cref="Query"/> after construction (mirrors <c>INeedsGameClock</c> / <c>INeedsSpawner</c>), so the
	/// behaviour never looks the service up itself.
	/// </summary>
	public interface INeedsEntityQuery
	{
		EntityQueryService Query { get; set; }
	}
}
