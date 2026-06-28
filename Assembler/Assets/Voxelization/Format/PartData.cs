using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Voxelization
{
	public enum PartEncoding
	{
		Layers,
		Script,
		Primitives,
		Mirror,
		Copy,
		Planned,
	}

	public enum MirrorAxis
	{
		X,
		Y,
		Z,
	}

	/// <summary>
	/// Geometry payload of a <see cref="VoxelPart"/>. All coordinates are
	/// Claude-facing Y-up, part-local: a part's local origin sits at its pivot,
	/// and <c>Offset</c> places grid cell (0,0,0) in that local frame, so
	/// geometry can extend to negative coordinates around the joint.
	/// </summary>
	public abstract record PartData
	{
		public abstract PartEncoding Encoding { get; }
	}

	/// <summary>
	/// Stacked 2D ASCII slices, bottom-to-top along +y. Layer index == local y;
	/// each layer holds Size.z rows (row 0 == z=0, the front) of Size.x
	/// characters (left-to-right == x ascending). Cells are palette keys, with
	/// '.' (or '_') meaning empty.
	/// </summary>
	public sealed record LayersPartData(Vector3Int Size, Vector3Int Offset, IReadOnlyList<string> Layers) : PartData
	{
		public override PartEncoding Encoding => PartEncoding.Layers;
	}

	/// <summary>
	/// A VoxelBuilder C# method body (compiled by ExpressionMethodCompiler,
	/// bound to <c>b</c>, ending <c>return b.Build();</c>). The built geometry
	/// must stay within [Offset, Offset + Size). Scripts must be deterministic —
	/// literal seeds/values only, no RNG — so re-runs reproduce the part.
	/// </summary>
	public sealed record ScriptPartData(Vector3Int Size, Vector3Int Offset, string Source) : PartData
	{
		public override PartEncoding Encoding => PartEncoding.Script;
	}

	/// <summary>
	/// Declarative solid shapes (box / sphere / cylinder, with rounding and
	/// half-clips), one per line, rasterized deterministically by
	/// <see cref="PrimitivesCodec"/>. Cheaper and more reliable than a script
	/// for geometric parts — "box 0 0 0 5 5 5" instead of code that builds one.
	/// Shape coordinates are grid cells; the grid is placed at
	/// <paramref name="Offset"/> like the other encodings.
	/// </summary>
	public sealed record PrimitivesPartData(Vector3Int Size, Vector3Int Offset, IReadOnlyList<string> Shapes) : PartData
	{
		public override PartEncoding Encoding => PartEncoding.Primitives;
	}

	/// <summary>
	/// A free copy of a sibling part, reflected through the parent's local
	/// origin plane perpendicular to <see cref="Axis"/>: geometry maps p → -p on
	/// that axis component (point reflection through the part's own pivot), and
	/// an omitted pivot is derived by negating the source pivot's component.
	/// </summary>
	public sealed record MirrorPartData(string Source, MirrorAxis Axis) : PartData
	{
		public override PartEncoding Encoding => PartEncoding.Mirror;
	}

	/// <summary>
	/// A prefab-like reuse of a sibling part: the source's geometry verbatim,
	/// positioned by this part's own pivot. Four identical wheels author once —
	/// one authored, one mirror, and copies (or mirrors of copies) for the rear
	/// pair. Unlike <see cref="MirrorPartData"/> nothing is reflected, so the
	/// pivot must be declared explicitly.
	/// </summary>
	public sealed record CopyPartData(string Source) : PartData
	{
		public override PartEncoding Encoding => PartEncoding.Copy;
	}

	/// <summary>
	/// Stage-1 output for a part that has been planned (encoding, bounds,
	/// guidance) but not yet authored. Stage 2 replaces it with
	/// <see cref="LayersPartData"/> or <see cref="ScriptPartData"/>.
	/// </summary>
	public sealed record PlannedPartData(PartEncoding PlannedEncoding, Vector3Int Size, Vector3Int Offset, string Note) : PartData
	{
		public override PartEncoding Encoding => PartEncoding.Planned;
	}
}
