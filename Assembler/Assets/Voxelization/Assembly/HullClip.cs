using System.Collections.Generic;
using Assembler.Voxels;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>How much of a part the silhouette envelope wanted to remove.</summary>
	public enum ClipTier
	{
		/// <summary>Small overhang — trimmed silently.</summary>
		Light,

		/// <summary>Substantial overhang — trimmed, with a reposition/resize hint.</summary>
		Moderate,

		/// <summary>Too much removed / full removal / disconnection — clip refused, geometry kept.</summary>
		Severe,
	}

	/// <summary>Why a part was refused (only meaningful for <see cref="ClipTier.Severe"/>).</summary>
	public enum ClipSevereReason
	{
		None,
		Ratio,
		FullRemoval,
		Disconnection,
	}

	/// <summary>One per-part outcome the clip wants the orchestrator to act on.</summary>
	public sealed record ClipIssue(string PartId, ClipTier Tier, float Ratio, ClipSevereReason Reason);

	/// <summary>Result of clipping a resolved part list to a reference silhouette envelope.</summary>
	public sealed record ClipResult(
		IReadOnlyList<AssembledPart> Parts,
		IReadOnlyList<ClipIssue> Issues,
		bool HullDiscarded,
		float RemovedFraction);

	/// <summary>Thresholds for the hull clip; see <see cref="VoxelizationConfig"/> for the operator-facing defaults.</summary>
	public sealed record HullClipSettings(int Dilation, float ModerateRatio, float SevereRatio, float GlobalFloor)
	{
		public static HullClipSettings Default { get; } = new(1, 0.20f, 0.50f, 0.30f);
	}

	/// <summary>
	/// Maps a hull <see cref="ClipIssue"/> onto the pipeline's
	/// <see cref="ValidationIssue"/> channel, shared by the forward bound (which
	/// folds its outcomes into the assembly report) and the B1 backstop. Severe →
	/// re-plan (the part's box is wrong for the silhouette); moderate → targeted
	/// re-author (reposition/resize to sit inside the envelope).
	/// </summary>
	public static class ClipIssues
	{
		public static ValidationIssue ToValidationIssue(ClipIssue issue) => issue.Tier == ClipTier.Severe
			? new ValidationIssue(issue.PartId, IssueCode.PartClippedSevere,
				$"Hull clip refused ({issue.Ratio:P0} outside the reference silhouette): {SevereReasonText(issue)}. " +
				"Kept the authored geometry; the part's box is wrong for the silhouette — re-plan its size/position.")
			: new ValidationIssue(issue.PartId, IssueCode.PartClippedModerate,
				$"Hull clip trimmed {issue.Ratio:P0} of the part where it overhung the reference silhouette. " +
				"Reposition or resize it to sit inside the envelope.");

		private static string SevereReasonText(ClipIssue issue) => issue.Reason switch
		{
			ClipSevereReason.FullRemoval => "the part lies entirely outside the silhouette",
			ClipSevereReason.Disconnection => "clipping would split the part into disconnected chunks",
			_ => "too much of the part lies outside the silhouette",
		};
	}

	/// <summary>
	/// Deterministic post-resolve step: trims authored geometry that overhangs the
	/// reference silhouette envelope, so the model can no longer sit outside the
	/// shape and the IoU coverage gate stays honest. A voxel is <em>outside</em>
	/// when, for any supplied axis, it projects onto a non-solid cell of that
	/// axis's (dilated) silhouette mask — axes with no silhouette impose no
	/// constraint, so a front-only reference never clips in depth.
	///
	/// Per part the clip classifies the removal as light (apply silently),
	/// moderate (apply + suggest reposition) or severe (refuse — keep the authored
	/// geometry and ask for a re-plan). A hull that would eat more than the global
	/// floor of the whole model's mass is judged inconsistent and discarded
	/// wholesale, so a bad reference never yields a worse result than no reference.
	///
	/// Pure: no I/O, no clock, no RNG, stable iteration — exact-assertion testable.
	/// </summary>
	public static class HullClip
	{
		public static ClipResult Apply(IReadOnlyList<AssembledPart> parts, ReferenceBrief brief, HullClipSettings settings)
		{
			var hull = SilhouetteHull.Build(brief, settings.Dilation);
			if (hull.IsEmpty)
			{
				return new ClipResult(parts, System.Array.Empty<ClipIssue>(), false, 0f);
			}

			// Classify against the same bounding box the occupancy/IoU gate projects
			// through, so "outside" here means "outside" there.
			var (composedMin, composedSize) = ComposedBounds(parts);
			if (composedSize == Vector3Int.zero)
			{
				return new ClipResult(parts, System.Array.Empty<ClipIssue>(), false, 0f);
			}

			return HullClassifier.Apply(parts, world => hull.Outside(world, composedMin, composedSize), settings);
		}

		/// <summary>Union bounding box of every part's world voxels (zero size when empty).</summary>
		private static (Vector3Int Min, Vector3Int Size) ComposedBounds(IReadOnlyList<AssembledPart> parts)
		{
			var min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
			var max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
			var any = false;
			foreach (var part in parts)
			{
				foreach (var kv in part.Grid.Voxels)
				{
					var world = kv.Key + part.WorldPivot;
					min = Vector3Int.Min(min, world);
					max = Vector3Int.Max(max, world);
					any = true;
				}
			}

			return any ? (min, max - min + Vector3Int.one) : (Vector3Int.zero, Vector3Int.zero);
		}
	}
}
