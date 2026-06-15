using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Deterministic plan-stage geometry checks. A bilateral model whose centre
	/// parts have even widths or off-centre offsets, or whose side parts lack a
	/// mirror twin, can NEVER assemble symmetrically — no amount of re-authoring
	/// fixes a doomed skeleton, so the planner is made to fix it before any
	/// authoring spend.
	/// </summary>
	public static class PlanGeometryChecks
	{
		/// <summary>
		/// Errors that make the skeleton structurally broken (declaration order,
		/// wrong overall height) or geometrically incapable of bilateral symmetry.
		/// Empty for sound plans. Messages are written as feedback for the
		/// planning model — every one of these is unfixable by re-authoring, so
		/// it must be caught before any authoring spend.
		/// </summary>
		public static IReadOnlyList<string> Errors(VoxelRigModel skeleton)
		{
			var errors = new List<string>();
			CheckDeclarationOrder(skeleton, errors);
			CheckVerticalExtent(skeleton, errors);

			if (!skeleton.IsBilateral)
			{
				return errors;
			}

			var xMirrors = skeleton.Parts
				.Where(p => p.Data is MirrorPartData { Axis: MirrorAxis.X })
				.ToLookup(p => ((MirrorPartData)p.Data).Source);

			foreach (var part in skeleton.Parts)
			{
				switch (part.Data)
				{
					case MirrorPartData mirror:
						CheckMirror(skeleton, part, mirror, errors);
						break;
					default:
						CheckAuthored(skeleton, part, xMirrors, errors);
						break;
				}
			}

			return errors;
		}

		/// <summary>The hierarchy is built in declaration order: parents and mirror sources must come first.</summary>
		private static void CheckDeclarationOrder(VoxelRigModel skeleton, List<string> errors)
		{
			var seen = new HashSet<string>();
			foreach (var part in skeleton.Parts)
			{
				if (part.Parent != VoxelRigModel.RootId && !seen.Contains(part.Parent))
				{
					errors.Add(skeleton.FindPart(part.Parent) == null
						? $"Part '{part.Id}' has unknown parent '{part.Parent}'."
						: $"Part '{part.Id}' is declared before its parent '{part.Parent}' — declare parents first; " +
						  "the hierarchy is built in declaration order.");
				}

				if (part.Data is MirrorPartData mirror && !seen.Contains(mirror.Source))
				{
					errors.Add($"Mirror part '{part.Id}' must be declared after its source '{mirror.Source}'.");
				}

				if (part.Data is CopyPartData copy && !seen.Contains(copy.Source))
				{
					errors.Add($"Copy part '{part.Id}' must be declared after its source '{copy.Source}'.");
				}

				seen.Add(part.Id);
			}
		}

		/// <summary>
		/// The part boxes must span the target bounding box — height (y) always,
		/// length (z, the forward axis) and width (x) when the manifest specifies
		/// them — within the model's size tolerance, with the lowest geometry on
		/// the ground. A wrong-sized plan fails scale validation after the entire
		/// authoring spend, so reject it up front. This is what stops a car
		/// coming out wider than it is long.
		/// </summary>
		private static void CheckVerticalExtent(VoxelRigModel skeleton, List<string> errors)
		{
			var boxes = PartBoxes(skeleton);
			if (boxes.Count == 0)
			{
				return;
			}

			var tolerance = Mathf.Max(0, skeleton.SizeTolerance);
			CheckExtent(boxes, 1, skeleton.TargetHeight, tolerance, "tall (y, up)", errors);
			CheckExtent(boxes, 2, skeleton.TargetLength, tolerance, "long (z, the FORWARD axis, nose-to-tail)", errors);
			CheckExtent(boxes, 0, skeleton.TargetWidth, tolerance, "wide (x, left-right)", errors);

			var minY = boxes.Min(b => b.Min.y);
			if (minY != 0)
			{
				errors.Add($"The lowest part box starts at y={minY}, but the origin is feet_center: the lowest " +
						   "geometry must sit at y=0 (the ground).");
			}
		}

		private static void CheckExtent(
			IReadOnlyList<(Vector3Int Min, Vector3Int Max)> boxes,
			int axis,
			int target,
			int tolerance,
			string description,
			List<string> errors)
		{
			if (target <= 0)
			{
				return;
			}

			var min = boxes.Min(b => b.Min[axis]);
			var max = boxes.Max(b => b.Max[axis]);
			var span = max - min + 1;
			if (Mathf.Abs(span - target) > tolerance)
			{
				var direction = span > target ? "too far apart" : "too close together";
				errors.Add($"The part boxes span {span} voxels {description} but the model must be {target} " +
						   $"(±{tolerance}) — the lowest and highest box on this axis are {direction}. Parts that " +
						   "stack along this axis (e.g. legs under a torso under a head) OVERLAP in world space; size " +
						   "and place them so the full extent from the lowest box to the highest equals the target, " +
						   "rather than summing each part's height.");
			}
		}

		/// <summary>
		/// Box-coverage feasibility against the reference silhouettes: a part's
		/// voxels can never leave its declared box, so any silhouette cell no box
		/// reaches is unfillable by authoring — the plan's shape is wrong (stubby
		/// limbs, too-narrow model) and must be re-planned, not re-authored. Only
		/// the ground-anchored faces (front/back/left/right) are pre-checked here;
		/// top/bottom have no height anchor and are enforced solely by the
		/// validator's face-aware IoU gate one stage later. Fails on the first
		/// infeasible face; returns null when every checked face is feasible.
		/// </summary>
		public static string? SilhouetteFeasibilityError(VoxelRigModel skeleton, ReferenceBrief brief, float coverageThreshold)
		{
			foreach (var spec in brief.Silhouettes)
			{
				if (!ProjectionFaceInfo.IsGroundAnchored(spec.Face))
				{
					continue;
				}

				var error = FeasibilityError(skeleton, spec, coverageThreshold);
				if (error != null)
				{
					return error;
				}
			}

			return null;
		}

		private static string? FeasibilityError(VoxelRigModel skeleton, SilhouetteSpec spec, float coverageThreshold)
		{
			if (spec.IsEmpty || spec.Size.x <= 0 || spec.Size.y <= 0)
			{
				return null;
			}

			var boxes = PartBoxes(skeleton);
			if (boxes.Count == 0)
			{
				return null;
			}

			// v is ground-aligned (y=0 at bottom); u is centre-aligned along the
			// face's horizontal model axis. Coverage (hit/solid over overlaid grids)
			// is invariant under a shared u-flip, so axis selection is all that
			// matters here — true handedness lives in the validator's IoU path.
			var uAxis = ProjectionFaceInfo.HorizontalAxis(spec.Face);
			var height = skeleton.TargetHeight;

			// The union of part boxes, centre-aligned on the horizontal axis and ground-aligned in y.
			var minU = boxes.Min(b => b.Min[uAxis]);
			var maxU = boxes.Max(b => b.Max[uAxis]);
			var minY = boxes.Min(b => b.Min.y);
			var plannedWidth = maxU - minU + 1;

			// When the manifest pins this face's horizontal axis (TargetWidth for
			// front/back's x, TargetLength for left/right's z), the overall span is
			// already enforced by the bounding-box checks, so compare SHAPES in the
			// plan's own frame and drop the redundant width sub-check. The vision
			// model's raw cell count rarely equals the manifest's voxel budget, so
			// resampling to the silhouette's own aspect would make that mismatch
			// (e.g. a 22-cell side read vs a 16-voxel length) unsatisfiable. Only an
			// UNCONSTRAINED axis falls back to the silhouette's aspect, where the
			// width sub-check is the sole proportion signal.
			var uTarget = uAxis == 2 ? skeleton.TargetLength : skeleton.TargetWidth;
			var axisConstrained = uTarget > 0;
			var width = axisConstrained
				? Mathf.Max(1, plannedWidth)
				: Mathf.Max(1, Mathf.RoundToInt((float)spec.Size.x * height / spec.Size.y));

			// The reference mask, resampled into the comparison frame ([u, v], v=0 bottom).
			var target = new bool[width, height];
			for (var u = 0; u < width; u++)
			{
				for (var v = 0; v < height; v++)
				{
					var su = Mathf.Clamp(Mathf.FloorToInt((u + 0.5f) * spec.Size.x / width), 0, spec.Size.x - 1);
					var row = spec.Size.y - 1 - Mathf.Clamp(Mathf.FloorToInt((v + 0.5f) * spec.Size.y / height), 0, spec.Size.y - 1);
					target[u, v] = row < spec.Rows.Count && su < spec.Rows[row].Length && SilhouetteSpec.IsSolid(spec.Rows[row][su]);
				}
			}

			var du = (width - plannedWidth) / 2 - minU;
			var covered = new bool[width, height];
			foreach (var (boxMin, boxMax) in boxes)
			{
				for (var u = boxMin[uAxis]; u <= boxMax[uAxis]; u++)
				{
					for (var y = boxMin.y; y <= boxMax.y; y++)
					{
						var gu = u + du;
						var gv = y - minY;
						if (gu >= 0 && gu < width && gv >= 0 && gv < height)
						{
							covered[gu, gv] = true;
						}
					}
				}
			}

			var solid = CountSolid(target);
			if (solid == 0)
			{
				return null;
			}

			// Coverage is evaluated for BOTH horizontal orientations of the reference,
			// and the better one taken. The plan's box layout is built in raw axis
			// order (u = z for a left/right face), whereas the silhouette follows the
			// projection's handedness convention (u = mirrored z for left/right) — so a
			// perfectly buildable plan can come out the mirror of the silhouette here.
			// True handedness is enforced later by the validator's IoU gate against the
			// correctly oriented projection, so this necessary-condition pre-check must
			// not reject a plan that merely faces the other way along the axis (which
			// would otherwise bounce an asymmetric subject — a dog's nose-vs-tail
			// profile — back and forth between feasibility and the IoU gate forever).
			var mirrored = MirrorU(target, width, height);
			var directHits = CountHits(covered, target, width, height);
			var mirroredHits = CountHits(covered, mirrored, width, height);
			var (hit, comparedTarget) = mirroredHits > directHits ? (mirroredHits, mirrored) : (directHits, target);

			// The silhouette is a vision-model guess, so coverage tolerates blob
			// noise (gaps read as solid). Width is robust to that noise: blobbing
			// doesn't move a silhouette's bounds, so a wrong overall width is a
			// reliable doomed-plan signal — but only when the manifest leaves that
			// axis free; when it pins it, the bounding-box checks own the dimension.
			var coverage = (float)hit / solid;
			var problems = string.Empty;
			if (!axisConstrained && Mathf.Abs(plannedWidth - width) > 1)
			{
				problems += $"- The plan spans {plannedWidth} voxels across the {spec.Face} view, but the reference " +
							$"proportions demand ~{width} at {height} tall.\n";
			}

			if (coverage < coverageThreshold)
			{
				problems += $"- Even if every part completely fills its declared box, the plan covers only " +
							$"{coverage:P0} of the reference {spec.Face} silhouette (needs {coverageThreshold:P0}) — " +
							"limbs too short, or parts misplaced.\n";
			}

			return problems.Length == 0
				? null
				: $"The planned shape cannot match the reference {spec.Face} view:\n{problems}" +
				  $"Compared at {width}x{height} (top row first):\n" +
				  $"PLANNED BOX COVERAGE:\n{RenderMask(covered)}\nREFERENCE SILHOUETTE:\n{RenderMask(comparedTarget)}\n" +
				  "Resize or re-place parts so their boxes reach the silhouette.";
		}

		private static int CountSolid(bool[,] mask)
		{
			var solid = 0;
			foreach (var cell in mask)
			{
				solid += cell ? 1 : 0;
			}

			return solid;
		}

		private static int CountHits(bool[,] covered, bool[,] target, int width, int height)
		{
			var hit = 0;
			for (var u = 0; u < width; u++)
			{
				for (var v = 0; v < height; v++)
				{
					if (target[u, v] && covered[u, v])
					{
						hit++;
					}
				}
			}

			return hit;
		}

		/// <summary>Mirrors a [u, v] occupancy grid along its horizontal (u) axis.</summary>
		private static bool[,] MirrorU(bool[,] mask, int width, int height)
		{
			var mirrored = new bool[width, height];
			for (var u = 0; u < width; u++)
			{
				for (var v = 0; v < height; v++)
				{
					mirrored[u, v] = mask[width - 1 - u, v];
				}
			}

			return mirrored;
		}

		/// <summary>
		/// The model's target bounding box in world cells: the union of every part's
		/// declared world box. This is the frame the forward hull bound and the
		/// authoring mask project through — it is fixed by the plan (re-authoring
		/// does not move boxes), so it stays stable across rounds and matches the
		/// frame the plan-time coverage gate already validates the silhouettes
		/// against. Zero size when the model has no boxes.
		/// </summary>
		public static (Vector3Int Min, Vector3Int Size) TargetFrame(VoxelRigModel model)
		{
			var boxes = PartBoxes(model);
			if (boxes.Count == 0)
			{
				return (Vector3Int.zero, Vector3Int.zero);
			}

			var min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
			var max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
			foreach (var (boxMin, boxMax) in boxes)
			{
				min = Vector3Int.Min(min, boxMin);
				max = Vector3Int.Max(max, boxMax);
			}

			return (min, max - min + Vector3Int.one);
		}

		/// <summary>Pivot accumulated up the parent chain (the part's local origin in model space).</summary>
		public static Vector3Int WorldPivot(VoxelRigModel model, VoxelPart part)
		{
			var world = Vector3Int.zero;
			var visited = new HashSet<string>();
			for (var current = part; current != null && visited.Add(current.Id);)
			{
				world += current.Pivot;
				current = current.Parent == VoxelRigModel.RootId ? null : model.FindPart(current.Parent);
			}

			return world;
		}

		private static string RenderMask(bool[,] mask)
		{
			var width = mask.GetLength(0);
			var height = mask.GetLength(1);
			var sb = new System.Text.StringBuilder();
			for (var y = height - 1; y >= 0; y--)
			{
				for (var x = 0; x < width; x++)
				{
					sb.Append(mask[x, y] ? '#' : '_');
				}

				if (y > 0)
				{
					sb.Append('\n');
				}
			}

			return sb.ToString();
		}

		/// <summary>World-space cell boxes every part's geometry is confined to (mirror boxes are reflections of their source's).</summary>
		private static IReadOnlyList<(Vector3Int Min, Vector3Int Max)> PartBoxes(VoxelRigModel skeleton)
		{
			var boxes = new List<(Vector3Int, Vector3Int)>();
			foreach (var part in skeleton.Parts)
			{
				if (part.Data is MirrorPartData mirror)
				{
					var source = skeleton.FindPart(mirror.Source);
					var (offset, size) = SizeAndOffsetOf(skeleton, source?.Data);
					if (size == Vector3Int.zero)
					{
						continue;
					}

					var localMin = offset;
					var localMax = offset + size - Vector3Int.one;
					var reflectedMin = localMin;
					var reflectedMax = localMax;
					switch (mirror.Axis)
					{
						case MirrorAxis.X:
							reflectedMin.x = -localMax.x;
							reflectedMax.x = -localMin.x;
							break;
						case MirrorAxis.Y:
							reflectedMin.y = -localMax.y;
							reflectedMax.y = -localMin.y;
							break;
						default:
							reflectedMin.z = -localMax.z;
							reflectedMax.z = -localMin.z;
							break;
					}

					var world = WorldPivot(skeleton, part);
					boxes.Add((world + reflectedMin, world + reflectedMax));
				}
				else
				{
					var (offset, size) = SizeAndOffsetOf(skeleton, part.Data);
					if (size == Vector3Int.zero)
					{
						continue;
					}

					var world = WorldPivot(skeleton, part);
					boxes.Add((world + offset, world + offset + size - Vector3Int.one));
				}
			}

			return boxes;
		}

		/// <summary>Declared window of a part's geometry; copies resolve through to their source's window.</summary>
		private static (Vector3Int Offset, Vector3Int Size) SizeAndOffsetOf(VoxelRigModel skeleton, PartData? data, int depth = 0) => data switch
		{
			PlannedPartData planned => (planned.Offset, planned.Size),
			LayersPartData layers => (layers.Offset, layers.Size),
			ScriptPartData script => (script.Offset, script.Size),
			PrimitivesPartData primitives => (primitives.Offset, primitives.Size),
			CopyPartData copy when depth < 8 => SizeAndOffsetOf(skeleton, skeleton.FindPart(copy.Source)?.Data, depth + 1),
			_ => (Vector3Int.zero, Vector3Int.zero),
		};

		private static void CheckMirror(VoxelRigModel skeleton, VoxelPart part, MirrorPartData mirror, List<string> errors)
		{
			var source = skeleton.FindPart(mirror.Source);
			if (source == null)
			{
				errors.Add($"Mirror part '{part.Id}' references unknown source '{mirror.Source}'.");
				return;
			}

			if (source.Data is MirrorPartData)
			{
				errors.Add($"Mirror part '{part.Id}' mirrors another mirror ('{mirror.Source}'); mirror the authored part instead.");
				return;
			}

			if (mirror.Axis != MirrorAxis.X)
			{
				errors.Add($"Mirror part '{part.Id}' mirrors across {mirror.Axis.ToString().ToLowerInvariant()}; bilateral models mirror across x.");
				return;
			}

			var sourceWorld = WorldPivot(skeleton, source);
			var expected = new Vector3Int(-sourceWorld.x, sourceWorld.y, sourceWorld.z);
			var world = WorldPivot(skeleton, part);
			if (world != expected)
			{
				errors.Add(
					$"Mirror part '{part.Id}' sits at world pivot [{world.x},{world.y},{world.z}] but the x reflection of " +
					$"'{mirror.Source}' is [{expected.x},{expected.y},{expected.z}]; fix its pivot (or omit the pivot to derive it).");
			}
		}

		private static void CheckAuthored(
			VoxelRigModel skeleton,
			VoxelPart part,
			ILookup<string, VoxelPart> xMirrors,
			List<string> errors)
		{
			var world = WorldPivot(skeleton, part);
			if (world.x != 0)
			{
				if (!xMirrors[part.Id].Any())
				{
					errors.Add(
						$"Side part '{part.Id}' (world pivot x={world.x}) has no mirror twin; add a part with " +
						$"`mirror: {{ source: {part.Id}, axis: x }}`, or move it onto the centre plane (pivot x=0).");
				}

				return;
			}

			var (offset, size) = SizeAndOffsetOf(skeleton, part.Data);

			if (size.x <= 0)
			{
				return;
			}

			if (size.x % 2 == 0)
			{
				errors.Add(
					$"Centre part '{part.Id}' (pivot x=0) is {size.x} wide; an even width can never straddle x=0 " +
					$"symmetrically — use an ODD width (e.g. {size.x - 1} or {size.x + 1}).");
			}
			else if (offset.x != -(size.x - 1) / 2)
			{
				errors.Add(
					$"Centre part '{part.Id}' has offset.x={offset.x}, but a {size.x}-wide grid centred on x=0 " +
					$"needs offset.x={-(size.x - 1) / 2}.");
			}
		}
	}
}
