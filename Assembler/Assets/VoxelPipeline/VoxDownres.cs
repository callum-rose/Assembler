using System.Collections.Generic;
using UnityEngine;

namespace Assembler.VoxelPipeline
{
    /// <summary>
    /// Supersample-and-downres: collapse a high-resolution working model (voxelized at
    /// <see cref="Options.Factor"/>× the target dimension) down to the target grid, one output
    /// voxel per <c>Factor³</c> block. Unlike the post-processing <see cref="IVoxStep"/>s — which
    /// run in place at the final resolution — this <i>changes the grid dimensions</i>, so it is a
    /// standalone transform run between voxelization and the pipeline, not a pipeline step.
    ///
    /// It exists to preserve detail that direct low-res voxelization aliases away. Two levers:
    ///
    /// <para><b>Occupancy (coverage + feature-aware).</b> Each output voxel sees a <i>coverage
    /// fraction</i> (occupied sub-voxels ÷ block size) instead of a single centre hit/miss, so the
    /// shell is anti-aliased. A plain <see cref="Options.CoverageThreshold"/> majority vote still
    /// erases features thinner than one output voxel (an antenna, a fin), so
    /// <see cref="Options.FeatureAware"/> force-keeps any block whose local structure is thinner
    /// than the factor — i.e. a sub-Nyquist feature the coverage vote would otherwise drop.</para>
    ///
    /// <para><b>Colour (salience-weighted vote).</b> Collapsing a block to one colour by raw
    /// majority lets a small bright detail (an eye, a stripe) get outvoted into mush. Instead each
    /// coarse colour cluster's vote is weighted by its perceptual (Oklab) distance from the block
    /// mean, scaled by <see cref="Options.ColourSalience"/> — so a perceptually distinct minority
    /// can win the voxel and stay visible. Salience 0 reduces to a pure majority vote.</para>
    ///
    /// This is the lightweight stand-in for explicit colour/geometry segmentation: it preserves
    /// <i>features</i> without a global segmentation pass. Floater removal (downstream in the
    /// pipeline) still cleans isolated specks the feature-aware keep might let through.
    /// </summary>
    public static class VoxDownres
    {
        /// <summary>Config for one downres pass. <see cref="Default"/> is a sensible A/B starting point.</summary>
        public readonly struct Options
        {
            /// <summary>K: each output voxel aggregates a <c>K³</c> block of the high-res model.</summary>
            public int Factor { get; init; }

            /// <summary>Occupy an output voxel when its block's occupied fraction reaches this (0..1).</summary>
            public float CoverageThreshold { get; init; }

            /// <summary>Also keep blocks whose local structure is thinner than the factor (sub-Nyquist features).</summary>
            public bool FeatureAware { get; init; }

            /// <summary>Boost for perceptually distinct minority colours in the per-voxel vote. 0 = pure majority.</summary>
            public float ColourSalience { get; init; }

            public static Options Default => new()
            {
                Factor = 2,
                CoverageThreshold = 0.5f,
                FeatureAware = true,
                ColourSalience = 1.0f,
            };
        }

        /// <summary>
        /// Downres <paramref name="hi"/> by <see cref="Options.Factor"/>, returning a new model at
        /// <c>ceil(dim / factor)</c> per axis. A factor ≤ 1 is a no-op (returns <paramref name="hi"/>).
        /// </summary>
        public static VoxModel Apply(VoxModel hi, Options options)
        {
            int factor = Mathf.Max(1, options.Factor);
            if (factor == 1)
            {
                return hi;
            }

            int outX = CeilDiv(hi.X, factor);
            int outY = CeilDiv(hi.Y, factor);
            int outZ = CeilDiv(hi.Z, factor);
            var lo = new VoxModel(outX, outY, outZ);

            // Bounded erosion depth (min(thickness, factor)) per high-res voxel, so the colour/occupancy
            // pass can tell a sub-output-voxel-thin feature from a thick surface. Only paid for when on.
            int[]? thickness = options.FeatureAware ? ThicknessMap(hi, factor) : null;

            float coverageThreshold = Mathf.Clamp01(options.CoverageThreshold);
            float salience = Mathf.Max(0f, options.ColourSalience);

            for (int oz = 0; oz < outZ; oz++)
            {
                for (int oy = 0; oy < outY; oy++)
                {
                    for (int ox = 0; ox < outX; ox++)
                    {
                        ResolveBlock(
                            hi, lo, ox, oy, oz, factor, coverageThreshold, salience, thickness);
                    }
                }
            }

            return lo;
        }

        // Aggregate one factor³ high-res block into output voxel (ox,oy,oz).
        private static void ResolveBlock(
            VoxModel hi, VoxModel lo, int ox, int oy, int oz, int factor,
            float coverageThreshold, float salience, int[]? thickness)
        {
            int x0 = ox * factor, y0 = oy * factor, z0 = oz * factor;
            int inBounds = 0;
            int occupied = 0;
            int maxThickness = 0;
            var colours = new List<Color32>();

            for (int dz = 0; dz < factor; dz++)
            {
                int z = z0 + dz;
                if (z >= hi.Z)
                {
                    break;
                }
                for (int dy = 0; dy < factor; dy++)
                {
                    int y = y0 + dy;
                    if (y >= hi.Y)
                    {
                        break;
                    }
                    for (int dx = 0; dx < factor; dx++)
                    {
                        int x = x0 + dx;
                        if (x >= hi.X)
                        {
                            break;
                        }

                        inBounds++;
                        int i = hi.Index(x, y, z);
                        if (!hi.Occupied[i])
                        {
                            continue;
                        }
                        occupied++;
                        colours.Add(hi.Colors[i]);
                        if (thickness != null && thickness[i] > maxThickness)
                        {
                            maxThickness = thickness[i];
                        }
                    }
                }
            }

            if (occupied == 0)
            {
                return;
            }

            // Coverage vote, with a feature-aware override for structures thinner than one output
            // voxel — those are sub-Nyquist and would otherwise be erased by the coverage majority.
            float coverage = (float)occupied / inBounds;
            bool thinFeature = thickness != null && maxThickness < factor;
            if (coverage < coverageThreshold && !thinFeature)
            {
                return;
            }

            int li = lo.Index(ox, oy, oz);
            lo.Occupied[li] = true;
            lo.Colors[li] = ChooseColour(colours, salience);
        }

        /// <summary>
        /// One colour for a block of occupied high-res voxels. Samples are pooled into coarse
        /// perceptual clusters; the winning cluster's mean is returned. With <paramref name="salience"/>
        /// at 0 this is a raw majority vote; above 0, each cluster's vote is multiplied by
        /// <c>1 + salience · OklabDistance(clusterMean, blockMean)</c>, so a perceptually distinct
        /// minority can outvote a larger bland majority and survive the downres.
        /// </summary>
        private static Color32 ChooseColour(IReadOnlyList<Color32> colours, float salience)
        {
            // Coarse 5-bit/channel clusters (matches the converter's surface-colour binning) so
            // near-identical samples pool into one vote rather than fragmenting.
            var bins = new Dictionary<int, (long r, long g, long b, int n)>();
            long sumR = 0, sumG = 0, sumB = 0;
            foreach (Color32 c in colours)
            {
                int key = ((c.r >> 3) << 10) | ((c.g >> 3) << 5) | (c.b >> 3);
                bins.TryGetValue(key, out (long r, long g, long b, int n) acc);
                bins[key] = (acc.r + c.r, acc.g + c.g, acc.b + c.b, acc.n + 1);
                sumR += c.r;
                sumG += c.g;
                sumB += c.b;
            }

            int total = colours.Count;
            var blockMeanLab = OklabColor.FromColor32(
                new Color32((byte)(sumR / total), (byte)(sumG / total), (byte)(sumB / total), 255));

            (long r, long g, long b, int n) best = default;
            float bestWeight = -1f;
            foreach ((long r, long g, long b, int n) acc in bins.Values)
            {
                var mean = new Color32((byte)(acc.r / acc.n), (byte)(acc.g / acc.n), (byte)(acc.b / acc.n), 255);
                float weight = salience <= 0f
                    ? acc.n
                    : acc.n * (1f + salience * OklabColor.FromColor32(mean).DistanceTo(blockMeanLab));
                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    best = acc;
                }
            }

            return new Color32((byte)(best.r / best.n), (byte)(best.g / best.n), (byte)(best.b / best.n), 255);
        }

        /// <summary>
        /// Bounded morphological-erosion depth per occupied voxel, capped at <paramref name="cap"/>:
        /// surface voxels (an empty face-neighbour, or the grid edge) erode at pass 1 → depth 1; a
        /// voxel that survives all <paramref name="cap"/> passes is at least that deep → depth = cap.
        /// Used to spot features thinner than the downres factor. Out-of-bounds counts as empty, so
        /// the model's outer shell erodes inward as expected.
        /// </summary>
        private static int[] ThicknessMap(VoxModel hi, int cap)
        {
            int n = hi.Occupied.Length;
            var thickness = new int[n];
            var current = (bool[])hi.Occupied.Clone();
            var next = new bool[n];

            for (int pass = 1; pass <= cap; pass++)
            {
                bool anyRemoved = false;
                for (int z = 0; z < hi.Z; z++)
                {
                    for (int y = 0; y < hi.Y; y++)
                    {
                        for (int x = 0; x < hi.X; x++)
                        {
                            int i = hi.Index(x, y, z);
                            if (!current[i])
                            {
                                continue;
                            }

                            if (IsInterior(current, hi, x, y, z))
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

            // Whatever still stands after the capped passes is at least `cap` deep.
            for (int i = 0; i < n; i++)
            {
                if (current[i])
                {
                    thickness[i] = cap;
                }
            }
            return thickness;
        }

        // A voxel is interior (survives erosion) only if all six face-neighbours are still present;
        // a neighbour off the grid counts as empty, so edge voxels are never interior.
        private static bool IsInterior(bool[] current, VoxModel hi, int x, int y, int z)
        {
            foreach ((int dx, int dy, int dz) in VoxModel.FaceNeighbours)
            {
                int nx = x + dx, ny = y + dy, nz = z + dz;
                if (!hi.InBounds(nx, ny, nz) || !current[hi.Index(nx, ny, nz)])
                {
                    return false;
                }
            }
            return true;
        }

        private static int CeilDiv(int a, int b) => (a + b - 1) / b;
    }
}
