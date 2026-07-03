namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// Knobs for an eye-placement run. Defaults target a bilaterally-symmetric creature
    /// seen three-quarter: two eyes, a small outward offset so the eye sits proud of the
    /// surface, and Haiku for the vision call.
    /// </summary>
    public sealed record EyePlacementOptions
    {
        /// <summary>The camera the model is rendered from and picks are reprojected through.</summary>
        public OrthographicView View { get; init; } = OrthographicView.Isometric;

        /// <summary>How many eyes to place.</summary>
        public int EyeCount { get; init; } = 2;

        /// <summary>Edge of the render (px). Larger reads better for the model but costs more.</summary>
        public int ImageSize { get; init; } = 512;

        /// <summary>
        /// How far, in voxels, to push each anchor out along its surface normal. 0 keeps the
        /// anchor on the hit voxel's centre; ~0.5 seats it on the face.
        /// </summary>
        public float SurfaceOffset { get; init; } = 0.5f;

        /// <summary>Vision model id; null/blank falls back to <see cref="ImageEyePlacer.DefaultModel"/>.</summary>
        public string? Model { get; init; }
    }
}
