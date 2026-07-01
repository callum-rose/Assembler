using System.Collections.Generic;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>All the knobs the spike window exposes, bundled for one <see cref="SpikePipeline.Run"/> call.</summary>
    public readonly struct SpikeSettings
    {
        /// <summary>Voxels along the longest bounding-box axis. Kept deliberately coarse for the stylised look.</summary>
        public int MaxDimVoxels { get; init; }

        /// <summary>Voxelise finer then downres to the target, force-keeping thin features.</summary>
        public bool FeatureAware { get; init; }

        /// <summary>Finer-voxelisation multiple for the feature-aware pass.</summary>
        public int FeatureFactor { get; init; }

        /// <summary>Block occupied-fraction needed to fill an output voxel (unless a thin feature forces it).</summary>
        public float FeatureCoverage { get; init; }

        public int TaubinPasses { get; init; }
        public float TaubinLambda { get; init; }
        public float TaubinMu { get; init; }

        /// <summary>Nudge smoothed vertices back onto the SDF iso surface (smooth output only).</summary>
        public bool SurfaceReproject { get; init; }

        public ColourMode ColourMode { get; init; }

        /// <summary>Target colour count for <see cref="MeshToVoxelSpike.ColourMode.PerModelPalette"/>.</summary>
        public int PaletteSize { get; init; }

        /// <summary>Swatches for <see cref="MeshToVoxelSpike.ColourMode.MasterPalette"/>.</summary>
        public IReadOnlyList<Color32>? MasterPalette { get; init; }

        /// <summary>Reject wrong-side thin-wall texel hits during colour reprojection.</summary>
        public bool NormalConsistency { get; init; }

        public ColourModes.Options ColourOptions => new()
        {
            PaletteSize = PaletteSize,
            MasterPalette = MasterPalette,
        };
    }
}
