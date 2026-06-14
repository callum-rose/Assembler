using System.Collections.Generic;
using System.Linq;
using Assembler.Voxels;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Pure-code Stage 4 checks: composed scale vs the manifest target, joint
	/// connectivity (no floating chunks within a part; every child touches its
	/// parent), palette legality, declared size vs built bounds, and — when a
	/// reference brief is present — palette and silhouette match against it.
	/// </summary>
	public sealed class ModelValidator
	{
		private readonly float _silhouetteIouThreshold;
		private readonly float _symmetryIouThreshold;

		public ModelValidator(float silhouetteIouThreshold = 0.75f, float symmetryIouThreshold = 0.97f)
		{
			_silhouetteIouThreshold = silhouetteIouThreshold;
			_symmetryIouThreshold = symmetryIouThreshold;
		}

		public ValidationReport Validate(AssembledModel assembled, ReferenceBrief brief, bool checkBriefPalette = true)
		{
			var issues = new List<ValidationIssue>();
			var model = assembled.Model;

			CheckScale(assembled, issues);
			CheckBilateralSymmetry(assembled, issues);
			foreach (var part in assembled.Parts)
			{
				CheckDeclaredSize(part, issues);
				CheckPaletteLegality(model, part, issues);
				if (!part.Part.Loose)
				{
					CheckFloatingChunks(part, issues);
				}

				CheckTouchesParent(assembled, part, issues);
				if (model.IsBilateral && part.Part.Data is not MirrorPartData && part.WorldPivot.x == 0)
				{
					CheckPartMirrorSymmetry(part, issues);
				}
			}

			if (!brief.IsEmpty)
			{
				// A noted refine relaxes the palette lock (the operator's note
				// outranks the brief), but the silhouette gates stay.
				if (checkBriefPalette)
				{
					CheckBriefPalette(model, brief, issues);
				}

				CheckSilhouette(assembled.Composed, brief, issues);
			}

			return new ValidationReport(issues);
		}

		private static void CheckScale(AssembledModel assembled, List<ValidationIssue> issues)
		{
			if (assembled.Composed.Voxels.Count == 0)
			{
				issues.Add(new ValidationIssue(string.Empty, IssueCode.ScaleMismatch, "Model has no voxels."));
				return;
			}

			var model = assembled.Model;
			var size = assembled.Composed.Size;
			var tolerance = Mathf.Max(Mathf.Max(0, model.SizeTolerance), Mathf.RoundToInt(model.TargetHeight * 0.1f));
			CheckExtent(size.y, model.TargetHeight, tolerance, "tall (y)", issues);
			CheckExtent(size.z, model.TargetLength, Mathf.Max(0, model.SizeTolerance), "long (z, the forward axis)", issues);
			CheckExtent(size.x, model.TargetWidth, Mathf.Max(0, model.SizeTolerance), "wide (x)", issues);
		}

		private static void CheckExtent(int actual, int target, int tolerance, string description, List<ValidationIssue> issues)
		{
			if (target > 0 && Mathf.Abs(actual - target) > tolerance)
			{
				issues.Add(new ValidationIssue(string.Empty, IssueCode.ScaleMismatch,
					$"Model is {actual} voxels {description} but the manifest requires {target} (tolerance ±{tolerance})."));
			}
		}

		/// <summary>
		/// Bilateral models must mirror across the centre x plane of their own
		/// bounding box (x' = min.x + max.x − x, which also handles even widths
		/// where the plane falls between cells). Occupancy IoU below the
		/// threshold means geometry was authored on both sides instead of
		/// mirrored, or pivots drifted off the plane.
		/// </summary>
		private void CheckBilateralSymmetry(AssembledModel assembled, List<ValidationIssue> issues)
		{
			if (!assembled.Model.IsBilateral || assembled.Composed.Voxels.Count == 0)
			{
				return;
			}

			var cells = assembled.Composed.Voxels.Keys;
			var occupancy = new HashSet<Vector3Int>(cells);
			var sum = assembled.Composed.Min.x + assembled.Composed.Max.x;
			var intersection = cells.Count(p => occupancy.Contains(new Vector3Int(sum - p.x, p.y, p.z)));
			var union = 2 * occupancy.Count - intersection;
			var iou = union == 0 ? 1f : (float)intersection / union;

			if (iou < _symmetryIouThreshold)
			{
				issues.Add(new ValidationIssue(string.Empty, IssueCode.Asymmetric,
					$"Model is only {iou:P0} mirror-symmetric across its centre x plane (threshold {_symmetryIouThreshold:P0}). " +
					"Author geometry on one side only and declare the other side as mirror parts."));
			}
		}

		/// <summary>
		/// A centre part (one sitting on the bilateral mirror plane) must itself
		/// be mirror-symmetric, colours included, across its own x bbox centre.
		/// Unlike the whole-model check this attributes the issue to the part, so
		/// the targeted re-authoring loop can act on it.
		/// </summary>
		private static void CheckPartMirrorSymmetry(AssembledPart part, List<ValidationIssue> issues)
		{
			var voxels = part.Grid.Voxels;
			if (voxels.Count == 0)
			{
				return;
			}

			var sum = part.Grid.Min.x + part.Grid.Max.x;
			var mismatches = voxels
				.Where(kv => !voxels.TryGetValue(new Vector3Int(sum - kv.Key.x, kv.Key.y, kv.Key.z), out var twin) || twin != kv.Value)
				.Select(kv => kv.Key)
				.OrderBy(p => p.y).ThenBy(p => p.z).ThenBy(p => p.x)
				.ToList();

			if (mismatches.Count == 0)
			{
				return;
			}

			var sample = string.Join(", ", mismatches.Take(5).Select(p => $"[{p.x},{p.y},{p.z}]"));
			issues.Add(new ValidationIssue(part.Part.Id, IssueCode.Asymmetric,
				$"Part sits on the bilateral mirror plane but is not left-right symmetric: {mismatches.Count} cell(s) " +
				$"lack a mirrored twin of the same colour (e.g. {sample}). Mirror every voxel and colour across the grid's x centre."));
		}

		private static void CheckDeclaredSize(AssembledPart part, List<ValidationIssue> issues)
		{
			var (size, offset) = part.Part.Data switch
			{
				LayersPartData layers => (layers.Size, layers.Offset),
				ScriptPartData script => (script.Size, script.Offset),
				PrimitivesPartData primitives => (primitives.Size, primitives.Offset),
				_ => (Vector3Int.zero, Vector3Int.zero),
			};

			if (size == Vector3Int.zero || part.Grid.Voxels.Count == 0)
			{
				return;
			}

			var min = part.Grid.Min;
			var max = part.Grid.Max;
			var limit = offset + size - Vector3Int.one;
			if (min.x < offset.x || min.y < offset.y || min.z < offset.z ||
				max.x > limit.x || max.y > limit.y || max.z > limit.z)
			{
				issues.Add(new ValidationIssue(part.Part.Id, IssueCode.SizeExceeded,
					$"Built bounds [{min.x},{min.y},{min.z}]..[{max.x},{max.y},{max.z}] exceed the declared " +
					$"size {size.x}x{size.y}x{size.z} at offset [{offset.x},{offset.y},{offset.z}]."));
			}
		}

		private static void CheckPaletteLegality(VoxelRigModel model, AssembledPart part, List<ValidationIssue> issues)
		{
			var bad = part.Grid.Voxels.Values.Where(i => i < 1 || i > model.Palette.Count).Distinct().ToList();
			if (bad.Count > 0)
			{
				issues.Add(new ValidationIssue(part.Part.Id, IssueCode.PaletteBreach,
					$"Grid references undeclared palette indices: {string.Join(", ", bad)}."));
			}
		}

		private static void CheckFloatingChunks(AssembledPart part, List<ValidationIssue> issues)
		{
			var components = CountConnectedComponents(part.Grid.Voxels.Keys);
			if (components > 1)
			{
				issues.Add(new ValidationIssue(part.Part.Id, IssueCode.FloatingChunk,
					$"Part geometry splits into {components} disconnected chunks; it must be one connected volume."));
			}
		}

		private static void CheckTouchesParent(AssembledModel assembled, AssembledPart part, List<ValidationIssue> issues)
		{
			if (part.Part.Parent == VoxelRigModel.RootId || part.Grid.Voxels.Count == 0)
			{
				return;
			}

			var parent = assembled.FindPart(part.Part.Parent);
			if (parent == null || parent.Grid.Voxels.Count == 0)
			{
				return;
			}

			var parentWorld = new HashSet<Vector3Int>(parent.WorldVoxels.Select(kv => kv.Key));
			var touches = part.WorldVoxels.Select(kv => kv.Key).Any(p =>
				parentWorld.Contains(p) ||
				parentWorld.Contains(p + Vector3Int.right) || parentWorld.Contains(p + Vector3Int.left) ||
				parentWorld.Contains(p + Vector3Int.up) || parentWorld.Contains(p + Vector3Int.down) ||
				parentWorld.Contains(p + new Vector3Int(0, 0, 1)) || parentWorld.Contains(p + new Vector3Int(0, 0, -1)));

			if (!touches)
			{
				issues.Add(new ValidationIssue(part.Part.Id, IssueCode.DisconnectedPart,
					$"Part does not touch its parent '{part.Part.Parent}' — adjust its pivot or extend its geometry to the joint."));
			}
		}

		private static void CheckBriefPalette(VoxelRigModel model, ReferenceBrief brief, List<ValidationIssue> issues)
		{
			if (brief.Palette.Count == 0)
			{
				return;
			}

			var allowed = new HashSet<int>(brief.Palette.Select(e => ColourKey(e.Colour)));
			var rogue = model.Palette
				.Where(e => !allowed.Contains(ColourKey(e.Colour)))
				.Select(e => e.ToHex())
				.ToList();

			if (rogue.Count > 0)
			{
				issues.Add(new ValidationIssue(string.Empty, IssueCode.PaletteMismatch,
					$"Model palette contains colours not in the reference brief: {string.Join(", ", rogue)}."));
			}
		}

		/// <summary>
		/// Face-aware IoU gate: every silhouette in the brief is checked against the
		/// model's projection of that face, one issue per failing face. This is the
		/// sole enforcement for top/bottom (which skip the plan-stage pre-check).
		/// </summary>
		private void CheckSilhouette(VoxelModel composed, ReferenceBrief brief, List<ValidationIssue> issues)
		{
			if (composed.Voxels.Count == 0)
			{
				return;
			}

			foreach (var silhouette in brief.Silhouettes)
			{
				if (silhouette.IsEmpty)
				{
					continue;
				}

				var face = VoxelProjector.ParseFace(silhouette.Face);
				var projection = VoxelProjector.Occupancy(composed, face);
				var iou = SilhouetteMatcher.Iou(projection, silhouette);
				if (iou < _silhouetteIouThreshold)
				{
					issues.Add(new ValidationIssue(string.Empty, IssueCode.SilhouetteMismatch,
						$"{silhouette.Face} silhouette IoU {iou:0.00} is below the {_silhouetteIouThreshold:0.00} threshold."));
				}
			}
		}

		private static int CountConnectedComponents(IEnumerable<Vector3Int> cells)
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

		private static int ColourKey(Color32 c) => (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
	}
}
