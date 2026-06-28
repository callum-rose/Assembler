using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VoxelsFromMeshSpike
{
    /// <summary>
    /// Collapses noisy per-voxel colours onto a small set of "basic" colours.
    ///
    /// The colour sampled per voxel is noisy (bilinear texture filtering + JPEG-ish
    /// artefacts) and shows soft gradients at material boundaries. This:
    ///   1. <b>Extracts the basic colours</b> — the dominant, well-populated colours,
    ///      found by coarse-binning (to swallow noise) then greedily taking the most
    ///      popular bins that are far enough apart and big enough to be a real region.
    ///   2. <b>Snaps every voxel</b> to its nearest basic colour. Because each voxel
    ///      picks the single nearest basic colour, a soft gradient between two regions
    ///      becomes a hard step (each voxel falls to one side of the boundary).
    ///
    /// Assumes (as the source art does) that regions are roughly flat with distinctive
    /// boundaries, so gradient/anti-aliased voxels are comparatively rare — the
    /// min-region gate keeps those thin transition bands from becoming basic colours.
    /// </summary>
    public static class ColorQuantizer
    {
        public readonly struct Options
        {
            /// <summary>Hard cap on the number of basic colours.</summary>
            public int MaxColors { get; }

            /// <summary>
            /// Colours closer than this (Euclidean distance in normalised [0,1] RGB,
            /// range 0..~1.73) are treated as the same basic colour and merged.
            /// </summary>
            public float SimilarityThreshold { get; }

            /// <summary>
            /// A colour must cover at least this fraction (0..1) of all voxels to
            /// qualify as a basic colour — filters thin gradient/noise bands.
            /// </summary>
            public float MinRegionFraction { get; }

            public Options(int maxColors, float similarityThreshold, float minRegionFraction)
            {
                MaxColors = Mathf.Max(1, maxColors);
                SimilarityThreshold = Mathf.Max(0f, similarityThreshold);
                MinRegionFraction = Mathf.Clamp01(minRegionFraction);
            }

            public static Options Default => new Options(16, 0.12f, 0.01f);
        }

        /// <summary>Returns a copy of <paramref name="source"/> with every voxel snapped to a basic colour.</summary>
        public static VoxResult Quantise(VoxResult source, Options options)
        {
            IReadOnlyList<Color32> palette = ExtractPalette(source.Cells, options);
            if (palette.Count == 0)
            {
                return source;
            }

            Vector3[] paletteVecs = palette.Select(AsVector).ToArray();
            var cells = new List<VoxCell>(source.Cells.Count);
            foreach (VoxCell cell in source.Cells)
            {
                int idx = NearestIndex(cell.Color, paletteVecs);
                cells.Add(new VoxCell(cell.X, cell.Y, cell.Z, palette[idx]));
            }

            return new VoxResult(source.GridX, source.GridY, source.GridZ, cells);
        }

        /// <summary>Extracts the dominant "basic" colours from the voxel set.</summary>
        public static IReadOnlyList<Color32> ExtractPalette(IReadOnlyList<VoxCell> cells, Options options)
        {
            if (cells.Count == 0)
            {
                return Array.Empty<Color32>();
            }

            // Coarse-bin to 5 bits/channel so near-identical (noisy) colours pool together.
            var bins = new Dictionary<int, long[]>(); // key -> [sumR, sumG, sumB, count]
            foreach (VoxCell cell in cells)
            {
                Color32 c = cell.Color;
                int key = ((c.r >> 3) << 10) | ((c.g >> 3) << 5) | (c.b >> 3);
                if (!bins.TryGetValue(key, out long[]? acc))
                {
                    acc = new long[4];
                    bins[key] = acc;
                }
                acc[0] += c.r;
                acc[1] += c.g;
                acc[2] += c.b;
                acc[3] += 1;
            }

            long minPop = (long)Math.Floor(cells.Count * options.MinRegionFraction);

            var ordered = bins.Values
                .Select(a => (mean: new Vector3(a[0], a[1], a[2]) / (a[3] * 255f), pop: a[3]))
                .OrderByDescending(x => x.pop)
                .ToList();

            var palette = new List<Vector3>();
            foreach ((Vector3 mean, long pop) in ordered)
            {
                if (palette.Count >= options.MaxColors)
                {
                    break;
                }
                // Bins are population-sorted, so once one is too small the rest are too.
                if (pop < minPop)
                {
                    break;
                }
                if (palette.All(p => Vector3.Distance(p, mean) >= options.SimilarityThreshold))
                {
                    palette.Add(mean);
                }
            }

            // If the gate filtered everything (very fragmented art), keep the dominant colour.
            if (palette.Count == 0)
            {
                palette.Add(ordered[0].mean);
            }

            return palette.Select(AsColor32).ToList();
        }

        private static int NearestIndex(Color32 c, Vector3[] paletteVecs)
        {
            Vector3 v = AsVector(c);
            int best = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < paletteVecs.Length; i++)
            {
                float d = (v - paletteVecs[i]).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = i;
                }
            }
            return best;
        }

        private static Vector3 AsVector(Color32 c) => new Vector3(c.r, c.g, c.b) / 255f;

        private static Color32 AsColor32(Vector3 c) => new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.x * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.y * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(c.z * 255f), 0, 255),
            255);
    }
}
