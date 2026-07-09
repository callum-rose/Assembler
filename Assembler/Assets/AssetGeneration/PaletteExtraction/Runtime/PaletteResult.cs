using System.Collections.Generic;
using Assembler.AssetGeneration.Colour;

namespace Assembler.AssetGeneration.PaletteExtraction
{
    /// <summary>
    /// The outcome of <see cref="PaletteExtractor.Extract"/>: the object's fundamental colours plus the
    /// diagnostics the tuning window renders. <see cref="Palette"/> is ordered by descending coverage and
    /// is the value fed downstream as a voxeliser master palette.
    /// </summary>
    public readonly struct PaletteResult
    {
        /// <summary>The fundamental colours, most-used first. Feed as the voxeliser's master palette.</summary>
        public IReadOnlyList<Rgba32> Palette { get; init; }

        /// <summary>The detected background colour (border median) that was masked out.</summary>
        public Rgba32 Background { get; init; }

        /// <summary>Object-pixel count per swatch, aligned with <see cref="Palette"/> (diagnostics).</summary>
        public IReadOnlyList<int> Coverage { get; init; }

        /// <summary>Total object (non-background, post-erosion) pixels the palette was drawn from.</summary>
        public int ObjectPixelCount { get; init; }

        /// <summary>Row-major object mask (true = object) after background removal and erosion — drives
        /// the window's masked preview. Length = width × height.</summary>
        public bool[] ObjectMask { get; init; }

        /// <summary>An empty result (no object pixels) carrying only the detected background.</summary>
        public static PaletteResult Empty(Rgba32 background, bool[] mask) => new()
        {
            Palette = System.Array.Empty<Rgba32>(),
            Background = background,
            Coverage = System.Array.Empty<int>(),
            ObjectPixelCount = 0,
            ObjectMask = mask,
        };
    }
}
