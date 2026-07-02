using System.Collections.Generic;
using Assembler.AssetGeneration.MeshToVoxels;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Writes the spike's blocky occupancy grid out as a MagicaVoxel <c>.vox</c>. Bridges the spike's
    /// <see cref="VoxelGrid"/> + flat per-voxel colours into the shared
    /// <see cref="Assembler.AssetGeneration.MeshToVoxels.VoxWriter"/>, which owns palette-building
    /// (exact ≤254 colours, else median-cut), the reserved-slot handling, and the g3→MagicaVoxel
    /// axis remap — so the exported model matches the <c>Window &gt; Voxels &gt; Mesh to Voxels</c>
    /// output's orientation and colour handling.
    /// </summary>
    public static class SpikeVoxExport
    {
        /// <summary>
        /// Build a <see cref="VoxResult"/> from the occupied cells of <paramref name="grid"/>
        /// (coloured by <paramref name="colours"/>, indexed by <see cref="VoxelGrid.Index"/>) and
        /// write it to <paramref name="path"/>. Returns the number of voxels written.
        /// </summary>
        public static int Write(string path, VoxelGrid grid, Color32[] colours)
        {
            var cells = new List<VoxCell>(grid.OccupiedCount);
            for (int z = 0; z < grid.NZ; z++)
            {
                for (int y = 0; y < grid.NY; y++)
                {
                    for (int x = 0; x < grid.NX; x++)
                    {
                        int i = grid.Index(x, y, z);
                        if (grid.Occupied[i])
                        {
                            cells.Add(new VoxCell(x, y, z, colours[i]));
                        }
                    }
                }
            }

            VoxWriter.Write(path, new VoxResult(grid.NX, grid.NY, grid.NZ, cells));
            return cells.Count;
        }
    }
}
