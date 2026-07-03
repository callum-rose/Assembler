using System.Collections.Generic;

namespace Assembler.AssetGeneration.MeshToVoxel
{
    /// <summary>The three colour-handling modes surfaced as live toggles in the Mesh to Voxel window.</summary>
    public enum ColourMode
    {
        /// <summary>Averaged reprojected colour, untouched — the truest read of the source texture.</summary>
        Raw,

        /// <summary>Flatten to a small per-model palette (k-means over the reprojected colours in Oklab).</summary>
        PerModelPalette,

        /// <summary>Snap each colour to the nearest swatch of a shared master palette (Oklab) for cross-asset cohesion.</summary>
        MasterPalette,

        /// <summary>Keep Raw's faithful colours but merge perceptually-near shades (Oklab tolerance) into the
        /// model's fundamental colours — the output count is emergent, not a fixed target.</summary>
        Consolidated,
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
            public IReadOnlyList<Rgba32>? MasterPalette { get; init; }

            /// <summary>Oklab merge radius for <see cref="ColourMode.Consolidated"/> (0 = exact — no merging).</summary>
            public float ConsolidateTolerance { get; init; }

            /// <summary>Hard cap on <see cref="ColourMode.Consolidated"/>'s output colours (0 = unlimited —
            /// let the tolerance decide). When set, the model is guaranteed to use at most this many colours.</summary>
            public int ConsolidateMaxColours { get; init; }
        }

        /// <summary>
        /// A palette assignment: the snapped colours plus the palette and per-entry labels the Potts
        /// smoother works on. <see cref="Palette"/>/<see cref="Labels"/> are null for
        /// <see cref="ColourMode.Raw"/> (no palette to relabel against); labels are −1 for entries
        /// outside the validity mask.
        /// </summary>
        public readonly struct PaletteAssignment
        {
            public Rgba32[] Colours { get; init; }
            public Rgba32[]? Palette { get; init; }
            public int[]? Labels { get; init; }
        }

        // Mirrors PaletteSnap: penalise snapping a near-neutral colour onto a saturated swatch so hull
        // panels don't turn pink. Only ADDED chroma is charged; desaturating is free.
        private const float ChromaGainPenalty = 8f;

        public static Rgba32[] Apply(Rgba32[] colours, bool[]? mask, ColourMode mode, Options options) =>
            AssignPalette(colours, mask, mode, options).Colours;

        public static PaletteAssignment AssignPalette(Rgba32[] colours, bool[]? mask, ColourMode mode, Options options) =>
            mode switch
            {
                ColourMode.PerModelPalette => PerModelPalette(colours, mask, FMath.Max(1, options.PaletteSize)),
                ColourMode.MasterPalette => MasterPaletteSnap(colours, mask, options.MasterPalette),
                ColourMode.Consolidated => Consolidate(
                    colours, mask, FMath.Max(0f, options.ConsolidateTolerance), FMath.Max(0, options.ConsolidateMaxColours)),
                _ => new PaletteAssignment { Colours = (Rgba32[])colours.Clone() },
            };

        // ---- Per-model palette (deterministic k-means in Oklab) --------------

        private static PaletteAssignment PerModelPalette(Rgba32[] colours, bool[]? mask, int k)
        {
            var result = (Rgba32[])colours.Clone();
            List<int> valid = ValidIndices(colours, mask);
            if (valid.Count == 0)
            {
                return new PaletteAssignment { Colours = result };
            }

            var labs = new OklabColor[valid.Count];
            for (int i = 0; i < valid.Count; i++)
            {
                labs[i] = OklabColor.FromColor32(colours[valid[i]]);
            }

            int clusters = FMath.Min(k, valid.Count);
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

            // Compact empty clusters away (duplicate seeds leave them when PaletteSize exceeds the
            // separable colours) so the palette never carries a mid-grey placeholder swatch that the
            // Potts smoother could assign to voxels despite appearing nowhere in the model.
            var memberCounts = new int[clusters];
            for (int i = 0; i < valid.Count; i++)
            {
                memberCounts[assignment[i]]++;
            }
            var remap = new int[clusters];
            int used = 0;
            for (int c = 0; c < clusters; c++)
            {
                remap[c] = memberCounts[c] > 0 ? used++ : -1;
            }

            Rgba32[] allRepresentatives = ClusterRgbMeans(colours, valid, assignment, clusters);
            var representatives = new Rgba32[used];
            for (int c = 0; c < clusters; c++)
            {
                if (remap[c] >= 0)
                {
                    representatives[remap[c]] = allRepresentatives[c];
                }
            }

            int[] labels = UnassignedLabels(colours.Length);
            for (int i = 0; i < valid.Count; i++)
            {
                int label = remap[assignment[i]];
                result[valid[i]] = representatives[label];
                labels[valid[i]] = label;
            }
            return new PaletteAssignment { Colours = result, Palette = representatives, Labels = labels };
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
        private static Rgba32[] ClusterRgbMeans(Rgba32[] colours, List<int> valid, int[] assignment, int clusters)
        {
            var r = new long[clusters];
            var g = new long[clusters];
            var b = new long[clusters];
            var n = new int[clusters];

            for (int i = 0; i < valid.Count; i++)
            {
                Rgba32 c = colours[valid[i]];
                int k = assignment[i];
                r[k] += c.r;
                g[k] += c.g;
                b[k] += c.b;
                n[k]++;
            }

            var means = new Rgba32[clusters];
            for (int k = 0; k < clusters; k++)
            {
                means[k] = n[k] > 0
                    ? new Rgba32((byte)(r[k] / n[k]), (byte)(g[k] / n[k]), (byte)(b[k] / n[k]), 255)
                    : new Rgba32(128, 128, 128, 255);
            }
            return means;
        }

        // ---- Master-palette snap --------------------------------------------

        private static PaletteAssignment MasterPaletteSnap(Rgba32[] colours, bool[]? mask, IReadOnlyList<Rgba32>? palette)
        {
            var result = (Rgba32[])colours.Clone();
            if (palette == null || palette.Count == 0)
            {
                return new PaletteAssignment { Colours = result };
            }

            var swatches = new Rgba32[palette.Count];
            var paletteLab = new OklabColor[palette.Count];
            for (int i = 0; i < palette.Count; i++)
            {
                swatches[i] = palette[i];
                paletteLab[i] = OklabColor.FromColor32(palette[i]);
            }

            var cache = new Dictionary<int, int>();
            int[] labels = UnassignedLabels(colours.Length);
            List<int> valid = ValidIndices(colours, mask);
            foreach (int index in valid)
            {
                Rgba32 c = colours[index];
                int key = ColourKey(c);
                if (!cache.TryGetValue(key, out int swatch))
                {
                    swatch = NearestSwatch(OklabColor.FromColor32(c), paletteLab);
                    cache[key] = swatch;
                }
                result[index] = swatches[swatch];
                labels[index] = swatch;
            }
            return new PaletteAssignment { Colours = result, Palette = swatches, Labels = labels };
        }

        // ---- Consolidated (Raw fidelity, tolerance-merged into fundamental colours) ----

        // A run of near-identical source shades: a fixed seed (the distance yardstick) plus a
        // frequency-weighted RGB accumulator whose mean becomes the representative swatch.
        private struct ColourCluster
        {
            public OklabColor Seed;
            public long R;
            public long G;
            public long B;
            public int Count;
        }

        // Raw sampling keeps the truest colours but scatters a solid region across dozens of
        // near-duplicate shades. This collapses that variation to the model's *fundamental* colours:
        // leader-cluster the distinct colours in Oklab within <paramref name="tolerance"/>, then — when
        // <paramref name="maxColours"/> is set — agglomeratively merge the nearest clusters down to that
        // hard cap, so the model is locked to a known colour count. Each voxel is repainted with its
        // cluster's frequency-weighted mean.
        private static PaletteAssignment Consolidate(Rgba32[] colours, bool[]? mask, float tolerance, int maxColours)
        {
            var result = (Rgba32[])colours.Clone();
            List<int> valid = ValidIndices(colours, mask);
            if (valid.Count == 0)
            {
                return new PaletteAssignment { Colours = result };
            }

            // Distinct source colours with occurrence counts, so the merge is frequency-weighted:
            // the most common shades seed clusters first and dominate each representative mean.
            var counts = new Dictionary<int, int>();
            foreach (int index in valid)
            {
                int key = ColourKey(colours[index]);
                counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
            }

            // Most-frequent-first, ties broken by the colour key, so the seed choice is deterministic.
            var ordered = new List<KeyValuePair<int, int>>(counts);
            ordered.Sort((x, y) => y.Value != x.Value ? y.Value.CompareTo(x.Value) : x.Key.CompareTo(y.Key));

            // Leader clustering: assign each colour to the nearest seed within tolerance, else start a
            // new cluster. Comparing against the fixed seed (not a drifting centroid) bounds every
            // cluster to `tolerance` of its seed, so genuinely distinct fundamentals never chain-merge.
            float tolSqr = tolerance * tolerance;
            var clusters = new List<ColourCluster>();
            var clusterOfKey = new Dictionary<int, int>(ordered.Count);
            foreach (KeyValuePair<int, int> entry in ordered)
            {
                Rgba32 c = FromColourKey(entry.Key);
                OklabColor lab = OklabColor.FromColor32(c);

                int best = -1;
                float bestSqr = tolSqr;
                for (int i = 0; i < clusters.Count; i++)
                {
                    float d = lab.SquaredDistanceTo(clusters[i].Seed);
                    if (d <= bestSqr)
                    {
                        bestSqr = d;
                        best = i;
                    }
                }
                if (best < 0)
                {
                    best = clusters.Count;
                    clusters.Add(new ColourCluster { Seed = lab });
                }

                ColourCluster cl = clusters[best];
                cl.R += (long)c.r * entry.Value;
                cl.G += (long)c.g * entry.Value;
                cl.B += (long)c.b * entry.Value;
                cl.Count += entry.Value;
                clusters[best] = cl;
                clusterOfKey[entry.Key] = best;
            }

            // Lock to the requested colour count: merge the closest clusters until the cap is met. When
            // it isn't set (or already satisfied) leader indices pass through unchanged.
            int[]? remap = maxColours > 0 && clusters.Count > maxColours
                ? MergeToCount(clusters, maxColours)
                : null;

            var palette = new Rgba32[clusters.Count];
            for (int i = 0; i < clusters.Count; i++)
            {
                palette[i] = MeanColour(clusters[i]);
            }

            int[] labels = UnassignedLabels(colours.Length);
            foreach (int index in valid)
            {
                int leaderLabel = clusterOfKey[ColourKey(colours[index])];
                int label = remap?[leaderLabel] ?? leaderLabel;
                result[index] = palette[label];
                labels[index] = label;
            }
            return new PaletteAssignment { Colours = result, Palette = palette, Labels = labels };
        }

        // Agglomeratively merge the two nearest clusters (Oklab distance between their frequency-weighted
        // means) until at most <paramref name="target"/> survive — the hard cap that locks the model to a
        // known colour count. Anchoring on the accumulated means keeps the survivors the dominant colours,
        // not the chromatic outliers a farthest-point seeding would chase. Compacts <paramref name="clusters"/>
        // to the survivors and returns remap[oldIndex] = newIndex.
        private static int[] MergeToCount(List<ColourCluster> clusters, int target)
        {
            int n = clusters.Count;
            var parent = new int[n];
            var means = new OklabColor[n];
            var alive = new List<int>(n);
            for (int i = 0; i < n; i++)
            {
                parent[i] = i;
                means[i] = MeanLab(clusters[i]);
                alive.Add(i);
            }

            while (alive.Count > target)
            {
                int mergeI = 0;
                int mergeJ = 1;
                float bestSqr = float.MaxValue;
                for (int i = 0; i < alive.Count; i++)
                {
                    for (int j = i + 1; j < alive.Count; j++)
                    {
                        float d = means[alive[i]].SquaredDistanceTo(means[alive[j]]);
                        if (d < bestSqr)
                        {
                            bestSqr = d;
                            mergeI = i;
                            mergeJ = j;
                        }
                    }
                }

                int a = alive[mergeI];
                int b = alive[mergeJ];
                ColourCluster ca = clusters[a];
                ColourCluster cb = clusters[b];
                ca.R += cb.R;
                ca.G += cb.G;
                ca.B += cb.B;
                ca.Count += cb.Count;
                clusters[a] = ca;
                means[a] = MeanLab(ca);
                parent[b] = a;
                alive.RemoveAt(mergeJ);
            }

            var finalIndex = new int[n];
            var survivors = new List<ColourCluster>(alive.Count);
            for (int i = 0; i < alive.Count; i++)
            {
                finalIndex[alive[i]] = i;
                survivors.Add(clusters[alive[i]]);
            }

            var remap = new int[n];
            for (int i = 0; i < n; i++)
            {
                remap[i] = finalIndex[Find(parent, i)];
            }

            clusters.Clear();
            clusters.AddRange(survivors);
            return remap;
        }

        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]]; // path-halving keeps the union-find near-flat
                i = parent[i];
            }
            return i;
        }

        private static Rgba32 MeanColour(ColourCluster c) =>
            new((byte)(c.R / c.Count), (byte)(c.G / c.Count), (byte)(c.B / c.Count), 255);

        private static OklabColor MeanLab(ColourCluster c) => OklabColor.FromColor32(MeanColour(c));

        private static int ColourKey(Rgba32 c) => (c.r << 16) | (c.g << 8) | c.b;

        private static Rgba32 FromColourKey(int key) =>
            new((byte)((key >> 16) & 0xFF), (byte)((key >> 8) & 0xFF), (byte)(key & 0xFF), 255);

        private static int[] UnassignedLabels(int length)
        {
            var labels = new int[length];
            for (int i = 0; i < labels.Length; i++)
            {
                labels[i] = -1;
            }
            return labels;
        }

        private static int NearestSwatch(OklabColor c, OklabColor[] paletteLab)
        {
            int best = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < paletteLab.Length; i++)
            {
                float gain = FMath.Max(0f, paletteLab[i].Chroma - c.Chroma);
                float d = c.SquaredDistanceTo(paletteLab[i]) + ChromaGainPenalty * gain * gain;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = i;
                }
            }
            return best;
        }

        private static List<int> ValidIndices(Rgba32[] colours, bool[]? mask)
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
