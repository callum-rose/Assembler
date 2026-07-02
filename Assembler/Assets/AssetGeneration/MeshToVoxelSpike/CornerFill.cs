using System.Collections.Generic;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Boxiness helper: fill any empty voxel with at least <see cref="NeighbourThreshold"/> of its
    /// six face-neighbours occupied — the concave corners and notches that make a blocky silhouette
    /// read ragged — and colour each fill the modal (most common) colour of those occupied
    /// neighbours, so it stays on-palette. Each pass reads a snapshot (order-independent). By
    /// default a single pass, which can't run away filling a whole cavity; <c>recursive</c> repeats
    /// until a pass fills nothing, so a fill that creates a new ≥-threshold corner is chased down
    /// (deeper concavities box out, at the cost of more aggressive filling). Cells flagged as real
    /// air gaps (fine gap fraction &gt; ¼) are skipped every pass, so the space between legs and a
    /// mug-handle hole stay open regardless. Mutates <paramref name="grid"/> and
    /// <paramref name="colours"/> in place; returns the total number filled.
    /// </summary>
    public static class CornerFill
    {
        /// <summary>Minimum occupied face-neighbours for an empty cell to be filled.</summary>
        public const int NeighbourThreshold = 3;

        // A cell whose fine support is more than a quarter air-gap is a real gap — never fill it.
        private const float GapFillLimit = 0.25f;

        private static readonly (int dx, int dy, int dz)[] FaceNeighbours =
        {
            (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1),
        };

        /// <summary>
        /// <paramref name="gapFraction"/> (per-cell, <see cref="VoxelGrid.Index"/> layout) may be
        /// null to skip the gap guard. With <paramref name="recursive"/>, passes repeat until stable
        /// (always terminates — each pass only adds cells to a finite grid).
        /// </summary>
        public static int Apply(VoxelGrid grid, Color32[] colours, float[]? gapFraction, bool recursive = false)
        {
            int total = 0;
            int filledThisPass;
            do
            {
                filledThisPass = SinglePass(grid, colours, gapFraction);
                total += filledThisPass;
            }
            while (recursive && filledThisPass > 0);
            return total;
        }

        private static int SinglePass(VoxelGrid grid, Color32[] colours, float[]? gapFraction)
        {
            var wasOccupied = (bool[])grid.Occupied.Clone();
            var counts = new Dictionary<int, int>();
            int filled = 0;

            for (int z = 0; z < grid.NZ; z++)
            {
                for (int y = 0; y < grid.NY; y++)
                {
                    for (int x = 0; x < grid.NX; x++)
                    {
                        int i = grid.Index(x, y, z);
                        if (wasOccupied[i])
                        {
                            continue;
                        }
                        if (gapFraction != null && gapFraction[i] > GapFillLimit)
                        {
                            continue;
                        }

                        counts.Clear();
                        int occupiedNeighbours = 0;
                        Color32 modal = default;
                        int bestCount = 0;

                        foreach ((int dx, int dy, int dz) in FaceNeighbours)
                        {
                            int nx = x + dx, ny = y + dy, nz = z + dz;
                            if (!grid.InBounds(nx, ny, nz) || !wasOccupied[grid.Index(nx, ny, nz)])
                            {
                                continue;
                            }

                            occupiedNeighbours++;
                            Color32 c = colours[grid.Index(nx, ny, nz)];
                            int key = (c.r << 16) | (c.g << 8) | c.b;
                            int count = counts.TryGetValue(key, out int existing) ? existing + 1 : 1;
                            counts[key] = count;
                            // Strict >: the first colour (in fixed neighbour order) to reach the top
                            // count wins, so ties are deterministic.
                            if (count > bestCount)
                            {
                                bestCount = count;
                                modal = c;
                            }
                        }

                        if (occupiedNeighbours >= NeighbourThreshold)
                        {
                            grid.Occupied[i] = true;
                            colours[i] = modal;
                            filled++;
                        }
                    }
                }
            }
            return filled;
        }
    }
}
