using System;
using System.Collections.Generic;
using Assembler.AssetGeneration.Colour;

namespace Assembler.AssetGeneration.PaletteExtraction
{
    /// <summary>
    /// Engine-free extraction of an object's <b>fundamental colours</b> from a source image, ignoring the
    /// background. Deterministic (no <c>Random</c>/time; stable tie-breaks) so identical input yields
    /// identical output — it runs equally from an editor window, a test, batch mode, or a player build,
    /// taking a raw pixel array rather than a <c>Texture2D</c>.
    ///
    /// Pipeline: detect the background (border median) → mask the object (alpha if present, else a
    /// tolerance flood-fill from the edges) → erode the anti-alias halo → collapse shading steps into
    /// emergent fundamentals via <see cref="ColourModes"/> (Oklab tolerance-merge) → drop spurious
    /// low-coverage-and-scattered clusters (a compact small blob such as an eye survives; a spray of
    /// JPEG/AA speckle does not) → order by coverage.
    /// </summary>
    public static class PaletteExtractor
    {
        public static PaletteResult Extract(Rgba32[] pixels, int width, int height, PaletteExtractionOptions options)
        {
            if (pixels is null)
            {
                throw new ArgumentNullException(nameof(pixels));
            }
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentException("Width and height must be positive.", nameof(width));
            }
            if (pixels.Length < width * height)
            {
                throw new ArgumentException(
                    $"Pixel array ({pixels.Length}) is smaller than width×height ({width * height}).", nameof(pixels));
            }

            Rgba32 background = BorderMedian(pixels, width, height);

            bool[] objectMask = BuildObjectMask(pixels, width, height, background, options.BackgroundTolerance);
            Erode(objectMask, width, height, Math.Max(0, options.ErodePixels));

            int objectPixelCount = CountTrue(objectMask);
            if (objectPixelCount == 0)
            {
                return PaletteResult.Empty(background, objectMask);
            }

            // Collapse the surviving object pixels into their fundamental colours (frequency-weighted,
            // Oklab tolerance-merge, capped) — the engine reused from the voxeliser's colour handling.
            var colourOptions = new ColourModes.Options
            {
                ConsolidateTolerance = Math.Max(0f, options.MergeTolerance),
                ConsolidateMaxColours = Math.Max(0, options.MaxColours),
            };
            ColourModes.PaletteAssignment assignment =
                ColourModes.AssignPalette(pixels, objectMask, ColourMode.Consolidated, colourOptions);

            Rgba32[]? palette = assignment.Palette;
            int[]? labels = assignment.Labels;
            if (palette is null || labels is null || palette.Length == 0)
            {
                return PaletteResult.Empty(background, objectMask);
            }

            return Finalise(palette, labels, objectMask, width, height, objectPixelCount, background, options);
        }

        // ---- Background detection --------------------------------------------

        // Per-channel median of the border pixels — robust to an object that touches an edge (a few object
        // pixels on the border can't move the median) and to a JPEG-noisy background.
        private static Rgba32 BorderMedian(Rgba32[] pixels, int width, int height)
        {
            var r = new List<byte>();
            var g = new List<byte>();
            var b = new List<byte>();

            void Add(int x, int y)
            {
                Rgba32 c = pixels[y * width + x];
                r.Add(c.r);
                g.Add(c.g);
                b.Add(c.b);
            }

            for (int x = 0; x < width; x++)
            {
                Add(x, 0);
                Add(x, height - 1);
            }
            for (int y = 1; y < height - 1; y++)
            {
                Add(0, y);
                Add(width - 1, y);
            }

            return new Rgba32(Median(r), Median(g), Median(b), 255);
        }

        private static byte Median(List<byte> values)
        {
            values.Sort();
            return values[values.Count / 2];
        }

        // ---- Object mask ------------------------------------------------------

        // A real alpha channel is the cheap defensive path; otherwise flood-fill the background inward from
        // the edges so the object may legitimately contain the background colour without being eaten.
        private static bool[] BuildObjectMask(
            Rgba32[] pixels, int width, int height, Rgba32 background, float tolerance)
        {
            int n = width * height;
            var mask = new bool[n];

            if (HasAlphaChannel(pixels, n))
            {
                for (int i = 0; i < n; i++)
                {
                    mask[i] = pixels[i].a >= 128;
                }
                return mask;
            }

            OklabColor bgLab = OklabColor.FromColor32(background);
            float tolSqr = tolerance * tolerance;

            // Pixels within the tolerance band of the background colour — flood candidates.
            var nearBg = new bool[n];
            for (int i = 0; i < n; i++)
            {
                nearBg[i] = OklabColor.FromColor32(pixels[i]).SquaredDistanceTo(bgLab) <= tolSqr;
            }

            // Flood-fill (4-connected) the background inward from every edge pixel that is near-bg. Only
            // near-bg pixels reachable from an edge become background; an interior region that happens to
            // match the background colour but is enclosed by the object stays object.
            var isBg = new bool[n];
            var stack = new Stack<int>();
            for (int x = 0; x < width; x++)
            {
                Seed(x, 0);
                Seed(x, height - 1);
            }
            for (int y = 0; y < height; y++)
            {
                Seed(0, y);
                Seed(width - 1, y);
            }

            void Seed(int x, int y)
            {
                int i = y * width + x;
                if (nearBg[i] && !isBg[i])
                {
                    isBg[i] = true;
                    stack.Push(i);
                }
            }

            while (stack.Count > 0)
            {
                int i = stack.Pop();
                int x = i % width;
                int y = i / width;
                if (x > 0)
                {
                    Spread(i - 1);
                }
                if (x < width - 1)
                {
                    Spread(i + 1);
                }
                if (y > 0)
                {
                    Spread(i - width);
                }
                if (y < height - 1)
                {
                    Spread(i + width);
                }
            }

            void Spread(int j)
            {
                if (nearBg[j] && !isBg[j])
                {
                    isBg[j] = true;
                    stack.Push(j);
                }
            }

            for (int i = 0; i < n; i++)
            {
                mask[i] = !isBg[i];
            }
            return mask;
        }

        // "Real" alpha = at least one meaningfully-transparent pixel. The generators here emit solid
        // backgrounds, so this is normally false and the flood-fill path runs.
        private static bool HasAlphaChannel(Rgba32[] pixels, int n)
        {
            for (int i = 0; i < n; i++)
            {
                if (pixels[i].a < 250)
                {
                    return true;
                }
            }
            return false;
        }

        // Morphological erosion with a 3×3 (8-connected) element, `passes` times: clears the anti-alias /
        // JPEG-ringing halo of mixed object/background colours ringing the silhouette. Image-edge object
        // pixels erode too (their off-image neighbours count as background).
        private static void Erode(bool[] mask, int width, int height, int passes)
        {
            for (int pass = 0; pass < passes; pass++)
            {
                var src = (bool[])mask.Clone();
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * width + x;
                        if (!src[i])
                        {
                            continue;
                        }
                        mask[i] = AllNeighboursSet(src, width, height, x, y);
                    }
                }
            }
        }

        private static bool AllNeighboursSet(bool[] src, int width, int height, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height || !src[ny * width + nx])
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        // ---- Spurious-cluster defence + ordering ------------------------------

        // For each emergent cluster: keep it if it covers enough of the object, OR — when it's small — if
        // its pixels form a compact blob (a real feature like an eye) rather than a scattered spray (JPEG/AA
        // speckle). Dropped clusters' pixels fold into the nearest surviving swatch. The result is ordered
        // by descending coverage.
        private static PaletteResult Finalise(
            Rgba32[] palette, int[] labels, bool[] objectMask, int width, int height,
            int objectPixelCount, Rgba32 background, PaletteExtractionOptions options)
        {
            int k = palette.Length;
            var count = new int[k];
            var minX = new int[k];
            var minY = new int[k];
            var maxX = new int[k];
            var maxY = new int[k];
            for (int c = 0; c < k; c++)
            {
                minX[c] = int.MaxValue;
                minY[c] = int.MaxValue;
                maxX[c] = -1;
                maxY[c] = -1;
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int i = y * width + x;
                    if (!objectMask[i])
                    {
                        continue;
                    }
                    int c = labels[i];
                    if (c < 0)
                    {
                        continue;
                    }
                    count[c]++;
                    if (x < minX[c])
                    {
                        minX[c] = x;
                    }
                    if (y < minY[c])
                    {
                        minY[c] = y;
                    }
                    if (x > maxX[c])
                    {
                        maxX[c] = x;
                    }
                    if (y > maxY[c])
                    {
                        maxY[c] = y;
                    }
                }
            }

            float minCount = options.MinCoverage * objectPixelCount;
            var keep = new bool[k];
            int keptCount = 0;
            for (int c = 0; c < k; c++)
            {
                if (count[c] == 0)
                {
                    continue;
                }
                bool covered = count[c] >= minCount;
                bool compact = Compactness(count[c], minX[c], minY[c], maxX[c], maxY[c]) >= options.MinCompactness;
                if (covered || compact)
                {
                    keep[c] = true;
                    keptCount++;
                }
            }

            // Never prune everything away: if the thresholds reject all clusters, keep the largest so the
            // object always yields at least one colour.
            if (keptCount == 0)
            {
                keep[LargestCluster(count)] = true;
            }

            // Map every cluster to a surviving cluster: survivors map to themselves; a dropped cluster maps
            // to the nearest survivor in Oklab (its pixels repaint to that swatch).
            var survivorLab = new OklabColor[k];
            for (int c = 0; c < k; c++)
            {
                if (keep[c])
                {
                    survivorLab[c] = OklabColor.FromColor32(palette[c]);
                }
            }
            var remap = new int[k];
            for (int c = 0; c < k; c++)
            {
                remap[c] = keep[c] ? c : NearestSurvivor(OklabColor.FromColor32(palette[c]), keep, survivorLab);
            }

            // Final coverage per survivor = its own pixels plus every dropped cluster folded into it.
            var finalCount = new int[k];
            for (int c = 0; c < k; c++)
            {
                if (count[c] > 0)
                {
                    finalCount[remap[c]] += count[c];
                }
            }

            // Order survivors by descending coverage; ties broken by colour key for determinism.
            var survivors = new List<int>();
            for (int c = 0; c < k; c++)
            {
                if (keep[c])
                {
                    survivors.Add(c);
                }
            }
            survivors.Sort((a, b) =>
                finalCount[a] != finalCount[b]
                    ? finalCount[b].CompareTo(finalCount[a])
                    : ColourKey(palette[a]).CompareTo(ColourKey(palette[b])));

            var resultPalette = new Rgba32[survivors.Count];
            var resultCoverage = new int[survivors.Count];
            for (int i = 0; i < survivors.Count; i++)
            {
                resultPalette[i] = palette[survivors[i]];
                resultCoverage[i] = finalCount[survivors[i]];
            }

            return new PaletteResult
            {
                Palette = resultPalette,
                Background = background,
                Coverage = resultCoverage,
                ObjectPixelCount = objectPixelCount,
                ObjectMask = objectMask,
            };
        }

        // Fraction of the cluster's bounding box its pixels fill. A localized blob fills its box densely;
        // a spray scattered across the image has a huge box and a near-zero ratio.
        private static float Compactness(int count, int minX, int minY, int maxX, int maxY)
        {
            if (maxX < minX || maxY < minY)
            {
                return 0f;
            }
            long area = (long)(maxX - minX + 1) * (maxY - minY + 1);
            return area == 0 ? 0f : count / (float)area;
        }

        private static int LargestCluster(int[] count)
        {
            int best = 0;
            for (int c = 1; c < count.Length; c++)
            {
                if (count[c] > count[best])
                {
                    best = c;
                }
            }
            return best;
        }

        private static int NearestSurvivor(OklabColor c, bool[] keep, OklabColor[] survivorLab)
        {
            int best = -1;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < keep.Length; i++)
            {
                if (!keep[i])
                {
                    continue;
                }
                float d = c.SquaredDistanceTo(survivorLab[i]);
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = i;
                }
            }
            return best;
        }

        private static int ColourKey(Rgba32 c) => (c.r << 16) | (c.g << 8) | c.b;

        private static int CountTrue(bool[] mask)
        {
            int n = 0;
            foreach (bool b in mask)
            {
                if (b)
                {
                    n++;
                }
            }
            return n;
        }
    }
}
