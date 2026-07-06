namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// Knobs for an eye-placement run. Defaults target a bilaterally-symmetric creature
    /// seen three-quarter: two eyes, a small outward offset so the eye sits proud of the
    /// surface, and Haiku for the vision call.
    /// </summary>
    public sealed record EyePlacementOptions
    {
        /// <summary>
        /// The camera the model is rendered from and picks are reprojected through. Ignored when
        /// <see cref="AutoOrient"/> is on — the view is then derived from the detected front.
        /// </summary>
        public OrthographicView View { get; init; } = OrthographicView.Isometric;

        /// <summary>
        /// When true, a ring of isometric views is rendered and a vision pass picks the one that best
        /// shows the model's front; that view is then used for eye placement, so eyes land on the
        /// actual face rather than whichever side happened to point at a fixed camera. Costs one extra
        /// vision call (over all candidates). See <see cref="ModelOrientation"/>.
        /// </summary>
        public bool AutoOrient { get; init; } = true;

        /// <summary>Downward tilt of the eye-placement / candidate camera (degrees).</summary>
        public float PitchDegrees { get; init; } = 30f;

        /// <summary>How many isometric candidate views to render around the model for front detection.</summary>
        public int OrientationViewCount { get; init; } = 8;

        /// <summary>How many eyes to place.</summary>
        public int EyeCount { get; init; } = 2;

        /// <summary>Edge of the render (px). Larger reads better for the model but costs more.</summary>
        public int ImageSize { get; init; } = 1024;

        /// <summary>MSAA sample count for the camera render (1/2/4/8). Ignored by the CPU fallback.</summary>
        public int Msaa { get; init; } = 8;

        /// <summary>
        /// How far, in voxels, to push each anchor out along its surface normal. 0 keeps the
        /// anchor on the hit voxel's centre; ~0.5 seats it on the face.
        /// </summary>
        public float SurfaceOffset { get; init; } = 0.5f;

        /// <summary>
        /// Assume the creature's eyes are bilaterally symmetric: place the best-observed eye and
        /// mirror it across the model's symmetry plane for the other, so one reliable pick is
        /// enough and the pair is symmetric by construction. Only applies when <see cref="EyeCount"/>
        /// is 2; other counts snap each pick independently. See <see cref="EyeReprojection"/>.
        /// </summary>
        public bool AssumeSymmetry { get; init; } = true;

        /// <summary>
        /// Radius, in voxels, of the neighbourhood searched around each 2D pick when snapping it to
        /// a real surface voxel. Larger tolerates a less precise pick and recovers picks that graze
        /// the silhouette (which a single ray would drop), at the cost of a few more raycasts. 0
        /// casts only the exact pick.
        /// </summary>
        public float SurfaceSnapRadius { get; init; } = 2f;

        /// <summary>Vision model id; null/blank falls back to <see cref="ImageEyePlacer.DefaultModel"/>.</summary>
        public string? Model { get; init; }
    }
}
