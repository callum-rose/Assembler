using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Voxels;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// The reference visual hull: the (dilated) silhouette masks of every supplied
	/// axis, against an arbitrary frame. A world voxel is <em>inside</em> the hull
	/// iff every supplied-axis mask says solid where the voxel projects; an axis
	/// with no silhouette imposes no constraint, so a front-only reference never
	/// constrains depth. The frame (origin + size) is supplied by the caller so the
	/// same hull can be tested against the composed bounding box (B1's post-compose
	/// clip) or the stable target bounding box (B2's forward per-part bound).
	///
	/// Pure: no I/O, no clock, no RNG — exact-assertion testable.
	/// </summary>
	public sealed class SilhouetteHull
	{
		private readonly IReadOnlyList<FaceMask> _masks;

		private SilhouetteHull(IReadOnlyList<FaceMask> masks) => _masks = masks;

		/// <summary>True when no supplied axis carries a silhouette — the hull constrains nothing.</summary>
		public bool IsEmpty => _masks.Count == 0;

		public static SilhouetteHull Build(ReferenceBrief brief, int dilation)
		{
			var masks = new List<FaceMask>();
			foreach (var spec in brief.Silhouettes)
			{
				if (spec.IsEmpty || spec.Size.x <= 0 || spec.Size.y <= 0)
				{
					continue;
				}

				masks.Add(FaceMask.Build(spec, dilation));
			}

			return new SilhouetteHull(masks);
		}

		/// <summary>True when the world voxel projects onto a solid cell of EVERY supplied-axis mask, within the given frame.</summary>
		public bool Contains(Vector3Int world, Vector3Int frameMin, Vector3Int frameSize)
		{
			var local = world - frameMin;
			foreach (var mask in _masks)
			{
				if (!mask.SolidAt(local, frameSize))
				{
					return false;
				}
			}

			return true;
		}

		/// <summary>The complement of <see cref="Contains"/>: outside when any supplied axis projects onto a non-solid cell.</summary>
		public bool Outside(Vector3Int world, Vector3Int frameMin, Vector3Int frameSize) =>
			!Contains(world, frameMin, frameSize);
	}

	/// <summary>
	/// Shared per-part classification engine for the silhouette hull. Both the
	/// post-compose backstop (<see cref="HullClip"/>, B1) and the forward per-part
	/// bound (<see cref="ForwardHullBound"/>, B2) feed it the same per-voxel
	/// <c>outside</c> predicate (they differ only in the frame the predicate maps
	/// through); the tiering, connectivity guard, survivor application and global
	/// floor are identical. Pure and stable-iteration so callers stay exact-testable.
	/// </summary>
	internal static class HullClassifier
	{
		public static ClipResult Apply(
			IReadOnlyList<AssembledPart> parts, Func<Vector3Int, bool> outside, HullClipSettings settings)
		{
			// First pass: per-part survivors + tier, plus the aggregate the clip would
			// remove (severe parts included — a wildly-misplaced part should still
			// trip the floor) for the global-floor judgement.
			var classified = parts.Select(part => Classify(part, outside, settings)).ToList();
			var totalVoxels = classified.Sum(c => c.Total);
			var totalOutside = classified.Sum(c => c.Outside);
			var removedFraction = totalVoxels == 0 ? 0f : (float)totalOutside / totalVoxels;

			if (removedFraction > settings.GlobalFloor)
			{
				return new ClipResult(parts, Array.Empty<ClipIssue>(), true, removedFraction);
			}

			var resultParts = new List<AssembledPart>(parts.Count);
			var issues = new List<ClipIssue>();
			var changed = false;
			foreach (var c in classified)
			{
				switch (c.Tier)
				{
					case ClipTier.Light when c.Outside > 0:
						resultParts.Add(ApplySurvivors(c.Part, c.Survivors));
						changed = true;
						break;

					case ClipTier.Moderate:
						resultParts.Add(ApplySurvivors(c.Part, c.Survivors));
						issues.Add(new ClipIssue(c.Part.Part.Id, ClipTier.Moderate, c.Ratio, ClipSevereReason.None));
						changed = true;
						break;

					case ClipTier.Severe:
						// Refuse: keep the authored geometry untouched, flag for re-plan.
						resultParts.Add(c.Part);
						issues.Add(new ClipIssue(c.Part.Part.Id, ClipTier.Severe, c.Ratio, c.Reason));
						break;

					default:
						resultParts.Add(c.Part);
						break;
				}
			}

			return new ClipResult(changed ? resultParts : parts, issues, false, removedFraction);
		}

		private sealed record PartClip(
			AssembledPart Part,
			Dictionary<Vector3Int, byte> Survivors,
			int Total,
			int Outside,
			float Ratio,
			ClipTier Tier,
			ClipSevereReason Reason);

		private static PartClip Classify(AssembledPart part, Func<Vector3Int, bool> outside, HullClipSettings settings)
		{
			var voxels = part.Grid.Voxels;
			var total = voxels.Count;
			if (total == 0)
			{
				return new PartClip(part, new Dictionary<Vector3Int, byte>(), 0, 0, 0f, ClipTier.Light, ClipSevereReason.None);
			}

			// Survivors are kept in PART-LOCAL space (connectivity is translation
			// invariant, and applying back only needs local coords).
			var survivors = new Dictionary<Vector3Int, byte>(voxels.Count);
			var removed = 0;
			foreach (var kv in voxels)
			{
				if (outside(kv.Key + part.WorldPivot))
				{
					removed++;
				}
				else
				{
					survivors[kv.Key] = kv.Value;
				}
			}

			var ratio = (float)removed / total;

			if (survivors.Count == 0)
			{
				return new PartClip(part, survivors, total, removed, ratio, ClipTier.Severe, ClipSevereReason.FullRemoval);
			}

			// Loose parts (foliage, scatter) are allowed to fragment, so they skip the
			// disconnection guard.
			if (!part.Part.Loose && ConnectedComponents(survivors.Keys) > 1)
			{
				return new PartClip(part, survivors, total, removed, ratio, ClipTier.Severe, ClipSevereReason.Disconnection);
			}

			var tier = ratio >= settings.SevereRatio ? ClipTier.Severe
				: ratio >= settings.ModerateRatio ? ClipTier.Moderate
				: ClipTier.Light;
			var reason = tier == ClipTier.Severe ? ClipSevereReason.Ratio : ClipSevereReason.None;
			return new PartClip(part, survivors, total, removed, ratio, tier, reason);
		}

		private static AssembledPart ApplySurvivors(AssembledPart part, Dictionary<Vector3Int, byte> survivors)
		{
			var min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
			var max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
			foreach (var p in survivors.Keys)
			{
				min = Vector3Int.Min(min, p);
				max = Vector3Int.Max(max, p);
			}

			return part with { Grid = new VoxelModel(survivors, part.Grid.Palette, min, max) };
		}

		private static int ConnectedComponents(IEnumerable<Vector3Int> cells)
		{
			var remaining = new HashSet<Vector3Int>(cells);
			var components = 0;
			var stack = new Stack<Vector3Int>();
			while (remaining.Count > 0)
			{
				components++;
				var seed = remaining.First();
				remaining.Remove(seed);
				stack.Push(seed);
				while (stack.Count > 0)
				{
					var p = stack.Pop();
					Visit(p + Vector3Int.right);
					Visit(p + Vector3Int.left);
					Visit(p + Vector3Int.up);
					Visit(p + Vector3Int.down);
					Visit(p + new Vector3Int(0, 0, 1));
					Visit(p + new Vector3Int(0, 0, -1));
				}
			}

			return components;

			void Visit(Vector3Int p)
			{
				if (remaining.Remove(p))
				{
					stack.Push(p);
				}
			}
		}
	}

	/// <summary>
	/// One axis's dilated solid mask in silhouette (u, v bottom-origin)
	/// resolution. A world voxel is tested by projecting it onto this face and
	/// nearest-mapping the projection cell back into silhouette resolution — the
	/// inverse of the resampling <see cref="SilhouetteMatcher.Iou"/> performs.
	/// </summary>
	public sealed class FaceMask
	{
		private readonly ProjectionFace _face;
		private readonly bool[,] _solid;
		private readonly int _width;
		private readonly int _height;

		private FaceMask(ProjectionFace face, bool[,] solid, int width, int height)
		{
			_face = face;
			_solid = solid;
			_width = width;
			_height = height;
		}

		public static FaceMask Build(SilhouetteSpec spec, int dilation)
		{
			var face = VoxelProjector.ParseFace(spec.Face);
			var width = spec.Size.x;
			var height = spec.Size.y;
			var solid = new bool[width, height];
			for (var v = 0; v < height; v++)
			{
				// Rows are image-style (top first); v = 0 is the bottom, matching the
				// projection's bottom-origin v. This is the same flip Iou uses.
				var row = height - 1 - v < spec.Rows.Count ? spec.Rows[height - 1 - v] : string.Empty;
				for (var u = 0; u < width; u++)
				{
					solid[u, v] = u < row.Length && SilhouetteSpec.IsSolid(row[u]);
				}
			}

			return new FaceMask(face, Dilate(solid, width, height, dilation), width, height);
		}

		public bool SolidAt(Vector3Int local, Vector3Int frameSize)
		{
			var (pu, pv, _) = VoxelProjector.MapToPlane(local, frameSize, _face);
			var (projWidth, projHeight) = VoxelProjector.Dimensions(frameSize, _face);
			var u = Mathf.Clamp(Mathf.FloorToInt((pu + 0.5f) * _width / projWidth), 0, _width - 1);
			var v = Mathf.Clamp(Mathf.FloorToInt((pv + 0.5f) * _height / projHeight), 0, _height - 1);
			return _solid[u, v];
		}

		private static bool[,] Dilate(bool[,] mask, int width, int height, int amount)
		{
			for (var step = 0; step < amount; step++)
			{
				var next = new bool[width, height];
				for (var u = 0; u < width; u++)
				{
					for (var v = 0; v < height; v++)
					{
						next[u, v] = mask[u, v] || AnySolidNeighbour(mask, width, height, u, v);
					}
				}

				mask = next;
			}

			return mask;
		}

		private static bool AnySolidNeighbour(bool[,] mask, int width, int height, int u, int v)
		{
			for (var du = -1; du <= 1; du++)
			{
				for (var dv = -1; dv <= 1; dv++)
				{
					if (du == 0 && dv == 0)
					{
						continue;
					}

					var nu = u + du;
					var nv = v + dv;
					if (nu >= 0 && nu < width && nv >= 0 && nv < height && mask[nu, nv])
					{
						return true;
					}
				}
			}

			return false;
		}
	}
}
