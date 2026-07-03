using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Pre-planning sanity check that the manifest's declared bounding box and the
	/// reference image agree on proportions. The manifest is the scale bible (its
	/// pinned extents win on absolute scale) and the reference is the shape oracle;
	/// when an axis the manifest pins disagrees with the image's own aspect by more
	/// than the tolerance, the two inputs are inconsistent. Caught here it is an
	/// instant, actionable failure with a suggested dimension — rather than the
	/// silhouette being silently squashed into the box and the plan then failing the
	/// coverage gate several attempts (and minutes) later with an opaque message.
	/// </summary>
	public static class ReferenceConsistency
	{
		/// <summary>
		/// A human-readable conflict description when a manifest-pinned axis disagrees
		/// with a reference silhouette's aspect beyond <paramref name="aspectTolerance"/>
		/// (a fraction of the pinned extent), or null when every reference is consistent
		/// with the bounding box. Faces whose silhouette axes are not both pinned are
		/// skipped — an unconstrained axis has no declared proportion to contradict.
		/// </summary>
		public static string? ProportionConflict(ManifestAsset asset, ReferenceBrief brief, float aspectTolerance)
		{
			var conflicts = new List<string>();
			foreach (var spec in brief.Silhouettes)
			{
				if (spec.IsEmpty || spec.Size.x <= 0 || spec.Size.y <= 0)
				{
					continue;
				}

				var (uExtent, uName) = HorizontalExtent(asset, spec.Face);
				var (vExtent, vName) = VerticalExtent(asset, spec.Face);
				if (uExtent <= 0 || vExtent <= 0)
				{
					continue;
				}

				// The u-voxels the image implies, holding the pinned vertical extent: an
				// aspect-preserving read, so it is independent of the silhouette's own
				// cell resolution.
				var impliedU = Mathf.RoundToInt((float)spec.Size.x / spec.Size.y * vExtent);
				var tolerance = Mathf.Max(asset.Tolerance, Mathf.RoundToInt(uExtent * aspectTolerance));
				if (Mathf.Abs(impliedU - uExtent) > tolerance)
				{
					conflicts.Add(
						$"- {spec.Face} view: the image is {spec.Size.x}x{spec.Size.y}, implying {uName} ≈ {impliedU} " +
						$"at {vName} {vExtent}, but the manifest pins {uName} {uExtent} (±{tolerance}).");
				}
			}

			return conflicts.Count == 0
				? null
				: $"Asset '{asset.Id}': the reference image(s) and the manifest bounding box disagree on proportions:\n" +
				  string.Join("\n", conflicts) + "\n" +
				  "Reconcile before planning: set the manifest dimension to the image-implied value (trust the image), " +
				  "or supply a reference whose proportions match the manifest. (Raise VoxelizationConfig." +
				  nameof(VoxelizationConfig.ReferenceAspectTolerance) + " to relax this check.)";
		}

		/// <summary>Horizontal silhouette axis (u): z (length) for left/right, x (width) otherwise.</summary>
		private static (int Extent, string Name) HorizontalExtent(ManifestAsset asset, string face) =>
			ProjectionFaceInfo.HorizontalAxis(face) == 2
				? (asset.Length, "length")
				: (asset.Width, "width");

		/// <summary>Vertical silhouette axis (v): y (height) for the ground-anchored faces, z (length) for top/bottom.</summary>
		private static (int Extent, string Name) VerticalExtent(ManifestAsset asset, string face) =>
			ProjectionFaceInfo.IsGroundAnchored(face)
				? (asset.Height, "height")
				: (asset.Length, "length");
	}
}
