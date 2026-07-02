using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// One-off analysis of the fine occupancy grid that every downstream stage shares: the
    /// bounded-erosion thickness map (moved here from the old FeatureAwareDownsample), 6-connected
    /// components with a main-component mask (the connectivity gate for thin-feature keeps and
    /// floater removal), the air-gap mask (empty cells pinched between occupied cells — the space
    /// between a dog's legs), and 3D prefix sums over each so the grid placement search can re-vote
    /// hundreds of candidate lattices with 8 lookups per block per mask.
    /// </summary>
    public sealed class FineGridAnalysis
    {
        public VoxelGrid Fine { get; private init; } = null!;

        /// <summary>The fine-cells-per-coarse-voxel factor the thickness map was capped at.</summary>
        public int Factor { get; private init; }

        /// <summary>Bounded erosion depth per occupied cell, 1..Factor (0 = empty).</summary>
        public int[] Thickness { get; private init; } = null!;

        /// <summary>Cells belonging to the largest 6-connected occupied component.</summary>
        public bool[] MainMask { get; private init; } = null!;

        /// <summary>Empty cells with occupied cells on opposing sides ≤2 cells away along some axis.</summary>
        public bool[] GapMask { get; private init; } = null!;

        public int ComponentCount { get; private init; }

        public IntegralVolume OccupancyIntegral { get; private init; } = null!;

        /// <summary>Integral over cells at least <see cref="Factor"/> thick — a block with occupancy but no thick cells is sub-Nyquist.</summary>
        public IntegralVolume ThickIntegral { get; private init; } = null!;

        public IntegralVolume GapIntegral { get; private init; } = null!;
        public IntegralVolume MainIntegral { get; private init; } = null!;

        /// <summary>Inclusive occupied bounding box, per axis. Zero-extent when the grid is empty.</summary>
        public Vector3Int OccupiedMin { get; private init; }
        public Vector3Int OccupiedMax { get; private init; }

        public bool IsEmpty => OccupancyIntegral.Total == 0;

        private FineGridAnalysis() { }

        public static FineGridAnalysis Build(VoxelGrid fine, int factor)
        {
            factor = Mathf.Max(1, factor);
            int[] thickness = ThicknessMap(fine, factor);

            var thickMask = new bool[fine.Occupied.Length];
            for (int i = 0; i < thickness.Length; i++)
            {
                thickMask[i] = thickness[i] >= factor;
            }

            int[] labels = OccupancyCleanup.LabelComponents(fine.Occupied, fine.NX, fine.NY, fine.NZ, out int componentCount);
            bool[] mainMask = LargestComponentMask(fine.Occupied, labels, componentCount);
            bool[] gapMask = BuildGapMask(fine);
            (Vector3Int min, Vector3Int max) = OccupiedBounds(fine);

            return new FineGridAnalysis
            {
                Fine = fine,
                Factor = factor,
                Thickness = thickness,
                MainMask = mainMask,
                GapMask = gapMask,
                ComponentCount = componentCount,
                OccupancyIntegral = IntegralVolume.Build(fine.Occupied, fine.NX, fine.NY, fine.NZ),
                ThickIntegral = IntegralVolume.Build(thickMask, fine.NX, fine.NY, fine.NZ),
                GapIntegral = IntegralVolume.Build(gapMask, fine.NX, fine.NY, fine.NZ),
                MainIntegral = IntegralVolume.Build(mainMask, fine.NX, fine.NY, fine.NZ),
                OccupiedMin = min,
                OccupiedMax = max,
            };
        }

        /// <summary>
        /// Bounded morphological-erosion depth per occupied voxel (capped at <paramref name="cap"/>):
        /// surface voxels erode at pass 1 → depth 1; a voxel surviving all passes is at least
        /// <paramref name="cap"/> deep. Moved verbatim from the old FeatureAwareDownsample.
        /// </summary>
        public static int[] ThicknessMap(VoxelGrid grid, int cap)
        {
            int n = grid.Occupied.Length;
            var thickness = new int[n];
            var current = (bool[])grid.Occupied.Clone();
            var next = new bool[n];

            for (int pass = 1; pass <= cap; pass++)
            {
                // The write buffer is swapped between passes and would otherwise carry stale trues
                // from two passes ago, resurrecting eroded cells (a bug inherited from — and now
                // fixed relative to — the old FeatureAwareDownsample, whose thickness map marked
                // nearly everything cap-thick and so almost never detected thin features).
                System.Array.Clear(next, 0, n);

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

        // Largest component wins; ties resolve to the lowest label id, so the mask is deterministic.
        private static bool[] LargestComponentMask(bool[] occupied, int[] labels, int componentCount)
        {
            var mask = new bool[occupied.Length];
            if (componentCount == 0)
            {
                return mask;
            }

            var sizes = new int[componentCount];
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i] >= 0)
                {
                    sizes[labels[i]]++;
                }
            }

            int main = 0;
            for (int c = 1; c < componentCount; c++)
            {
                if (sizes[c] > sizes[main])
                {
                    main = c;
                }
            }

            for (int i = 0; i < labels.Length; i++)
            {
                mask[i] = labels[i] == main;
            }
            return mask;
        }

        // An air gap is an empty cell pinched between occupied cells: along at least one axis there
        // is an occupied cell within 2 cells in BOTH directions. That flags the space between a
        // dog's legs or under a canopy without flagging open air next to a single wall.
        private const int GapReach = 2;

        private static bool[] BuildGapMask(VoxelGrid grid)
        {
            var gap = new bool[grid.Occupied.Length];
            for (int z = 0; z < grid.NZ; z++)
            {
                for (int y = 0; y < grid.NY; y++)
                {
                    for (int x = 0; x < grid.NX; x++)
                    {
                        if (grid.Occupied[grid.Index(x, y, z)])
                        {
                            continue;
                        }

                        gap[grid.Index(x, y, z)] =
                            PinchedAlong(grid, x, y, z, 1, 0, 0)
                            || PinchedAlong(grid, x, y, z, 0, 1, 0)
                            || PinchedAlong(grid, x, y, z, 0, 0, 1);
                    }
                }
            }
            return gap;
        }

        private static bool PinchedAlong(VoxelGrid grid, int x, int y, int z, int dx, int dy, int dz) =>
            OccupiedWithin(grid, x, y, z, dx, dy, dz) && OccupiedWithin(grid, x, y, z, -dx, -dy, -dz);

        private static bool OccupiedWithin(VoxelGrid grid, int x, int y, int z, int dx, int dy, int dz)
        {
            for (int step = 1; step <= GapReach; step++)
            {
                if (grid.IsOccupied(x + dx * step, y + dy * step, z + dz * step))
                {
                    return true;
                }
            }
            return false;
        }

        private static (Vector3Int min, Vector3Int max) OccupiedBounds(VoxelGrid grid)
        {
            var min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            var max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            bool any = false;

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
                        any = true;
                        min = Vector3Int.Min(min, new Vector3Int(x, y, z));
                        max = Vector3Int.Max(max, new Vector3Int(x, y, z));
                    }
                }
            }

            return any ? (min, max) : (Vector3Int.zero, Vector3Int.zero);
        }
    }
}
