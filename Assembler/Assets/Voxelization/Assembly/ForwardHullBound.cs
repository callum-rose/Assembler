using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// B2 forward per-part hull bound: a hard, non-destructive cap applied to the
	/// resolved part list <em>after</em> world pivots are known and <em>before</em>
	/// composition. Each part's geometry is clipped to the reference visual hull at
	/// its own world position, against the stable <see cref="PlanGeometryChecks.TargetFrame"/>
	/// (the target bounding box the silhouettes are validated against) rather than
	/// the composed bounds — so the bound is a pure function of (authored geometry +
	/// brief), reproducible from <c>vmodel.yaml + reference_brief.yaml</c>, and
	/// cannot compound across views the way B1's whole-model post-compose clip can.
	///
	/// Reuses B1's tier / connectivity / global-floor logic verbatim via
	/// <see cref="HullClassifier"/>: light overflow trims silently, moderate trims
	/// and flags a re-author, severe (ratio / full-removal / disconnection) keeps
	/// the authored geometry and flags a re-plan, and a hull that would eat more
	/// than the global floor of the model's mass is discarded wholesale. Loose parts
	/// skip the disconnection guard (B1 parity).
	///
	/// Pure: no I/O, no clock, no RNG — exact-assertion testable.
	/// </summary>
	public static class ForwardHullBound
	{
		public static ClipResult Apply(
			IReadOnlyList<AssembledPart> parts, VoxelRigModel model, ReferenceBrief brief, HullClipSettings settings)
		{
			var hull = SilhouetteHull.Build(brief, settings.Dilation);
			if (hull.IsEmpty)
			{
				return new ClipResult(parts, Array.Empty<ClipIssue>(), false, 0f);
			}

			// The target bounding box is known up front and stable across rounds, so
			// author mask, this enforcement clip and the plan-time coverage gate all
			// project through the exact same frame.
			var (frameMin, frameSize) = PlanGeometryChecks.TargetFrame(model);
			if (frameSize == Vector3Int.zero)
			{
				return new ClipResult(parts, Array.Empty<ClipIssue>(), false, 0f);
			}

			return HullClassifier.Apply(parts, world => hull.Outside(world, frameMin, frameSize), settings);
		}
	}
}
