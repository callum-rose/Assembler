using System.Collections.Generic;
using Assembler.Voxels;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// The Y-up (Claude-facing / Unity) ↔ Z-up (.vox / Goxel storage) boundary
	/// for whole grids — the grid-level counterpart of
	/// <see cref="GoxelCoordinateConverter"/>. The swap is involutive.
	/// </summary>
	public static class VoxelGridConvert
	{
		public static VoxelModel SwapYZ(VoxelModel model)
		{
			var swapped = new Dictionary<Vector3Int, byte>(model.Voxels.Count);
			foreach (var kv in model.Voxels)
			{
				var p = kv.Key;
				swapped[new Vector3Int(p.x, p.z, p.y)] = kv.Value;
			}

			return new VoxelModel(
				swapped,
				model.Palette,
				Swap(model.Min),
				Swap(model.Max));
		}

		private static Vector3Int Swap(Vector3Int v) => new(v.x, v.z, v.y);
	}
}
