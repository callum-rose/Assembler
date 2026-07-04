using System.Collections.Generic;
using Assembler.AssetGeneration.Colour;
using NUnit.Framework;

namespace Assembler.AssetGeneration.PaletteExtraction.Tests
{
    /// <summary>
    /// Tuning-independent behaviour of <see cref="PaletteExtractor"/> on synthetic images — determinism,
    /// background detection, flood-fill masking, and the empty case. These assert structure, not the
    /// corpus-tuned colour counts (that is <c>PaletteCorpusTests</c>).
    /// </summary>
    public sealed class PaletteExtractorTests
    {
        private static readonly Rgba32 Blue = new(40, 90, 200, 255);
        private static readonly Rgba32 Red = new(200, 40, 40, 255);
        private static readonly Rgba32 Green = new(40, 180, 60, 255);

        // A framed image: a solid background border with a rectangular object interior painted by `fill`.
        private static Rgba32[] Framed(int width, int height, Rgba32 background, int inset,
            System.Func<int, int, Rgba32> fill)
        {
            var pixels = new Rgba32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool border = x < inset || y < inset || x >= width - inset || y >= height - inset;
                    pixels[y * width + x] = border ? background : fill(x, y);
                }
            }
            return pixels;
        }

        [Test]
        public void DetectsBackgroundAsBorderColour()
        {
            Rgba32[] pixels = Framed(64, 64, Blue, 6, (_, _) => Red);
            PaletteResult result = PaletteExtractor.Extract(pixels, 64, 64, PaletteExtractionOptions.Default);

            Assert.That(result.Background, Is.EqualTo(Blue));
        }

        [Test]
        public void ExtractsTwoDistinctInteriorColours()
        {
            // Left half red, right half green, framed by a blue background.
            Rgba32[] pixels = Framed(80, 80, Blue, 8, (x, _) => x < 40 ? Red : Green);
            PaletteResult result = PaletteExtractor.Extract(pixels, 80, 80, PaletteExtractionOptions.Default);

            Assert.That(result.Palette.Count, Is.EqualTo(2), "red and green are far apart in Oklab");
            Assert.That(result.ObjectPixelCount, Is.GreaterThan(0));
            CollectionAssert.Contains(new List<Rgba32>(result.Palette), Red);
            CollectionAssert.Contains(new List<Rgba32>(result.Palette), Green);
        }

        [Test]
        public void OrdersPaletteByDescendingCoverage()
        {
            // Three-quarters red, one-quarter green → red must come first.
            Rgba32[] pixels = Framed(80, 80, Blue, 8, (x, _) => x < 58 ? Red : Green);
            PaletteResult result = PaletteExtractor.Extract(pixels, 80, 80, PaletteExtractionOptions.Default);

            Assert.That(result.Palette.Count, Is.EqualTo(2));
            Assert.That(result.Palette[0], Is.EqualTo(Red));
            Assert.That(result.Coverage[0], Is.GreaterThan(result.Coverage[1]));
        }

        [Test]
        public void IsDeterministic()
        {
            Rgba32[] pixels = Framed(96, 96, Blue, 6, (x, y) => (x + y) % 3 == 0 ? Red : Green);

            PaletteResult a = PaletteExtractor.Extract(pixels, 96, 96, PaletteExtractionOptions.Default);
            PaletteResult b = PaletteExtractor.Extract(pixels, 96, 96, PaletteExtractionOptions.Default);

            CollectionAssert.AreEqual(new List<Rgba32>(a.Palette), new List<Rgba32>(b.Palette));
            CollectionAssert.AreEqual(new List<int>(a.Coverage), new List<int>(b.Coverage));
        }

        [Test]
        public void AllBackgroundYieldsEmptyPalette()
        {
            var pixels = new Rgba32[32 * 32];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Blue;
            }

            PaletteResult result = PaletteExtractor.Extract(pixels, 32, 32, PaletteExtractionOptions.Default);

            Assert.That(result.Palette.Count, Is.EqualTo(0));
            Assert.That(result.ObjectPixelCount, Is.EqualTo(0));
            Assert.That(result.Background, Is.EqualTo(Blue));
        }

        [Test]
        public void ObjectMayContainTheBackgroundColour()
        {
            // A red object with a fully-enclosed blue core: flood-fill from the edges can't reach the core,
            // so it stays object — proving the mask is connectivity-based, not a global colour match.
            Rgba32[] pixels = Framed(80, 80, Blue, 10, (x, y) =>
                x >= 30 && x < 50 && y >= 30 && y < 50 ? Blue : Red);

            PaletteResult result = PaletteExtractor.Extract(pixels, 80, 80, PaletteExtractionOptions.Default);

            // The enclosed blue survives as a fundamental alongside red (not merged, far apart in Oklab).
            CollectionAssert.Contains(new List<Rgba32>(result.Palette), Blue);
            CollectionAssert.Contains(new List<Rgba32>(result.Palette), Red);
        }
    }
}
