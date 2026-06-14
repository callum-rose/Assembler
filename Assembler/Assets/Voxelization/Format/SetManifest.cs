using System;
using System.Collections.Generic;

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

		/// <summary>
		/// Binding per-asset theming distilled from the game brief (colours,
		/// materials, style, distinguishing features). Downstream stages must
		/// match it, inventing only where it is silent.
		/// </summary>
		public string Description { get; init; } = string.Empty;

		/// <summary>Bounding-box extent UP (y), in voxels.</summary>
		public int Height { get; init; }

		/// <summary>Bounding-box extent along z, the model's FORWARD axis (a car's nose-to-tail length), in voxels. 0 = unconstrained.</summary>
		public int Length { get; init; }

		/// <summary>Bounding-box extent along x, the model's left-right axis (a car's track width), in voxels. 0 = unconstrained.</summary>
		public int Width { get; init; }

		/// <summary>How strictly the bounding box is enforced: each specified extent must match within ± this many voxels.</summary>
		public int Tolerance { get; init; } = 1;

		public string Symmetry { get; init; } = "none";
		public bool Rig { get; init; }
		public string Reference { get; init; } = string.Empty;

		public bool HasReference => Reference.Length > 0;
	}

	/// <summary>
	/// The set manifest / scale bible (*.manifest.yaml): the assets to generate,
	/// each anchored to a bounding box (height/length/width in voxels) so scale
	/// consistency across the set is automatic — only relative scale matters.
	/// </summary>
	public sealed record SetManifest
	{
		public string Game { get; init; } = string.Empty;
		public IReadOnlyList<ManifestAsset> Assets { get; init; } = Array.Empty<ManifestAsset>();
	}
}
