using System.Collections.Generic;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Single source of truth for how each labelled reference face maps to
	/// projection geometry. The face→axis relationship was previously re-encoded
	/// in the brief extractor (co-axial dedup), the plan-stage feasibility check
	/// (which axis, which faces are ground-anchored), and the bilateral
	/// symmetrisation gate; centralising it here keeps those in lock-step when a
	/// face is added or its mapping changes.
	/// </summary>
	public static class ProjectionFaceInfo
	{
		/// <summary>
		/// The three projection planes as (canonical, twin) pairs: front/back share
		/// the x-y plane, right/left share z-y, top/bottom share x-z. Co-axial twins
		/// encode the same silhouette constraint, so dedup keeps the canonical when
		/// present and the twin only when it is the sole image of that plane.
		/// </summary>
		public static IReadOnlyList<(string Canonical, string Twin)> CoAxialPairs { get; } = new[]
		{
			("front", "back"),
			("right", "left"),
			("top", "bottom"),
		};

		/// <summary>The model-space axis a face's silhouette horizontal (u) maps to: 0 = x (front/back/top/bottom), 2 = z (left/right).</summary>
		public static int HorizontalAxis(string face) => face is "left" or "right" ? 2 : 0;

		/// <summary>
		/// Ground-anchored faces (silhouette v runs along y) get the plan-stage
		/// feasibility pre-check; top/bottom have no height anchor and are enforced
		/// solely by the validator's IoU gate.
		/// </summary>
		public static bool IsGroundAnchored(string face) => face is "front" or "back" or "left" or "right";

		/// <summary>
		/// Faces whose horizontal axis is x, so bilateral (mirror-across-x)
		/// symmetrisation applies to their silhouette. Left/right are front-back in z
		/// and must NOT be x-mirrored.
		/// </summary>
		public static bool IsXHorizontal(string face) => HorizontalAxis(face) == 0;
	}
}
