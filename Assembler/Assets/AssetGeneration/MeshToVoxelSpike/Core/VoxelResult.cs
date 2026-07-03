namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// The lean, engine-free output of <see cref="MeshVoxeliser.Voxelise"/>: the blocky occupancy
    /// grid, its flat per-voxel colours (indexed by <see cref="VoxelGrid.Index"/>), the grid
    /// dimensions, and the objective metrics. No <c>UnityEngine.Mesh</c> — a caller that wants a
    /// renderable mesh builds one from <see cref="Occupancy"/> + <see cref="VoxelColours"/>, and a
    /// caller that wants a MagicaVoxel file passes them to <see cref="SpikeVoxExport"/>.
    /// </summary>
    public sealed class VoxelResult
    {
        /// <summary>The (possibly downsampled) occupancy grid the blocky model is built from.</summary>
        public VoxelGrid Occupancy { get; init; } = null!;

        /// <summary>Flat per-voxel colours, indexed by <see cref="VoxelGrid.Index"/>.</summary>
        public Rgba32[] VoxelColours { get; init; } = null!;

        public int GridX { get; init; }
        public int GridY { get; init; }
        public int GridZ { get; init; }

        /// <summary>Occupied voxel count in the final grid.</summary>
        public int VoxelCount { get; init; }

        /// <summary>The objective per-run readout (counts, chosen placement, score terms).</summary>
        public SpikeMetrics Metrics { get; init; }
    }

    /// <summary>
    /// The richer output of <see cref="MeshVoxeliser.VoxeliseWithPreview"/>: the lean
    /// <see cref="VoxelResult"/> plus the smooth-comparison intermediates as portable
    /// <see cref="g3.DMesh3"/> meshes (NOT <c>UnityEngine.Mesh</c>), so the core stays engine-free.
    /// The editor/preview layer converts these to Unity meshes for display. <see cref="Reprojected"/>
    /// is null when SDF surface reprojection is off.
    /// </summary>
    public sealed class VoxelPreview
    {
        public VoxelResult Voxel { get; init; } = null!;

        /// <summary>The (possibly UV-dilated) source model, for the textured "original" preview.</summary>
        public LoadedModel Model { get; init; } = null!;

        /// <summary>Raw marching-cubes isosurface (before smoothing).</summary>
        public g3.DMesh3 Iso { get; init; } = null!;

        /// <summary>Taubin-smoothed isosurface.</summary>
        public g3.DMesh3 Smoothed { get; init; } = null!;

        /// <summary>Optional SDF-reprojected smooth mesh; null when reprojection is off.</summary>
        public g3.DMesh3? Reprojected { get; init; }

        /// <summary>The smooth mesh whose vertex colours <see cref="SmoothVertexColours"/> holds.</summary>
        public g3.DMesh3 SmoothColoured { get; init; } = null!;

        /// <summary>Per-vertex reprojected colours for <see cref="SmoothColoured"/>, indexed by g3 vertex id.</summary>
        public Rgba32[] SmoothVertexColours { get; init; } = null!;
    }
}
