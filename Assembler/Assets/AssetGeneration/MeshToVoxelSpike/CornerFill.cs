using Assembler.AssetGeneration.MeshToVoxels;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Boxiness helper: fill the concave corners and notches that make a blocky silhouette read
    /// ragged, colouring each fill the modal colour of its occupied face-neighbours so it stays
    /// on-palette. An empty voxel is filled when EITHER
    /// <list type="bullet">
    /// <item>≥<see cref="SameColourThreshold"/> of its six face-neighbours share a colour (a clear
    /// colour consensus — fill it that colour), OR</item>
    /// <item>it has ≥<see cref="AnyColourThreshold"/> occupied face-neighbours (a deep enough
    /// concavity to box out regardless of colour — fill it the modal colour).</item>
    /// </list>
    /// "Share a colour" is a perceptual match within <c>colourTolerance</c> (Oklab distance), not
    /// exact RGB equality — so near-identical shades (Raw-mode samples, near-duplicate palette
    /// entries) still count as one colour. Tolerance 0 is exact match. The colour-consensus gate is
    /// what keeps colour boundaries clean: a 3-neighbour corner where distinct colour regions meet
    /// has no consensus and fewer than 4 neighbours, so it is left alone rather than filled with an
    /// arbitrary pick. Each pass reads a snapshot (order-independent). By default a single pass;
    /// <c>recursive</c> repeats until a pass fills nothing, so a fill that creates a new qualifying
    /// corner is chased down. Cells flagged as real air gaps (fine gap fraction &gt; ¼) are skipped
    /// every pass. Mutates <paramref name="grid"/> and <paramref name="colours"/> in place; returns
    /// the total number filled.
    /// </summary>
    public static class CornerFill
    {
        /// <summary>Fill when this many face-neighbours share a colour (fill that colour).</summary>
        public const int SameColourThreshold = 3;

        /// <summary>Fill when this many face-neighbours are occupied, any colours (fill the modal).</summary>
        public const int AnyColourThreshold = 4;

        // A cell whose fine support is more than a quarter air-gap is a real gap — never fill it.
        private const float GapFillLimit = 0.25f;

        private static readonly (int dx, int dy, int dz)[] FaceNeighbours =
        {
            (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1),
        };

        /// <summary>
        /// <paramref name="gapFraction"/> (per-cell, <see cref="VoxelGrid.Index"/> layout) may be
        /// null to skip the gap guard. <paramref name="colourTolerance"/> is the Oklab distance
        /// within which neighbour colours count as the same (0 = exact match). With
        /// <paramref name="recursive"/>, passes repeat until stable (always terminates — each pass
        /// only adds cells to a finite grid).
        /// </summary>
        public static int Apply(
            VoxelGrid grid, Color32[] colours, float[]? gapFraction,
            bool recursive = false, float colourTolerance = 0f)
        {
            int total = 0;
            int filledThisPass;
            do
            {
                filledThisPass = SinglePass(grid, colours, gapFraction, colourTolerance);
                total += filledThisPass;
            }
            while (recursive && filledThisPass > 0);
            return total;
        }

        private static int SinglePass(VoxelGrid grid, Color32[] colours, float[]? gapFraction, float colourTolerance)
        {
            var wasOccupied = (bool[])grid.Occupied.Clone();
            float toleranceSqr = colourTolerance * colourTolerance;

            // A voxel has ≤6 face-neighbours, so its colours group into at most 6 perceptual
            // clusters. Reuse these across cells to avoid per-cell allocation.
            var clusterLab = new OklabColor[6];
            var clusterColour = new Color32[6];
            var clusterCount = new int[6];
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

                        int occupiedNeighbours = 0;
                        int clusters = 0;

                        foreach ((int dx, int dy, int dz) in FaceNeighbours)
                        {
                            int nx = x + dx, ny = y + dy, nz = z + dz;
                            if (!grid.InBounds(nx, ny, nz) || !wasOccupied[grid.Index(nx, ny, nz)])
                            {
                                continue;
                            }

                            occupiedNeighbours++;
                            Color32 c = colours[grid.Index(nx, ny, nz)];
                            OklabColor lab = OklabColor.FromColor32(c);

                            // Add to the first cluster within tolerance, else seed a new one. The
                            // seed colour (first neighbour in scan order) is the cluster's fill colour.
                            int match = -1;
                            for (int k = 0; k < clusters; k++)
                            {
                                if (lab.SquaredDistanceTo(clusterLab[k]) <= toleranceSqr)
                                {
                                    match = k;
                                    break;
                                }
                            }
                            if (match >= 0)
                            {
                                clusterCount[match]++;
                            }
                            else
                            {
                                clusterLab[clusters] = lab;
                                clusterColour[clusters] = c;
                                clusterCount[clusters] = 1;
                                clusters++;
                            }
                        }

                        // Largest cluster: its size is the consensus count, its seed the fill colour.
                        // First-in-scan-order wins ties (strict >), so the result is deterministic.
                        int bestCount = 0;
                        Color32 modal = default;
                        for (int k = 0; k < clusters; k++)
                        {
                            if (clusterCount[k] > bestCount)
                            {
                                bestCount = clusterCount[k];
                                modal = clusterColour[k];
                            }
                        }

                        // Fill on a clear colour consensus (≥3 similar) or a deep concavity (≥4 any).
                        if (bestCount >= SameColourThreshold || occupiedNeighbours >= AnyColourThreshold)
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
