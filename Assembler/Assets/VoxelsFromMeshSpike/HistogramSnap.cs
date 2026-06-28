using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VoxelsFromMeshSpike
{
    /// <summary>
    /// Optional colour-reduction step — snaps every voxel onto a small set of the model's own
    /// histogram peaks, chosen for <b>maximum variety</b> rather than raw popularity. It tallies a
    /// histogram of the occupied voxel colours, seeds with the single most-populous colour, then
    /// greedily adds the colour that is <i>most perceptually distinct</i> (largest minimum Oklab
    /// distance) from the peaks already chosen — stopping once the next-best candidate is closer than
    /// the variety threshold, or once the safety cap is hit. Finally every voxel snaps to its nearest
    /// peak (Oklab).
    ///
    /// Choosing for variety (farthest-point / max-min) fixes the failure mode of a plain top-N: the N
    /// most <i>common</i> colours can all be near-identical shades of one dominant hue, so a secondary
    /// material never gets a peak. Driving on distance instead keeps the kept colours spread out, and
    /// the count adapts to how colour-rich the model actually is.
    ///
    /// Where <see cref="PaletteSnap"/> pulls colours onto a SHARED master set (cross-asset cohesion),
    /// this reduces to the model's OWN dominant colours (per-model economy) — useful as a first pass
    /// to collapse a gradient-ridden model down to a handful of clean source colours before the
    /// master-palette snap maps those onto the shared swatches. Runs after de-light, before
    /// palette-snap. Snapping is cached per distinct colour. Standalone too.
    /// </summary>
    public static class HistogramSnap
    {
        // A candidate colour must occupy at least this fraction of the model (and at least
        // MinPeakVoxels voxels) to be eligible as a peak. Farthest-point selection is otherwise
        // pathologically drawn to outliers — a few anti-aliasing specks are, by definition, far from
        // everything, so without a population gate the "most distinct" pick is noise. Gated-out
        // colours are still snapped to the nearest surviving peak; they just can't *become* one.
        private const float NoiseFloorFraction = 0.002f;
        private const int MinPeakVoxels = 2;

        /// <summary>
        /// Reduce the model to a variety-selected set of its own dominant colours.
        /// </summary>
        /// <param name="model">The voxel model, mutated in place.</param>
        /// <param name="maxPeaks">Safety cap on how many peaks to keep (upper bound only).</param>
        /// <param name="varietyThreshold">
        /// Minimum Oklab distance a new peak must add over the colours already kept. Selection stops
        /// once no remaining candidate clears it, so this — not <paramref name="maxPeaks"/> — is the
        /// primary control: higher means fewer, more distinct colours. Zero means "keep adding distinct
        /// colours up to the cap".
        /// </param>
        public static void Apply(VoxModel model, int maxPeaks, float varietyThreshold)
        {
            if (maxPeaks < 1)
            {
                return;
            }

            // Histogram of exact voxel colours (24-bit RGB key → occurrence count).
            var counts = new Dictionary<int, int>();
            int total = 0;
            for (int i = 0; i < model.Occupied.Length; i++)
            {
                if (!model.Occupied[i])
                {
                    continue;
                }
                int key = Key(model.Colors[i]);
                counts.TryGetValue(key, out int n);
                counts[key] = n + 1;
                total++;
            }

            // A single colour (or none) — nothing to reduce.
            if (counts.Count <= 1)
            {
                return;
            }

            IReadOnlyList<Color32> peaks = PickPeaks(counts, total, maxPeaks, varietyThreshold);
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
        /// Greedy farthest-point (max-min) selection over the histogram. Seed with the most-populous
        /// colour, then repeatedly add the eligible candidate whose <i>minimum</i> Oklab distance to
        /// the already-chosen peaks is largest — i.e. the one that contributes the most new variety —
        /// until that best distance no longer clears <paramref name="varietyThreshold"/> or the
        /// <paramref name="maxPeaks"/> cap is reached. Candidates are gated by population so noise
        /// specks can't win; on a tiny/uniform model where the gate would leave fewer than two
        /// candidates it is dropped so the model isn't collapsed onto a single colour.
        /// </summary>
        private static IReadOnlyList<Color32> PickPeaks(
            Dictionary<int, int> counts, int total, int maxPeaks, float varietyThreshold)
        {
            int floor = Math.Max(MinPeakVoxels, Mathf.CeilToInt(NoiseFloorFraction * total));

            // Most-populous first (key as a deterministic tie-break) so the seed and any
            // equal-distance picks favour the more common colour.
            List<KeyValuePair<int, int>> ordered = counts
                .Where(kv => kv.Value >= floor)
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key)
                .ToList();
            if (ordered.Count < 2)
            {
                ordered = counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).ToList();
            }

            float varietySqr = varietyThreshold * varietyThreshold;
            var peaks = new List<Color32> { FromKey(ordered[0].Key) };
            var peaksLab = new List<OklabColor> { OklabColor.FromColor32(peaks[0]) };

            while (peaks.Count < maxPeaks)
            {
                OklabColor bestLab = default;
                Color32 best = default;
                float bestMinSqr = -1f;
                foreach (KeyValuePair<int, int> entry in ordered)
                {
                    OklabColor lab = OklabColor.FromColor32(FromKey(entry.Key));
                    float minSqr = float.MaxValue;
                    foreach (OklabColor p in peaksLab)
                    {
                        minSqr = Mathf.Min(minSqr, p.SquaredDistanceTo(lab));
                    }
                    if (minSqr > bestMinSqr)
                    {
                        bestMinSqr = minSqr;
                        best = FromKey(entry.Key);
                        bestLab = lab;
                    }
                }

                // Nothing left that is distinct enough (covers both "all remaining are near-duplicates
                // of a chosen peak" and "every candidate is already chosen", where bestMinSqr == 0).
                if (bestMinSqr <= varietySqr)
                {
                    break;
                }

                peaks.Add(best);
                peaksLab.Add(bestLab);
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
