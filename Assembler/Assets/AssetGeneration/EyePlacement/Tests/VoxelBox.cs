using System.Collections.Generic;
using UnityEngine;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement.Tests
{
    /// <summary>Builds simple solid-box <see cref="VoxelModel"/>s for the placement tests.</summary>
    internal static class VoxelBox
    {
        public static VoxelModel Solid(Vector3Int min, Vector3Int max)
        {
            var voxels = new Dictionary<Vector3Int, byte>();
            for (int x = min.x; x <= max.x; x++)
            {
                for (int y = min.y; y <= max.y; y++)
                {
                    for (int z = min.z; z <= max.z; z++)
                    {
                        voxels[new Vector3Int(x, y, z)] = 1;
                    }
                }
            }

            var palette = new Color32[] { new(200, 200, 200, 255) };
            return new VoxelModel(voxels, palette, min, max);
        }
    }
}
