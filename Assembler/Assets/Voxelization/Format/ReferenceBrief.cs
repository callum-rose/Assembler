using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>Real-world dimensions extracted from a reference image, in metres.</summary>
	public sealed record RealWorldDims(float Height, float Width, float Depth)
	{
		public static RealWorldDims None { get; } = new(0f, 0f, 0f);
	}

	/// <summary>
	/// An orthographic occupancy mask used as the automated validation oracle.
	/// <see cref="Face"/> names the projection (front = looking along -z,
	/// side = looking along -x, top = looking down -y). Rows are listed
	/// image-style, top row first; '#' is solid, '.' is empty.
	/// </summary>
	public sealed record SilhouetteSpec(string Face, Vector3Int Size, IReadOnlyList<string> Rows)
	{
		public static SilhouetteSpec None { get; } = new(string.Empty, Vector3Int.zero, Array.Empty<string>());

		public bool IsEmpty => Rows.Count == 0;
	}

	/// <summary>
	/// Structured output of the single vision call over a reference image
	/// (Stage 1). When present it is authoritative: the palette is locked
	/// downstream, and proportions/silhouette drive validation.
	/// </summary>
	public sealed record ReferenceBrief
	{
		public string Source { get; init; } = string.Empty;
		public RealWorldDims RealWorldDims { get; init; } = RealWorldDims.None;
		public IReadOnlyList<PaletteEntry> Palette { get; init; } = Array.Empty<PaletteEntry>();
		public IReadOnlyDictionary<string, float> Proportions { get; init; } = new Dictionary<string, float>();
		public IReadOnlyList<string> SignatureFeatures { get; init; } = Array.Empty<string>();
		public SilhouetteSpec Silhouette { get; init; } = SilhouetteSpec.None;

		public static ReferenceBrief None { get; } = new();

		public bool IsEmpty => Source.Length == 0 && Palette.Count == 0 && Silhouette.IsEmpty;
	}
}
