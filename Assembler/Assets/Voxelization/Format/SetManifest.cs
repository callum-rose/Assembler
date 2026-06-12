using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// One asset row in a set manifest. <see cref="Reference"/> names an
	/// optional reference image (resolved via IReferenceImageSource); empty
	/// means none. <see cref="Symmetry"/> is a hint for the planner:
	/// "bilateral", "radial:N", or "none".
	/// </summary>
	public sealed record ManifestAsset
	{
		public string Id { get; init; } = string.Empty;
		public float RealWorldHeight { get; init; }
		public string Symmetry { get; init; } = "none";
		public bool Rig { get; init; }
		public string Reference { get; init; } = string.Empty;

		public bool HasReference => Reference.Length > 0;
	}

	/// <summary>
	/// The set manifest / scale bible (*.manifest.yaml): the assets to generate,
	/// each anchored to a height so scale consistency across the set is
	/// automatic. Generated manifests always use unit 1 with heights expressed
	/// directly in voxels (only relative scale matters); a hand-written
	/// metres-style manifest (unit 0.18, height 1.8) still resolves to the same
	/// voxel counts through <see cref="HeightInVoxels"/>.
	/// </summary>
	public sealed record SetManifest
	{
		public string Game { get; init; } = string.Empty;
		public float Unit { get; init; } = 1f;
		public IReadOnlyList<ManifestAsset> Assets { get; init; } = Array.Empty<ManifestAsset>();

		public int HeightInVoxels(ManifestAsset asset) =>
			Mathf.Max(1, Mathf.RoundToInt(asset.RealWorldHeight / Mathf.Max(1e-6f, Unit)));
	}
}
