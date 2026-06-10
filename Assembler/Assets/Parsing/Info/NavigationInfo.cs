using UnityEngine;

namespace Assembler.Parsing.Info
{
	/// <summary>Which world axes the planar walkability grid spans: <c>XY</c> (default) or <c>XZ</c> (ground
	/// plane). The remaining axis is the off-plane axis, preserved as-is when projecting world points.</summary>
	public enum NavPlane
	{
		XY,
		XZ
	}

	/// <summary>Projects world-space <see cref="Vector3"/>s onto a <see cref="NavPlane"/>'s two in-plane axes
	/// and back, so the nav grid and pathfinder stay plane-agnostic and only this mapping knows the plane.</summary>
	public static class NavPlaneExtensions
	{
		/// <summary>The two in-plane components of a world point (the pair fed to the grid).</summary>
		public static (float U, float V) Project(this NavPlane plane, Vector3 world) =>
			plane == NavPlane.XZ ? (world.x, world.z) : (world.x, world.y);

		/// <summary>A world point from in-plane coordinates, taking the off-plane component from
		/// <paramref name="reference"/> (e.g. the goal's height) so projected points keep a sensible depth.</summary>
		public static Vector3 ToWorld(this NavPlane plane, float u, float v, Vector3 reference) =>
			plane == NavPlane.XZ ? new Vector3(u, reference.y, v) : new Vector3(u, v, reference.z);

		/// <summary>A world-space direction from an in-plane direction, with the off-plane component zero.</summary>
		public static Vector3 ToWorldDirection(this NavPlane plane, float u, float v) =>
			plane == NavPlane.XZ ? new Vector3(u, 0f, v) : new Vector3(u, v, 0f);
	}

	/// <summary>
	/// Grid-navigation configuration from the descriptor's <c>Navigation:</c> section: the world bounds the
	/// walkability grid spans (<c>Min*</c>/<c>Max*</c> are the two in-<see cref="Plane"/> axes, not literally
	/// X and Y), its cell size, the entity tag whose colliders are rasterized as obstacles, which plane the grid
	/// lies in, whether searches may step diagonally, and the <em>default</em> agent radius (world units) by which
	/// obstacles are inflated so paths keep clearance — <see cref="AgentRadius"/> defaults to <c>0</c> (no
	/// inflation) and applies to any agent that doesn't set its own radius. <see cref="Default"/> is used when no
	/// section is present.
	/// </summary>
	public sealed record NavigationInfo(
		float CellSize,
		float MinX,
		float MinY,
		float MaxX,
		float MaxY,
		string ObstacleTag,
		NavPlane Plane,
		bool AllowDiagonal,
		float AgentRadius)
	{
		public static NavigationInfo Default => new(1f, -50f, -50f, 50f, 50f, "obstacle", NavPlane.XY, true, 0f);
	}
}
