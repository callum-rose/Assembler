using System.Collections.Generic;
using UnityEngine;

namespace VoxelsFromMeshSpike
{
    /// <summary>
    /// Pipeline step 4 — flattens baked shading without knowing the lighting. Meshy bakes
    /// lighting/AO into the texture, so a surface meant to be one flat colour arrives as a
    /// gradient. This:
    ///   1. Segments <b>material regions</b> = spatially-connected runs of similar colour
    ///      (6-connected region-grow with an Oklab similarity threshold between adjacent
    ///      voxels, so a smooth intra-material gradient chains into one region).
    ///   2. Collapses each region to a single <b>dominant</b> colour (most-populous coarse
    ///      bin's average — not the mean, which would drag toward shaded samples).
    ///
    /// Producing the representative <i>before</i> palette-snap is what stops a dark shaded
    /// red from later snapping to brown: the representative is the un-shaded material colour.
    /// </summary>
    public static class DeLight
    {
        public readonly struct Options
        {
            /// <summary>Max Oklab distance between two adjacent voxels for them to join the same region.</summary>
            public float SimilarityThreshold { get; }

            public Options(float similarityThreshold)
            {
                SimilarityThreshold = Mathf.Max(0f, similarityThreshold);
            }

            public static Options Default => new Options(0.10f);
        }

        public static void Apply(VoxModel model, Options options)
        {
            var lab = new OklabColor[model.Occupied.Length];
            for (int i = 0; i < model.Occupied.Length; i++)
            {
                if (model.Occupied[i])
                {
                    lab[i] = OklabColor.FromColor32(model.Colors[i]);
                }
            }

            float thresholdSqr = options.SimilarityThreshold * options.SimilarityThreshold;
            var visited = new bool[model.Occupied.Length];
            var region = new List<int>();
            var stack = new Stack<int>();

            for (int seed = 0; seed < model.Occupied.Length; seed++)
            {
                if (!model.Occupied[seed] || visited[seed])
                {
                    continue;
                }

                region.Clear();
                visited[seed] = true;
                stack.Push(seed);
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    region.Add(i);
                    (int x, int y, int z) = model.Coords(i);
                    foreach ((int dx, int dy, int dz) in VoxModel.FaceNeighbours)
                    {
                        int nx = x + dx, ny = y + dy, nz = z + dz;
                        if (!model.InBounds(nx, ny, nz))
                        {
                            continue;
                        }
                        int n = model.Index(nx, ny, nz);
                        if (model.Occupied[n] && !visited[n] &&
                            lab[i].SquaredDistanceTo(lab[n]) <= thresholdSqr)
                        {
                            visited[n] = true;
                            stack.Push(n);
                        }
                    }
                }

                Color32 representative = DominantColor(model, region);
                foreach (int i in region)
                {
                    model.Colors[i] = representative;
                }
            }
        }

        /// <summary>
        /// The region's dominant colour: coarse-bin to 5 bits/channel (so noisy near-identical
        /// samples pool), take the most-populous bin, and return that bin's average.
        /// </summary>
        private static Color32 DominantColor(VoxModel model, List<int> region)
        {
            var bins = new Dictionary<int, (long r, long g, long b, int n)>();
            int bestKey = 0;
            int bestCount = -1;
            foreach (int i in region)
            {
                Color32 c = model.Colors[i];
                int key = ((c.r >> 3) << 10) | ((c.g >> 3) << 5) | (c.b >> 3);
                bins.TryGetValue(key, out (long r, long g, long b, int n) acc);
                acc = (acc.r + c.r, acc.g + c.g, acc.b + c.b, acc.n + 1);
                bins[key] = acc;
                if (acc.n > bestCount)
                {
                    bestCount = acc.n;
                    bestKey = key;
                }
            }

            (long r, long g, long b, int n) best = bins[bestKey];
            return new Color32(
                (byte)(best.r / best.n),
                (byte)(best.g / best.n),
                (byte)(best.b / best.n),
                255);
        }
    }
}
