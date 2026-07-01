using System.Collections.Generic;
using Assembler.AssetGeneration.MeshToVoxels;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>The three colour-handling modes surfaced as live toggles in the spike window.</summary>
    public enum ColourMode
    {
        /// <summary>Averaged reprojected colour, untouched — the truest read of the source texture.</summary>
        Raw,

        /// <summary>Flatten to a small per-model palette (k-means over the reprojected colours in Oklab).</summary>
        PerModelPalette,

        /// <summary>Snap each colour to the nearest swatch of a shared master palette (Oklab) for cross-asset cohesion.</summary>
        MasterPalette,
    }

    /// <summary>
    /// Stage 5 — post-sample colour handling. Flattening the reprojected colours to a small palette is
    /// what gives the Crossy-Road stylised read; <see cref="ColourMode.Raw"/> keeps them as-is for the
    /// A/B comparison. Operates on a flat colour array plus an optional validity mask (voxel occupancy /
    /// live vertices) so untouched entries are never dragged into the clustering.
    /// </summary>
    public static class ColourModes
    {
        public readonly struct Options
        {
            /// <summary>Target colour count for <see cref="ColourMode.PerModelPalette"/>.</summary>
            public int PaletteSize { get; init; }

            /// <summary>Swatches for <see cref="ColourMode.MasterPalette"/>.</summary>
            public IReadOnlyList<Color32>? MasterPalette { get; init; }
        }

        // Mirrors PaletteSnap: penalise snapping a near-neutral colour onto a saturated swatch so hull
        // panels don't turn pink. Only ADDED chroma is charged; desaturating is free.
        private const float ChromaGainPenalty = 8f;

        public static Color32[] Apply(Color32[] colours, bool[]? mask, ColourMode mode, Options options) =>
            mode switch
            {
                ColourMode.PerModelPalette => PerModelPalette(colours, mask, Mathf.Max(1, options.PaletteSize)),
                ColourMode.MasterPalette => MasterPaletteSnap(colours, mask, options.MasterPalette),
                _ => (Color32[])colours.Clone(),
            };

        // ---- Per-model palette (deterministic k-means in Oklab) --------------

        private static Color32[] PerModelPalette(Color32[] colours, bool[]? mask, int k)
        {
            var result = (Color32[])colours.Clone();
            List<int> valid = ValidIndices(colours, mask);
            if (valid.Count == 0)
            {
                return result;
            }

            var labs = new OklabColor[valid.Count];
            for (int i = 0; i < valid.Count; i++)
            {
                labs[i] = OklabColor.FromColor32(colours[valid[i]]);
            }

            int clusters = Mathf.Min(k, valid.Count);
            int[] seeds = FarthestPointSeeds(labs, clusters);
            var centroids = new OklabColor[clusters];
            for (int c = 0; c < clusters; c++)
            {
                centroids[c] = labs[seeds[c]];
            }

            var assignment = new int[valid.Count];
            const int maxIterations = 12;
            for (int iter = 0; iter < maxIterations; iter++)
            {
                bool changed = Assign(labs, centroids, assignment);
                Recompute(labs, assignment, centroids);
                if (!changed && iter > 0)
                {
                    break;
                }
            }

            Color32[] representatives = ClusterRgbMeans(colours, valid, assignment, clusters);
            for (int i = 0; i < valid.Count; i++)
            {
                result[valid[i]] = representatives[assignment[i]];
            }
            return result;
        }

        // Deterministic farthest-point (k-means++ without the randomness): first seed is the most
        // chromatic colour, each subsequent seed is the point farthest from the ones already chosen.
        private static int[] FarthestPointSeeds(OklabColor[] labs, int clusters)
        {
            var seeds = new int[clusters];
            int first = 0;
            float bestChroma = -1f;
            for (int i = 0; i < labs.Length; i++)
            {
                if (labs[i].Chroma > bestChroma)
                {
                    bestChroma = labs[i].Chroma;
                    first = i;
                }
            }
            seeds[0] = first;

            var minDistSqr = new float[labs.Length];
            for (int i = 0; i < labs.Length; i++)
            {
                minDistSqr[i] = labs[i].SquaredDistanceTo(labs[first]);
            }

            for (int c = 1; c < clusters; c++)
            {
                int farthest = 0;
                float best = -1f;
                for (int i = 0; i < labs.Length; i++)
                {
                    if (minDistSqr[i] > best)
                    {
                        best = minDistSqr[i];
                        farthest = i;
                    }
                }
                seeds[c] = farthest;
                for (int i = 0; i < labs.Length; i++)
                {
                    float d = labs[i].SquaredDistanceTo(labs[farthest]);
                    if (d < minDistSqr[i])
                    {
                        minDistSqr[i] = d;
                    }
                }
            }
            return seeds;
        }

        private static bool Assign(OklabColor[] labs, OklabColor[] centroids, int[] assignment)
        {
            bool changed = false;
            for (int i = 0; i < labs.Length; i++)
            {
                int best = 0;
                float bestSqr = float.MaxValue;
                for (int c = 0; c < centroids.Length; c++)
                {
                    float d = labs[i].SquaredDistanceTo(centroids[c]);
                    if (d < bestSqr)
                    {
                        bestSqr = d;
                        best = c;
                    }
                }
                if (assignment[i] != best)
                {
                    assignment[i] = best;
                    changed = true;
                }
            }
            return changed;
        }

        private static void Recompute(OklabColor[] labs, int[] assignment, OklabColor[] centroids)
        {
            var sumL = new float[centroids.Length];
            var sumA = new float[centroids.Length];
            var sumB = new float[centroids.Length];
            var count = new int[centroids.Length];

            for (int i = 0; i < labs.Length; i++)
            {
                int c = assignment[i];
                sumL[c] += labs[i].L;
                sumA[c] += labs[i].A;
                sumB[c] += labs[i].B;
                count[c]++;
            }

            for (int c = 0; c < centroids.Length; c++)
            {
                if (count[c] > 0)
                {
                    centroids[c] = new OklabColor(sumL[c] / count[c], sumA[c] / count[c], sumB[c] / count[c]);
                }
            }
        }

        // Each cluster's output colour is the mean RGB of its members — avoids an inverse-Oklab
        // transform and keeps the palette swatch a real average of the source colours.
        private static Color32[] ClusterRgbMeans(Color32[] colours, List<int> valid, int[] assignment, int clusters)
        {
            var r = new long[clusters];
            var g = new long[clusters];
            var b = new long[clusters];
            var n = new int[clusters];

            for (int i = 0; i < valid.Count; i++)
            {
                Color32 c = colours[valid[i]];
                int k = assignment[i];
                r[k] += c.r;
                g[k] += c.g;
                b[k] += c.b;
                n[k]++;
            }

            var means = new Color32[clusters];
            for (int k = 0; k < clusters; k++)
            {
                means[k] = n[k] > 0
                    ? new Color32((byte)(r[k] / n[k]), (byte)(g[k] / n[k]), (byte)(b[k] / n[k]), 255)
                    : new Color32(128, 128, 128, 255);
            }
            return means;
        }

        // ---- Master-palette snap --------------------------------------------

        private static Color32[] MasterPaletteSnap(Color32[] colours, bool[]? mask, IReadOnlyList<Color32>? palette)
        {
            var result = (Color32[])colours.Clone();
            if (palette == null || palette.Count == 0)
            {
                return result;
            }

            var paletteLab = new OklabColor[palette.Count];
            for (int i = 0; i < palette.Count; i++)
            {
                paletteLab[i] = OklabColor.FromColor32(palette[i]);
            }

            var cache = new Dictionary<int, Color32>();
            List<int> valid = ValidIndices(colours, mask);
            foreach (int index in valid)
            {
                Color32 c = colours[index];
                int key = (c.r << 16) | (c.g << 8) | c.b;
                if (!cache.TryGetValue(key, out Color32 snapped))
                {
                    snapped = palette[NearestSwatch(OklabColor.FromColor32(c), paletteLab)];
                    cache[key] = snapped;
                }
                result[index] = snapped;
            }
            return result;
        }

        private static int NearestSwatch(OklabColor c, OklabColor[] paletteLab)
        {
            int best = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < paletteLab.Length; i++)
            {
                float gain = Mathf.Max(0f, paletteLab[i].Chroma - c.Chroma);
                float d = c.SquaredDistanceTo(paletteLab[i]) + ChromaGainPenalty * gain * gain;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = i;
                }
            }
            return best;
        }

        private static List<int> ValidIndices(Color32[] colours, bool[]? mask)
        {
            var valid = new List<int>();
            for (int i = 0; i < colours.Length; i++)
            {
                if (mask == null || (i < mask.Length && mask[i]))
                {
                    valid.Add(i);
                }
            }
            return valid;
        }
    }
}
