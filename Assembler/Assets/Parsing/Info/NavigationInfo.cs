namespace Assembler.Parsing.Info
{
	/// <summary>
	/// Grid-navigation configuration from the descriptor's <c>Navigation:</c> section: the planar (XY) world
	/// bounds the walkability grid spans, its cell size, and the entity tag whose colliders are rasterized as
	/// obstacles. <see cref="Default"/> is used when no section is present.
	/// </summary>
	public sealed record NavigationInfo(
		float CellSize,
		float MinX,
		float MinY,
		float MaxX,
		float MaxY,
		string ObstacleTag)
	{
		public static NavigationInfo Default => new(1f, -50f, -50f, 50f, 50f, "obstacle");
	}
}
