using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Stage 1a (optional) — collapse a finely-voxelised occupancy grid down to a coarse target
    /// grid, one output voxel per <c>factor³</c> block, so the blocky model stays Crossy-Road chunky
    /// without a plain coverage vote erasing thin silhouette features (legs, ears, antennae).
    ///
    /// This reimplements the geometric half of <see cref="MeshToVoxels.VoxDownres"/> directly on the
    /// SDF occupancy grid: an output voxel fills when its block's occupied fraction clears
    /// <c>coverageThreshold</c>, OR — when <c>forceThinFeatures</c> is on — when the block contains a
    /// structure thinner than the factor (a sub-Nyquist feature the coverage majority would drop),
    /// detected by a bounded morphological-erosion thickness map. Colour is reprojected per final
    /// voxel downstream, so there is no colour-salience pass here.
    /// </summary>
    public static class FeatureAwareDownsample
    {
        /// <summary>
        /// Downsample <paramref name="fine"/> by <paramref name="factor"/>, returning a grid at
        /// <c>ceil(dim / factor)</c> per axis with <c>CellSize · factor</c>. A factor ≤ 1 is a no-op.
        /// </summary>
        public static VoxelGrid Apply(VoxelGrid fine, int factor, float coverageThreshold, bool forceThinFeatures)
        {
            factor = Mathf.Max(1, factor);
            if (factor == 1)
            {
                return fine;
            }

            int outX = CeilDiv(fine.NX, factor);
            int outY = CeilDiv(fine.NY, factor);
            int outZ = CeilDiv(fine.NZ, factor);
            var lo = new VoxelGrid(outX, outY, outZ)
            {
                Origin = fine.Origin,
                CellSize = fine.CellSize * factor,
            };

            int[]? thickness = forceThinFeatures ? ThicknessMap(fine, factor) : null;
            float coverage = Mathf.Clamp01(coverageThreshold);

            for (int oz = 0; oz < outZ; oz++)
            {
                for (int oy = 0; oy < outY; oy++)
                {
                    for (int ox = 0; ox < outX; ox++)
                    {
                        if (ResolveBlock(fine, ox, oy, oz, factor, coverage, thickness))
                        {
                            lo.Occupied[lo.Index(ox, oy, oz)] = true;
                        }
                    }
                }
            }

            return lo;
        }

        private static bool ResolveBlock(
            VoxelGrid fine, int ox, int oy, int oz, int factor, float coverageThreshold, int[]? thickness)
        {
            int x0 = ox * factor, y0 = oy * factor, z0 = oz * factor;
            int inBounds = 0, occupied = 0, maxThickness = 0;

            for (int dz = 0; dz < factor && z0 + dz < fine.NZ; dz++)
            {
                for (int dy = 0; dy < factor && y0 + dy < fine.NY; dy++)
                {
                    for (int dx = 0; dx < factor && x0 + dx < fine.NX; dx++)
                    {
                        inBounds++;
                        int i = fine.Index(x0 + dx, y0 + dy, z0 + dz);
                        if (!fine.Occupied[i])
                        {
                            continue;
                        }
                        occupied++;
                        if (thickness != null && thickness[i] > maxThickness)
                        {
                            maxThickness = thickness[i];
                        }
                    }
                }
            }

            if (occupied == 0 || inBounds == 0)
            {
                return false;
            }

            // Feature-aware override: a structure thinner than one output voxel is sub-Nyquist and
            // would be erased by the coverage majority — force-keep it.
            bool thinFeature = thickness != null && maxThickness < factor;
            return thinFeature || (float)occupied / inBounds >= coverageThreshold;
        }

        /// <summary>
        /// Bounded morphological-erosion depth per occupied voxel (capped at <paramref name="cap"/>):
        /// surface voxels erode at pass 1 → depth 1; a voxel surviving all passes is at least
        /// <paramref name="cap"/> deep. Mirrors <see cref="MeshToVoxels.VoxDownres"/>'s thickness map.
        /// </summary>
        private static int[] ThicknessMap(VoxelGrid grid, int cap)
        {
            int n = grid.Occupied.Length;
            var thickness = new int[n];
            var current = (bool[])grid.Occupied.Clone();
            var next = new bool[n];

            for (int pass = 1; pass <= cap; pass++)
            {
                bool anyRemoved = false;
                for (int z = 0; z < grid.NZ; z++)
                {
                    for (int y = 0; y < grid.NY; y++)
                    {
                        for (int x = 0; x < grid.NX; x++)
                        {
                            int i = grid.Index(x, y, z);
                            if (!current[i])
                            {
                                continue;
                            }

                            if (IsInterior(current, grid, x, y, z))
                            {
                                next[i] = true;
                            }
                            else
                            {
                                next[i] = false;
                                thickness[i] = pass;
                                anyRemoved = true;
                            }
                        }
                    }
                }

                (current, next) = (next, current);
                if (!anyRemoved)
                {
                    break;
                }
            }

            for (int i = 0; i < n; i++)
            {
                if (current[i])
                {
                    thickness[i] = cap;
                }
            }
            return thickness;
        }

        // Interior ⇔ all six face-neighbours present; an off-grid neighbour counts as empty, so the
        // outer shell erodes inward.
        private static readonly (int dx, int dy, int dz)[] FaceNeighbours =
        {
            (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1),
        };

        private static bool IsInterior(bool[] current, VoxelGrid grid, int x, int y, int z)
        {
            foreach ((int dx, int dy, int dz) in FaceNeighbours)
            {
                int nx = x + dx, ny = y + dy, nz = z + dz;
                if (!grid.InBounds(nx, ny, nz) || !current[grid.Index(nx, ny, nz)])
                {
                    return false;
                }
            }
            return true;
        }

        private static int CeilDiv(int a, int b) => (a + b - 1) / b;
    }
}
