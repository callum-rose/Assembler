using System;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>The grid axes to force mirror-symmetry across (any combination).</summary>
    [Flags]
    public enum SymmetryAxes
    {
        None = 0,
        X = 1 << 0,
        Y = 1 << 1,
        Z = 1 << 2,
    }

    /// <summary>
    /// Forces the coarse voxel model to be mirror-symmetric about the centre of its occupied
    /// bounding box on each selected axis, by <b>union</b>: a cell is filled if it OR its mirror is
    /// filled, so no feature from either half is lost. Newly added (mirrored) voxels copy the colour
    /// of the source voxel they mirror; voxels already present on both sides keep their own colour,
    /// so genuinely asymmetric surface detail (a mailbox's lettering, a two-tone coat) survives
    /// where the geometry already existed on both sides. Applied last, on the final coloured grid,
    /// so it is the authoritative word on the silhouette. Mutates <paramref name="grid"/> and
    /// <paramref name="colours"/> in place.
    /// </summary>
    public static class SymmetryEnforcer
    {
        public static void Apply(VoxelGrid grid, Color32[] colours, SymmetryAxes axes)
        {
            // Union operations across distinct axes commute and preserve one another's symmetry, so
            // the result is symmetric in every selected axis regardless of order.
            if ((axes & SymmetryAxes.X) != 0)
            {
                MirrorAxis(grid, colours, axis: 0);
            }
            if ((axes & SymmetryAxes.Y) != 0)
            {
                MirrorAxis(grid, colours, axis: 1);
            }
            if ((axes & SymmetryAxes.Z) != 0)
            {
                MirrorAxis(grid, colours, axis: 2);
            }
        }

        private static void MirrorAxis(VoxelGrid grid, Color32[] colours, int axis)
        {
            if (!TryOccupiedExtent(grid, axis, out int lo, out int hi))
            {
                return;
            }
            int sum = lo + hi; // mirror of coord a about the [lo,hi] centre is (lo+hi) − a

            // Read from a snapshot so the pass is a clean union of the pre-pass occupancy, never
            // chaining a just-added cell into another mirror.
            var wasOccupied = (bool[])grid.Occupied.Clone();
            for (int z = 0; z < grid.NZ; z++)
            {
                for (int y = 0; y < grid.NY; y++)
                {
                    for (int x = 0; x < grid.NX; x++)
                    {
                        int i = grid.Index(x, y, z);
                        if (!wasOccupied[i])
                        {
                            continue;
                        }

                        int mx = x, my = y, mz = z;
                        switch (axis)
                        {
                            case 0: mx = sum - x; break;
                            case 1: my = sum - y; break;
                            default: mz = sum - z; break;
                        }

                        int m = grid.Index(mx, my, mz);
                        if (!grid.Occupied[m])
                        {
                            grid.Occupied[m] = true;
                            colours[m] = colours[i];
                        }
                    }
                }
            }
        }

        private static bool TryOccupiedExtent(VoxelGrid grid, int axis, out int lo, out int hi)
        {
            lo = int.MaxValue;
            hi = int.MinValue;
            for (int z = 0; z < grid.NZ; z++)
            {
                for (int y = 0; y < grid.NY; y++)
                {
                    for (int x = 0; x < grid.NX; x++)
                    {
                        if (!grid.Occupied[grid.Index(x, y, z)])
                        {
                            continue;
                        }
                        int a = axis == 0 ? x : axis == 1 ? y : z;
                        if (a < lo) { lo = a; }
                        if (a > hi) { hi = a; }
                    }
                }
            }
            return hi >= lo;
        }
    }
}
