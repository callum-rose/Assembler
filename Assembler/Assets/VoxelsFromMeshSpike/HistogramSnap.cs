using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VoxelsFromMeshSpike
{
    /// <summary>
    /// Optional colour-reduction step — snaps every voxel onto one of the model's own
    /// <b>top N histogram peaks</b>. It tallies a histogram of the occupied voxel colours, picks the
    /// most-populous colours as "peaks" (suppressing near-duplicates so the chosen peaks stay spread
    /// out in Oklab rather than all clustering on one dominant hue), then snaps each voxel to its
    /// nearest peak (Oklab).
    ///
    /// Where <see cref="PaletteSnap"/> pulls colours onto a SHARED master set (cross-asset cohesion),
    /// this reduces to the model's OWN dominant colours (per-model economy) — useful as a first pass
    /// to collapse a gradient-ridden model down to a handful of clean source colours before the
    /// master-palette snap maps those onto the shared swatches. Runs after de-light, before
    /// palette-snap. Snapping is cached per distinct colour. Standalone too.
    /// </summary>
    public static class HistogramSnap
    {
        // Minimum Oklab distance a candidate colour must keep from every already-chosen peak to be
        // accepted as a new peak. Without it the top-N collapses onto N near-identical shades of the
        // single most common colour, defeating the point of reducing to N *distinct* colours.
        private const float MinPeakSeparation = 0.06f;

        public static void Apply(VoxModel model, int peakCount)
        {
            if (peakCount < 1)
            {
                return;
            }

            // Histogram of exact voxel colours (24-bit RGB key → occurrence count).
            var counts = new Dictionary<int, int>();
            for (int i = 0; i < model.Occupied.Length; i++)
            {
                if (!model.Occupied[i])
                {
                    continue;
                }
                int key = Key(model.Colors[i]);
                counts.TryGetValue(key, out int n);
                counts[key] = n + 1;
            }

            // Already at or below the target — nothing to reduce.
            if (counts.Count <= peakCount)
            {
                return;
            }

            IReadOnlyList<Color32> peaks = PickPeaks(counts, peakCount);
            OklabColor[] peaksLab = peaks.Select(OklabColor.FromColor32).ToArray();

            var cache = new Dictionary<int, Color32>();
            for (int i = 0; i < model.Occupied.Length; i++)
            {
                if (!model.Occupied[i])
                {
                    continue;
                }
                Color32 c = model.Colors[i];
                int key = Key(c);
                if (!cache.TryGetValue(key, out Color32 snapped))
                {
                    snapped = peaks[NearestIndex(OklabColor.FromColor32(c), peaksLab)];
                    cache[key] = snapped;
                }
                model.Colors[i] = snapped;
            }
        }

        /// <summary>
        /// Greedy non-maximum suppression over the histogram: walk colours most-populous first and
        /// accept each as a peak only if it sits at least <see cref="MinPeakSeparation"/> (Oklab) from
        /// every peak already taken, stopping at <paramref name="peakCount"/>. Returning fewer than N
        /// is correct when the model genuinely has fewer than N visually-distinct colours — the
        /// suppressed candidates are near-duplicates that would snap onto an existing peak anyway.
        /// </summary>
        private static IReadOnlyList<Color32> PickPeaks(Dictionary<int, int> counts, int peakCount)
        {
            float minSepSqr = MinPeakSeparation * MinPeakSeparation;
            var peaks = new List<Color32>();
            var peaksLab = new List<OklabColor>();

            foreach (KeyValuePair<int, int> entry in counts.OrderByDescending(kv => kv.Value))
            {
                if (peaks.Count >= peakCount)
                {
                    break;
                }
                Color32 c = FromKey(entry.Key);
                OklabColor lab = OklabColor.FromColor32(c);
                if (peaksLab.All(p => p.SquaredDistanceTo(lab) >= minSepSqr))
                {
                    peaks.Add(c);
                    peaksLab.Add(lab);
                }
            }

            return peaks;
        }

        private static int NearestIndex(OklabColor c, OklabColor[] peaksLab)
        {
            int best = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < peaksLab.Length; i++)
            {
                float d = c.SquaredDistanceTo(peaksLab[i]);
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = i;
                }
            }
            return best;
        }

        private static int Key(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

        private static Color32 FromKey(int key) =>
            new Color32((byte)(key >> 16), (byte)(key >> 8), (byte)key, 255);
    }
}
