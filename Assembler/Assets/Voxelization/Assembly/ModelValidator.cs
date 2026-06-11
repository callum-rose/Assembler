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

		public ModelValidator(float silhouetteIouThreshold = 0.75f)
		{
			_silhouetteIouThreshold = silhouetteIouThreshold;
		}

		public ValidationReport Validate(AssembledModel assembled, ReferenceBrief brief)
		{
			var issues = new List<ValidationIssue>();
			var model = assembled.Model;

			CheckScale(assembled, issues);
			foreach (var part in assembled.Parts)
			{
				CheckDeclaredSize(part, issues);
				CheckPaletteLegality(model, part, issues);
				CheckFloatingChunks(part, issues);
				CheckTouchesParent(assembled, part, issues);
			}

			if (!brief.IsEmpty)
			{
				CheckBriefPalette(model, brief, issues);
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

			var target = assembled.Model.HeightInVoxels;
			var actual = assembled.Composed.Size.y;
			var tolerance = Mathf.Max(1, Mathf.RoundToInt(target * 0.1f));
			if (Mathf.Abs(actual - target) > tolerance)
			{
				issues.Add(new ValidationIssue(string.Empty, IssueCode.ScaleMismatch,
					$"Model is {actual} voxels tall but the manifest requires {target} " +
					$"({assembled.Model.RealWorldHeight}m at {assembled.Model.Unit}m/voxel, tolerance ±{tolerance})."));
			}
		}

		private static void CheckDeclaredSize(AssembledPart part, List<ValidationIssue> issues)
		{
			var (size, offset) = part.Part.Data switch
			{
				LayersPartData layers => (layers.Size, layers.Offset),
				ScriptPartData script => (script.Size, script.Offset),
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

		private void CheckSilhouette(VoxelModel composed, ReferenceBrief brief, List<ValidationIssue> issues)
		{
			if (brief.Silhouette.IsEmpty || composed.Voxels.Count == 0)
			{
				return;
			}

			var face = VoxelProjector.ParseFace(brief.Silhouette.Face);
			var projection = VoxelProjector.Occupancy(composed, face);
			var iou = SilhouetteMatcher.Iou(projection, brief.Silhouette);
			if (iou < _silhouetteIouThreshold)
			{
				issues.Add(new ValidationIssue(string.Empty, IssueCode.SilhouetteMismatch,
					$"{brief.Silhouette.Face} silhouette IoU {iou:0.00} is below the {_silhouetteIouThreshold:0.00} threshold."));
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
