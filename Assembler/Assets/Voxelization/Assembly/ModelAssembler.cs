using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Voxels;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>One part with its resolved geometry: a part-local Y-up grid plus the accumulated world pivot.</summary>
	public sealed record AssembledPart(VoxelPart Part, VoxelModel Grid, Vector3Int WorldPivot)
	{
		/// <summary>The part's voxels translated into model space.</summary>
		public IEnumerable<KeyValuePair<Vector3Int, byte>> WorldVoxels =>
			Grid.Voxels.Select(kv => new KeyValuePair<Vector3Int, byte>(kv.Key + WorldPivot, kv.Value));
	}

	public sealed record AssembledModel(
		VoxelRigModel Model,
		IReadOnlyList<AssembledPart> Parts,
		VoxelModel Composed,
		ValidationReport AssemblyIssues)
	{
		public AssembledPart? FindPart(string id) => Parts.FirstOrDefault(p => p.Part.Id == id);
	}

	/// <summary>
	/// Pure-code Stage 3: resolves every part's data block into a grid (layers
	/// via the codec, scripts via the runner, mirrors by reflecting an
	/// already-resolved sibling), accumulates pivot translations down the tree,
	/// and composes the whole model into one volume. Problems are collected as
	/// issues (not thrown) so the orchestrator can re-author just the
	/// offending parts.
	/// </summary>
	public sealed class ModelAssembler
	{
		private readonly IPartScriptRunner _scriptRunner;

		public ModelAssembler(IPartScriptRunner scriptRunner) => _scriptRunner = scriptRunner;

		public async Task<AssembledModel> AssembleAsync(VoxelRigModel model, CancellationToken ct)
		{
			var issues = new List<ValidationIssue>();
			var grids = new Dictionary<string, VoxelModel>();

			foreach (var part in model.Parts)
			{
				ct.ThrowIfCancellationRequested();
				var grid = await ResolvePartGridAsync(model, part, grids, issues, ct).ConfigureAwait(false);
				grids[part.Id] = grid;
			}

			var assembled = ResolveWorldPivots(model, grids, issues);
			var composed = Compose(model, assembled);
			return new AssembledModel(model, assembled, composed, new ValidationReport(issues));
		}

		private async Task<VoxelModel> ResolvePartGridAsync(
			VoxelRigModel model,
			VoxelPart part,
			IReadOnlyDictionary<string, VoxelModel> resolved,
			List<ValidationIssue> issues,
			CancellationToken ct)
		{
			switch (part.Data)
			{
				case LayersPartData layers:
					try
					{
						return LayersCodec.Decode(layers, model.Palette);
					}
					catch (FormatException ex)
					{
						issues.Add(new ValidationIssue(part.Id, IssueCode.LayersInvalid, ex.Message));
						return EmptyGrid(model);
					}

				case ScriptPartData script:
					try
					{
						var built = await _scriptRunner.RunAsync(script.Source, ct).ConfigureAwait(false);
						return FitToWindow(RemapToModelPalette(model, part.Id, built, issues), script.Size, script.Offset, model);
					}
					catch (OperationCanceledException)
					{
						throw;
					}
					catch (Exception ex)
					{
						issues.Add(new ValidationIssue(part.Id, IssueCode.ScriptFailed, ex.Message));
						return EmptyGrid(model);
					}

				case PrimitivesPartData primitives:
					try
					{
						return PrimitivesCodec.Decode(primitives, model.Palette);
					}
					catch (FormatException ex)
					{
						issues.Add(new ValidationIssue(part.Id, IssueCode.PrimitivesInvalid, ex.Message));
						return EmptyGrid(model);
					}

				case MirrorPartData mirror:
					if (!resolved.TryGetValue(mirror.Source, out var source))
					{
						issues.Add(new ValidationIssue(part.Id, IssueCode.MirrorInvalid,
							$"Mirror source '{mirror.Source}' is not declared before this part."));
						return EmptyGrid(model);
					}

					return MirrorGrid(source, mirror.Axis, model);

				case CopyPartData copy:
					if (!resolved.TryGetValue(copy.Source, out var original))
					{
						issues.Add(new ValidationIssue(part.Id, IssueCode.CopyInvalid,
							$"Copy source '{copy.Source}' is not declared before this part."));
						return EmptyGrid(model);
					}

					// Verbatim reuse: the copy's own pivot does the positioning.
					return original;

				default:
					issues.Add(new ValidationIssue(part.Id, IssueCode.NotAuthored,
						"Part is still planned — it has no authored geometry."));
					return EmptyGrid(model);
			}
		}

		/// <summary>
		/// Point-reflects a part-local grid through the local origin on one axis
		/// (p → -p), matching the pivot reflection convention in
		/// <see cref="MirrorPartData"/>.
		/// </summary>
		public static VoxelModel MirrorGrid(VoxelModel source, MirrorAxis axis, VoxelRigModel model)
		{
			var mirrored = new Dictionary<Vector3Int, byte>(source.Voxels.Count);
			foreach (var kv in source.Voxels)
			{
				var p = kv.Key;
				var reflected = axis switch
				{
					MirrorAxis.X => new Vector3Int(-p.x, p.y, p.z),
					MirrorAxis.Y => new Vector3Int(p.x, -p.y, p.z),
					_ => new Vector3Int(p.x, p.y, -p.z),
				};
				mirrored[reflected] = kv.Value;
			}

			return LayersCodec.ToModel(mirrored, model.Palette);
		}

		/// <summary>
		/// Script authors routinely build their shape at the origin instead of at
		/// the declared offset, which cascades into SizeExceeded and
		/// DisconnectedPart failures even though the shape itself is right. When
		/// the shape fits the declared size, snap it into the declared window with
		/// the minimal per-axis shift; genuinely oversized shapes pass through
		/// unchanged so validation reports them.
		/// </summary>
		private static VoxelModel FitToWindow(VoxelModel grid, Vector3Int size, Vector3Int offset, VoxelRigModel model)
		{
			if (grid.Voxels.Count == 0)
			{
				return grid;
			}

			var min = grid.Min;
			var max = grid.Max;
			var shift = Vector3Int.zero;
			for (var axis = 0; axis < 3; axis++)
			{
				var lo = offset[axis];
				var hi = offset[axis] + size[axis] - 1;
				if (max[axis] - min[axis] > hi - lo)
				{
					continue;
				}

				if (min[axis] < lo)
				{
					shift[axis] = lo - min[axis];
				}
				else if (max[axis] > hi)
				{
					shift[axis] = hi - max[axis];
				}
			}

			return shift == Vector3Int.zero
				? grid
				: LayersCodec.ToModel(grid.Voxels.ToDictionary(kv => kv.Key + shift, kv => kv.Value), model.Palette);
		}

		private static VoxelModel RemapToModelPalette(
			VoxelRigModel model,
			string partId,
			VoxelModel built,
			List<ValidationIssue> issues)
		{
			var colourToIndex = new Dictionary<int, byte>();
			for (var i = 0; i < model.Palette.Count; i++)
			{
				colourToIndex[ColourKey(model.Palette[i].Colour)] = (byte)(i + 1);
			}

			var voxels = new Dictionary<Vector3Int, byte>(built.Voxels.Count);
			var unknown = new HashSet<string>();
			foreach (var kv in built.Voxels)
			{
				var colour = built.Palette[kv.Value - 1];
				if (colourToIndex.TryGetValue(ColourKey(colour), out var index))
				{
					voxels[kv.Key] = index;
				}
				else
				{
					unknown.Add($"#{colour.r:x2}{colour.g:x2}{colour.b:x2}");
				}
			}

			if (unknown.Count > 0)
			{
				issues.Add(new ValidationIssue(partId, IssueCode.PaletteBreach,
					$"Script used colours not in the model palette: {string.Join(", ", unknown.OrderBy(c => c))}. " +
					"Use only the declared palette colours."));
			}

			return LayersCodec.ToModel(voxels, model.Palette);
		}

		private static IReadOnlyList<AssembledPart> ResolveWorldPivots(
			VoxelRigModel model,
			IReadOnlyDictionary<string, VoxelModel> grids,
			List<ValidationIssue> issues)
		{
			var worldPivots = new Dictionary<string, Vector3Int>();
			var assembled = new List<AssembledPart>();

			foreach (var part in model.Parts)
			{
				Vector3Int parentPivot;
				if (part.Parent == VoxelRigModel.RootId)
				{
					parentPivot = Vector3Int.zero;
				}
				else if (!worldPivots.TryGetValue(part.Parent, out parentPivot))
				{
					issues.Add(new ValidationIssue(part.Id, IssueCode.HierarchyInvalid,
						$"Parent '{part.Parent}' is not declared before this part."));
					parentPivot = Vector3Int.zero;
				}

				var worldPivot = parentPivot + part.Pivot;
				worldPivots[part.Id] = worldPivot;
				assembled.Add(new AssembledPart(part, grids[part.Id], worldPivot));
			}

			return assembled;
		}

		/// <summary>
		/// Unions the resolved parts into one volume, declaration order winning ties.
		/// Public and pure so the hull clip can recompose a clipped part list without
		/// re-resolving every grid.
		/// </summary>
		public static VoxelModel Compose(VoxelRigModel model, IReadOnlyList<AssembledPart> parts)
		{
			// Declaration order wins ties: a later part overwrites where volumes overlap.
			var voxels = new Dictionary<Vector3Int, byte>();
			foreach (var part in parts)
			{
				foreach (var kv in part.WorldVoxels)
				{
					voxels[kv.Key] = kv.Value;
				}
			}

			return LayersCodec.ToModel(voxels, model.Palette);
		}

		private static VoxelModel EmptyGrid(VoxelRigModel model) =>
			LayersCodec.ToModel(new Dictionary<Vector3Int, byte>(), model.Palette);

		private static int ColourKey(Color32 c) => (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
	}
}
