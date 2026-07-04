using System.Collections.Generic;
using Assembler.AssetGeneration.Colour;

namespace Assembler.AssetGeneration.MeshToVoxel
{
    /// <summary>
    /// Writes the pipeline's blocky occupancy grid out as a MagicaVoxel <c>.vox</c>. Bridges the pipeline's
    /// <see cref="VoxelGrid"/> + flat per-voxel colours into <see cref="VoxWriter"/>, which owns
    /// palette-building (exact ≤254 colours, else median-cut), the reserved-slot handling, and the
    /// g3→MagicaVoxel axis remap. Engine-free (part of the portable core).
    /// </summary>
    public static class VoxExport
    {
        /// <summary>
        /// Build a <see cref="VoxResult"/> from the occupied cells of <paramref name="grid"/>
        /// (coloured by <paramref name="colours"/>, indexed by <see cref="VoxelGrid.Index"/>) and
        /// write it to <paramref name="path"/>. Returns the number of voxels written.
        /// </summary>
        public static int Write(string path, VoxelGrid grid, Rgba32[] colours)
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
