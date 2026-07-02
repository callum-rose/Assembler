using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Every intermediate mesh of one pipeline run, in preview order, for
    /// <see cref="SpikeStagePreviewer"/>. Coloured stages carry per-vertex reprojected colours;
    /// grey stages are geometry-only shape-capture intermediates. <see cref="Reprojected"/> is null
    /// when SDF reprojection is off.
    /// </summary>
    public sealed class SpikeStageResult
    {
        /// <summary>(1) The imported mesh, coloured from its own texture.</summary>
        public Mesh Original { get; init; } = null!;

        /// <summary>(2) Raw marching-cubes isosurface (grey).</summary>
        public Mesh Iso { get; init; } = null!;

        /// <summary>(3) Taubin-smoothed isosurface (grey).</summary>
        public Mesh Smoothed { get; init; } = null!;

        /// <summary>(4) Optional SDF-reprojected smooth mesh (grey); null when reprojection is off.</summary>
        public Mesh? Reprojected { get; init; }

        /// <summary>(5) Smooth remesh with reprojected vertex colours — the colour A/B comparison.</summary>
        public Mesh SmoothColoured { get; init; } = null!;

        /// <summary>(6) The primary Crossy-Road blocky voxel model with flat reprojected colours.</summary>
        public Mesh Blocky { get; init; } = null!;

        /// <summary>Voxel count in the (possibly downsampled) occupancy grid the blocky model was built from.</summary>
        public int VoxelCount { get; init; }

        public int GridX { get; init; }
        public int GridY { get; init; }
        public int GridZ { get; init; }

        /// <summary>The (possibly downsampled) occupancy grid the blocky model was built from — the source for .vox export.</summary>
        public VoxelGrid Occupancy { get; init; } = null!;

        /// <summary>Flat reprojected per-voxel colours, indexed by <see cref="VoxelGrid.Index"/> — matches <see cref="Blocky"/>.</summary>
        public Color32[] VoxelColours { get; init; } = null!;

        /// <summary>The objective per-run readout (counts, chosen placement, score terms).</summary>
        public SpikeMetrics Metrics { get; init; }
    }
}
