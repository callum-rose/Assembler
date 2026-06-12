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
		/// Errors that make the skeleton geometrically incapable of bilateral
		/// symmetry. Empty for non-bilateral models and for sound plans. Messages
		/// are written as feedback for the planning model.
		/// </summary>
		public static IReadOnlyList<string> Errors(VoxelRigModel skeleton)
		{
			if (!skeleton.IsBilateral)
			{
				return Array.Empty<string>();
			}

			var errors = new List<string>();
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

			var (size, offset) = part.Data switch
			{
				PlannedPartData planned => (planned.Size, planned.Offset),
				LayersPartData layers => (layers.Size, layers.Offset),
				ScriptPartData script => (script.Size, script.Offset),
				_ => (Vector3Int.zero, Vector3Int.zero),
			};

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
