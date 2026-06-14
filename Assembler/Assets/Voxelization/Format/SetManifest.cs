using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// One labelled reference image attached to an asset: a <see cref="File"/>
	/// (resolved via IReferenceImageSource) and the <see cref="Face"/> it depicts
	/// (one of <see cref="Faces"/>). The label removes the perspective-inference
	/// ambiguity — the pipeline is told each image's face rather than guessing.
	/// </summary>
	public sealed record ReferenceImage(string File, string Face)
	{
		/// <summary>The six valid perspective labels, matching <see cref="SilhouetteSpec.Face"/> strings.</summary>
		public static IReadOnlyList<string> Faces { get; } = new[] { "front", "back", "left", "right", "top", "bottom" };

		public static bool IsValidFace(string face) => Faces.Contains(face.ToLowerInvariant());
	}

	/// <summary>
	/// One asset row in a set manifest. <see cref="References"/> lists the
	/// labelled reference images (resolved via IReferenceImageSource); empty
	/// means none. <see cref="Symmetry"/> is a hint for the planner:
	/// "bilateral", "radial:N", or "none".
	/// </summary>
	public sealed record ManifestAsset
	{
		public string Id { get; init; } = string.Empty;

		/// <summary>
		/// Binding per-asset theming distilled from the game brief (colours,
		/// materials, style, distinguishing features). Downstream stages must
		/// match it, inventing only where it is silent.
		/// </summary>
		public string Description { get; init; } = string.Empty;

		public float RealWorldHeight { get; init; }

		/// <summary>Bounding-box extent along z, the model's FORWARD axis (a car's nose-to-tail length). 0 = unconstrained.</summary>
		public float Length { get; init; }

		/// <summary>Bounding-box extent along x, the model's left-right axis (a car's track width). 0 = unconstrained.</summary>
		public float Width { get; init; }

		/// <summary>How strictly the bounding box is enforced: each specified extent must match within ± this many voxels.</summary>
		public int Tolerance { get; init; } = 1;

		public string Symmetry { get; init; } = "none";
		public bool Rig { get; init; }
		public IReadOnlyList<ReferenceImage> References { get; init; } = Array.Empty<ReferenceImage>();

		public bool HasReference => References.Count > 0;
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

		/// <summary>Target length in voxels (z, forward); 0 = unconstrained.</summary>
		public int LengthInVoxels(ManifestAsset asset) => OptionalVoxels(asset.Length);

		/// <summary>Target width in voxels (x, left-right); 0 = unconstrained.</summary>
		public int WidthInVoxels(ManifestAsset asset) => OptionalVoxels(asset.Width);

		private int OptionalVoxels(float extent) =>
			extent <= 0f ? 0 : Mathf.Max(1, Mathf.RoundToInt(extent / Mathf.Max(1e-6f, Unit)));
	}
}
