namespace Assembler.AssetGeneration.PaletteExtraction
{
    /// <summary>
    /// The knobs for <see cref="PaletteExtractor.Extract"/>. All thresholds bias toward
    /// <b>over-segmentation</b>: under-segmentation is destructive (a whole material vanishes), whereas
    /// an extra near-duplicate swatch is harmless. Distances are in <b>Oklab</b> perceptual space so a
    /// single tolerance behaves consistently across hues. <see cref="Default"/> is tuned against the
    /// module's tuning corpus.
    /// </summary>
    public readonly struct PaletteExtractionOptions
    {
        /// <summary>Oklab radius within which an edge-connected pixel counts as background during the
        /// flood-fill (a band, not an exact match, so mild vignettes are still removed).</summary>
        public float BackgroundTolerance { get; init; }

        /// <summary>Morphological erosion passes applied to the object mask — kills the anti-alias /
        /// JPEG-ringing halo around the silhouette before colours are histogrammed (1–2).</summary>
        public int ErodePixels { get; init; }

        /// <summary>Oklab merge radius handed to <c>ColourModes.Consolidate</c>: shading steps within this
        /// of each other collapse into one fundamental colour. Bias loose.</summary>
        public float MergeTolerance { get; init; }

        /// <summary>Hard cap on the emergent colour count (generous — over-segmentation is safe). The
        /// spurious-cluster defence prunes further, so this is only a ceiling.</summary>
        public int MaxColours { get; init; }

        /// <summary>A cluster covering less than this fraction of the object pixels is a candidate for
        /// dropping — <i>unless</i> it survives the compactness test below.</summary>
        public float MinCoverage { get; init; }

        /// <summary>A low-coverage cluster is kept anyway when its pixels fill at least this fraction of
        /// their bounding box (a compact blob = a real small feature like an eye; a scattered spray =
        /// JPEG/AA speckle to drop).</summary>
        public float MinCompactness { get; init; }

        /// <summary>
        /// Corpus-tuned starting point — passes all 16 tuning images (see the module README for what each
        /// stresses). Sits in the centre of a broad passing region (merge 0.13–0.14, compactness 0.35–0.5)
        /// so it is robust to small JPEG-decode differences rather than balanced on a knife-edge.
        /// </summary>
        public static PaletteExtractionOptions Default => new()
        {
            BackgroundTolerance = 0.10f,
            ErodePixels = 2,
            MergeTolerance = 0.135f,
            MaxColours = 12,
            MinCoverage = 0.03f,
            MinCompactness = 0.40f,
        };
    }
}
