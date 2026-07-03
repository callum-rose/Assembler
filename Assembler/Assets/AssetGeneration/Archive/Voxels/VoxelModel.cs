using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Voxels
{
	public sealed class VoxelModel
	{
		public Dictionary<Vector3Int, byte> Voxels { get; }
		public Color32[] Palette { get; }
		public Vector3Int Min { get; }
		public Vector3Int Max { get; }

		public VoxelModel(Dictionary<Vector3Int, byte> voxels, Color32[] palette, Vector3Int min, Vector3Int max)
		{
			Voxels = voxels;
			Palette = palette;
			Min = min;
			Max = max;
		}

		public Vector3Int Size => Max - Min + Vector3Int.one;
	}
}
