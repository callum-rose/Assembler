using System.IO;
using Assembler.AssetGeneration.Colour;
using NUnit.Framework;
using UnityEngine;

namespace Assembler.AssetGeneration.PaletteExtraction.Tests
{
    /// <summary>
    /// The tuning gate: runs <see cref="PaletteExtractor"/> with <see cref="PaletteExtractionOptions.Default"/>
    /// over the tuning corpus and asserts each image resolves to its expected count of fundamental colours.
    /// If a change to the algorithm or the defaults regresses a stressor, the offending image fails here.
    /// Each expected range is the image's intended fundamentals ±1 (see the module README for what each
    /// stresses); the default options are tuned to land every image inside its range.
    /// </summary>
    public sealed class PaletteCorpusTests
    {
        private static string CorpusDir =>
            Path.Combine(Application.dataPath, "AssetGeneration", "PaletteExtraction", "TuningCorpus");

        // slug, expected-min, expected-max.
        [TestCase("turtle", 3, 4)]
        [TestCase("fox", 3, 4)]
        [TestCase("snowman", 3, 5)]
        [TestCase("crate", 1, 2)]
        [TestCase("tree", 2, 3)]
        [TestCase("ladybug", 2, 3)]
        [TestCase("penguin", 3, 4)]
        [TestCase("chicken", 3, 4)]
        [TestCase("panda", 2, 3)]
        [TestCase("robot", 3, 4)]
        [TestCase("cat", 3, 5)]
        [TestCase("traffic-cone", 2, 3)]
        [TestCase("parrot", 6, 8)]
        [TestCase("blue-whale", 3, 4)]
        [TestCase("bee", 3, 4)]
        [TestCase("gem", 1, 3)]
        public void ExtractsExpectedFundamentalCount(string slug, int min, int max)
        {
            PaletteResult result = ExtractCorpusImage(slug);
            Assert.That(
                result.Palette.Count,
                Is.InRange(min, max),
                $"{slug}: expected {min}-{max} fundamentals, got {result.Palette.Count} " +
                $"[{DescribePalette(result)}]");
        }

        private static PaletteResult ExtractCorpusImage(string slug)
        {
            var path = Path.Combine(CorpusDir, slug + ".jpg");
            Assert.That(File.Exists(path), Is.True, $"Missing corpus image: {path}");

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(path)), Is.True,
                    $"Failed to decode {slug}.jpg");

                Color32[] c = texture.GetPixels32();
                var pixels = new Rgba32[c.Length];
                for (int i = 0; i < c.Length; i++)
                {
                    pixels[i] = new Rgba32(c[i].r, c[i].g, c[i].b, c[i].a);
                }
                return PaletteExtractor.Extract(pixels, texture.width, texture.height, PaletteExtractionOptions.Default);
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        private static string DescribePalette(PaletteResult result)
        {
            var parts = new string[result.Palette.Count];
            for (int i = 0; i < result.Palette.Count; i++)
            {
                Rgba32 s = result.Palette[i];
                parts[i] = $"#{s.r:X2}{s.g:X2}{s.b:X2}";
            }
            return string.Join(" ", parts);
        }
    }
}
