using System;
using System.Collections.Generic;
using System.Linq;

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

		private static readonly Dictionary<string, int> FaceIndices =
			Faces.Select((face, index) => (face, index)).ToDictionary(t => t.face, t => t.index);

		public static bool IsValidFace(string face) => FaceIndices.ContainsKey(face.ToLowerInvariant());

		/// <summary>Position of a face within <see cref="Faces"/> in constant time, or -1 if not a valid face.</summary>
		public static int FaceIndex(string face) => FaceIndices.TryGetValue(face, out var index) ? index : -1;
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
		public IReadOnlyList<ReferenceImage> References { get; init; } = Array.Empty<ReferenceImage>();

		public bool HasReference => References.Count > 0;
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
