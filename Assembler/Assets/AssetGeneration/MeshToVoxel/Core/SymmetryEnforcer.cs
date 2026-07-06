using System;

namespace Assembler.AssetGeneration.MeshToVoxel
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
    /// Forces the coarse voxel model symmetric about the centre of its occupied bounding box on each
    /// selected axis, in one of two modes:
    /// <list type="bullet">
    /// <item><b>Union</b> (default): a cell is kept if it OR its mirror is filled, so no feature from
    /// either half is lost. Added voxels copy the source's colour; voxels already on both sides keep
    /// their own, so asymmetric surface detail (lettering, a two-tone coat) survives where the
    /// geometry already existed. Geometry comes out symmetric; colour need not.</item>
    /// <item><b>Force mirror</b>: the dominant half (more occupied voxels; ties keep the low side) is
    /// reflected onto the other, <i>overriding</i> whatever was there — the result is an exact mirror
    /// in both geometry and colour, discarding the input's asymmetry.</item>
    /// </list>
    /// Applied last, on the final coloured grid, so it is the authoritative word on the silhouette.
    /// Mutates <paramref name="grid"/> and <paramref name="colours"/> in place.
    /// </summary>
    public static class SymmetryEnforcer
    {
        public static void Apply(VoxelGrid grid, Rgba32[] colours, SymmetryAxes axes, bool forceMirror = false)
        {
            // Both operations across distinct axes commute and preserve one another's symmetry, so
            // the result is symmetric in every selected axis regardless of order.
            if ((axes & SymmetryAxes.X) != 0)
            {
                MirrorAxis(grid, colours, axis: 0, forceMirror);
            }
            if ((axes & SymmetryAxes.Y) != 0)
            {
                MirrorAxis(grid, colours, axis: 1, forceMirror);
            }
            if ((axes & SymmetryAxes.Z) != 0)
            {
                MirrorAxis(grid, colours, axis: 2, forceMirror);
            }
        }

        private static void MirrorAxis(VoxelGrid grid, Rgba32[] colours, int axis, bool forceMirror)
        {
            if (!TryOccupiedExtent(grid, axis, out int lo, out int hi))
            {
                return;
            }
            int sum = lo + hi; // mirror of coord a about the [lo,hi] centre is (lo+hi) − a

            if (forceMirror)
            {
                ForceMirrorAxis(grid, colours, axis, sum);
            }
            else
            {
                UnionAxis(grid, colours, axis, sum);
            }
        }

        // Union: fill each cell if it or its mirror was filled; added cells copy their source colour,
        // cells already present keep their own.
        private static void UnionAxis(VoxelGrid grid, Rgba32[] colours, int axis, int sum)
        {
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

                        int m = grid.Index(MirrorX(x, axis, sum), MirrorY(y, axis, sum), MirrorZ(z, axis, sum));
                        if (!grid.Occupied[m])
                        {
                            grid.Occupied[m] = true;
                            colours[m] = colours[i];
                        }
                    }
                }
            }
        }

        // Force mirror: keep the dominant half (plus the centre plane), overwrite the other half with
        // the dominant half's reflection — exact geometric AND colour symmetry, overriding the input.
        private static void ForceMirrorAxis(VoxelGrid grid, Rgba32[] colours, int axis, int sum)
        {
            var wasOccupied = (bool[])grid.Occupied.Clone();
            var wasColours = (Rgba32[])colours.Clone();
            bool keepLow = DominantIsLow(grid, wasOccupied, axis, sum);

            for (int z = 0; z < grid.NZ; z++)
            {
                for (int y = 0; y < grid.NY; y++)
                {
                    for (int x = 0; x < grid.NX; x++)
                    {
                        int twiceCoord = 2 * Coord(x, y, z, axis);
                        // The centre plane maps to itself; the kept half is left untouched.
                        if (twiceCoord == sum || (twiceCoord < sum) == keepLow)
                        {
                            continue;
                        }

                        int mx = MirrorX(x, axis, sum), my = MirrorY(y, axis, sum), mz = MirrorZ(z, axis, sum);
                        int i = grid.Index(x, y, z);
                        if (grid.InBounds(mx, my, mz))
                        {
                            int m = grid.Index(mx, my, mz);
                            grid.Occupied[i] = wasOccupied[m];
                            colours[i] = wasColours[m];
                        }
                        else
                        {
                            grid.Occupied[i] = false;
                        }
                    }
                }
            }
        }

        private static bool DominantIsLow(VoxelGrid grid, bool[] occupied, int axis, int sum)
        {
            int low = 0, high = 0;
            for (int z = 0; z < grid.NZ; z++)
            {
                for (int y = 0; y < grid.NY; y++)
                {
                    for (int x = 0; x < grid.NX; x++)
                    {
                        if (!occupied[grid.Index(x, y, z)])
                        {
                            continue;
                        }
                        int twiceCoord = 2 * Coord(x, y, z, axis);
                        if (twiceCoord < sum) { low++; }
                        else if (twiceCoord > sum) { high++; }
                    }
                }
            }
            return low >= high; // tie → keep the low side, deterministically
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
                        int a = Coord(x, y, z, axis);
                        if (a < lo) { lo = a; }
                        if (a > hi) { hi = a; }
                    }
                }
            }
            return hi >= lo;
        }

        private static int Coord(int x, int y, int z, int axis) => axis == 0 ? x : axis == 1 ? y : z;

        private static int MirrorX(int x, int axis, int sum) => axis == 0 ? sum - x : x;
        private static int MirrorY(int y, int axis, int sum) => axis == 1 ? sum - y : y;
        private static int MirrorZ(int z, int axis, int sum) => axis == 2 ? sum - z : z;
    }
}
