using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Assembler.Voxels.Pipeline.Stages
{
	/// <summary>
	/// Cheap, non-LLM geometry check + repair over <c>Model</c> (Phase 4):
	///
	/// - <b>Connectivity</b>: 6-connected flood fill identifies components; tiny
	///   orphan islands (≤ <see cref="_orphanMaxSize"/>) are optionally pruned and
	///   the remaining count is reported.
	/// - <b>Symmetry</b>: mirrors about the bounding-box X-plane and scores the
	///   fraction of voxels whose mirror is also occupied.
	///
	/// Writes a <see cref="GeometryReport"/> to <c>Geometry</c>; when it prunes it
	/// rebuilds both <c>Model</c> and <c>GoxelTextZUp</c> so downstream encode and
	/// display stay consistent. Valuable even with zero refinement iterations.
	/// </summary>
	public sealed class ValidateGeometryStage : IVoxelStage
	{
		private readonly bool _pruneOrphans;
		private readonly int _orphanMaxSize;

		public ValidateGeometryStage(bool pruneOrphans = true, int orphanMaxSize = 4)
		{
			_pruneOrphans = pruneOrphans;
			_orphanMaxSize = Mathf.Max(0, orphanMaxSize);
		}

		public string Name => "ValidateGeometry";

		public Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			if (ctx.Model == null)
			{
				throw new InvalidOperationException($"{Name}: Model is required (run ParseModel first).");
			}

			var model = ctx.Model;
			var total = model.Voxels.Count;
			if (total == 0)
			{
				var empty = new GeometryReport(0, 0, 0, 0, 1f);
				ctx.Observer.OnLog(empty.Summarise());
				return Task.FromResult(ctx with { Geometry = empty });
			}

			var components = FindComponents(model.Voxels.Keys);
			components.Sort((a, b) => b.Count.CompareTo(a.Count));
			var largest = components[0];

			// Prune small islands that are NOT the largest component.
			var pruned = new HashSet<Vector3Int>();
			if (_pruneOrphans)
			{
				for (var i = 1; i < components.Count; i++)
				{
					if (components[i].Count <= _orphanMaxSize)
					{
						foreach (var p in components[i])
						{
							pruned.Add(p);
						}
					}
				}
			}

			var workingModel = model;
			var workingText = ctx.GoxelTextZUp;
			if (pruned.Count > 0)
			{
				workingModel = RebuildWithout(model, pruned);
				workingText = GoxelTextWriter.Write(workingModel);
				components = FindComponents(workingModel.Voxels.Keys);
				components.Sort((a, b) => b.Count.CompareTo(a.Count));
				largest = components[0];
			}

			var symmetry = SymmetryScore(workingModel);
			var report = new GeometryReport(
				TotalVoxels: workingModel.Voxels.Count,
				ComponentCount: components.Count,
				LargestComponentSize: largest.Count,
				PrunedVoxels: pruned.Count,
				SymmetryScore: symmetry);

			ctx.Observer.OnLog(report.Summarise());

			return Task.FromResult(ctx with
			{
				Model = workingModel,
				GoxelTextZUp = workingText,
				Geometry = report,
			});
		}

		private static List<List<Vector3Int>> FindComponents(IEnumerable<Vector3Int> positions)
		{
			var remaining = new HashSet<Vector3Int>(positions);
			var components = new List<List<Vector3Int>>();
			var stack = new Stack<Vector3Int>();

			while (remaining.Count > 0)
			{
				Vector3Int seed = default;
				foreach (var p in remaining)
				{
					seed = p;
					break;
				}

				var component = new List<Vector3Int>();
				stack.Push(seed);
				remaining.Remove(seed);

				while (stack.Count > 0)
				{
					var p = stack.Pop();
					component.Add(p);
					TryVisit(remaining, stack, new Vector3Int(p.x + 1, p.y, p.z));
					TryVisit(remaining, stack, new Vector3Int(p.x - 1, p.y, p.z));
					TryVisit(remaining, stack, new Vector3Int(p.x, p.y + 1, p.z));
					TryVisit(remaining, stack, new Vector3Int(p.x, p.y - 1, p.z));
					TryVisit(remaining, stack, new Vector3Int(p.x, p.y, p.z + 1));
					TryVisit(remaining, stack, new Vector3Int(p.x, p.y, p.z - 1));
				}

				components.Add(component);
			}

			return components;
		}

		private static void TryVisit(HashSet<Vector3Int> remaining, Stack<Vector3Int> stack, Vector3Int neighbour)
		{
			if (remaining.Remove(neighbour))
			{
				stack.Push(neighbour);
			}
		}

		private static float SymmetryScore(VoxelModel model)
		{
			var sum = model.Min.x + model.Max.x;
			var occupied = model.Voxels;
			var matched = 0;
			foreach (var p in occupied.Keys)
			{
				var mirror = new Vector3Int(sum - p.x, p.y, p.z);
				if (occupied.ContainsKey(mirror))
				{
					matched++;
				}
			}

			return occupied.Count == 0 ? 1f : (float)matched / occupied.Count;
		}

		private static VoxelModel RebuildWithout(VoxelModel model, HashSet<Vector3Int> remove)
		{
			var voxels = new Dictionary<Vector3Int, byte>(model.Voxels.Count);
			Vector3Int min = new(int.MaxValue, int.MaxValue, int.MaxValue);
			Vector3Int max = new(int.MinValue, int.MinValue, int.MinValue);
			var hasAny = false;

			foreach (var kv in model.Voxels)
			{
				if (remove.Contains(kv.Key))
				{
					continue;
				}

				voxels[kv.Key] = kv.Value;
				if (!hasAny)
				{
					min = max = kv.Key;
					hasAny = true;
				}
				else
				{
					min = Vector3Int.Min(min, kv.Key);
					max = Vector3Int.Max(max, kv.Key);
				}
			}

			if (!hasAny)
			{
				min = max = Vector3Int.zero;
			}

			return new VoxelModel(voxels, model.Palette, min, max);
		}
	}
}
