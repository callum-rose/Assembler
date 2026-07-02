using System.Collections.Generic;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>How the coarse resolution is specified in the window.</summary>
    public enum ResolutionInput
    {
        /// <summary>Direct max-dimension voxel slider.</summary>
        MaxDimSlider,

        /// <summary>Derived from in-game size ÷ the shared global voxel size.</summary>
        WorldSize,
    }

    /// <summary>All the knobs the spike window exposes, bundled for one <see cref="SpikePipeline.Run"/> call.</summary>
    public readonly struct SpikeSettings
    {
        // ---- Resolution -------------------------------------------------------

        public ResolutionInput ResolutionInput { get; init; }

        /// <summary>Voxels along the longest bounding-box axis (<see cref="ResolutionInput.MaxDimSlider"/> mode).</summary>
        public int MaxDimVoxels { get; init; }

        /// <summary>Shared global voxel edge length, world units (<see cref="ResolutionInput.WorldSize"/> mode).</summary>
        public float VoxelWorldSize { get; init; }

        /// <summary>Intended in-game size of the model's longest axis, world units.</summary>
        public float TargetWorldSize { get; init; }

        /// <summary>The effective max-dimension voxel count for this run, clamped to the supported 4–96 range.</summary>
        public int ResolveMaxDimVoxels() => Mathf.Clamp(
            ResolutionInput == ResolutionInput.WorldSize && VoxelWorldSize > 0f
                ? Mathf.RoundToInt(TargetWorldSize / VoxelWorldSize)
                : MaxDimVoxels,
            4, 96);

        // ---- Geometry ---------------------------------------------------------

        /// <summary>Scored grid-placement search (phases × scale flex); off = the identity placement.</summary>
        public bool GridSearch { get; init; }

        /// <summary>Let the search snap model extents to whole voxel counts (±10% stretch).</summary>
        public bool ScaleFlex { get; init; }

        /// <summary>Force-keep sub-Nyquist features connected to the main body (legs, ears, antennae).</summary>
        public bool ThinFeatureKeep { get; init; }

        /// <summary>Fine-voxelisation multiple (2–4) used when the search or thin-keep is on.</summary>
        public int FineFactor { get; init; }

        /// <summary>Block occupied-fraction needed to fill an output voxel (unless thin-keep forces it).</summary>
        public float Coverage { get; init; }

        /// <summary>Drop coarse components whose fine support never touches the fine main component.</summary>
        public bool RemoveFloaters { get; init; }

        /// <summary>Protected morphological close→open radius (0 = off, 1–2).</summary>
        public int CleanupStrength { get; init; }

        /// <summary>The effective fine factor: 1 (no fine pass) unless the search or thin-keep needs one.</summary>
        public int ResolveFineFactor() =>
            GridSearch || ThinFeatureKeep ? Mathf.Clamp(FineFactor, 2, 4) : 1;

        // ---- Search score weights (advanced) -----------------------------------

        public float FaceWeight { get; init; }
        public float IouWeight { get; init; }
        public float GapWeight { get; init; }

        /// <summary>Colour-boundary alignment weight — speculative and costly, ships at 0.</summary>
        public float ColWeight { get; init; }

        public GridPlacementSearch.Options SearchOptions => new()
        {
            Coverage = Coverage,
            ThinFeatureKeep = ThinFeatureKeep,
            ScaleFlex = ScaleFlex,
            FaceWeight = FaceWeight,
            IouWeight = IouWeight,
            GapWeight = GapWeight,
            ColWeight = ColWeight,
        };

        // ---- Colour -----------------------------------------------------------

        /// <summary>Dilate UV-island colours into the texture gutters at load (kills Meshy's purple bleed).</summary>
        public bool UvDilate { get; init; }

        public int UvDilatePasses { get; init; }

        /// <summary>Multi-sample per surface voxel with Oklab-medoid aggregation instead of one centre sample.</summary>
        public bool MultiSampleColour { get; init; }

        /// <summary>Edge-aware Potts label smoothing strength (0 = off).</summary>
        public float PottsStrength { get; init; }

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

        // ---- Smooth comparison path --------------------------------------------

        public int TaubinPasses { get; init; }
        public float TaubinLambda { get; init; }
        public float TaubinMu { get; init; }

        /// <summary>Nudge smoothed vertices back onto the SDF iso surface (smooth output only).</summary>
        public bool SurfaceReproject { get; init; }

        /// <summary>Sensible starting point matching the plan's defaults; the window overrides from EditorPrefs.</summary>
        public static SpikeSettings Defaults => new()
        {
            ResolutionInput = ResolutionInput.MaxDimSlider,
            MaxDimVoxels = 24,
            VoxelWorldSize = 0.1f,
            TargetWorldSize = 2f,
            GridSearch = true,
            ScaleFlex = true,
            ThinFeatureKeep = true,
            FineFactor = 3,
            Coverage = 0.5f,
            RemoveFloaters = true,
            CleanupStrength = 1,
            FaceWeight = 1f,
            IouWeight = 1f,
            GapWeight = 2f,
            ColWeight = 0f,
            UvDilate = true,
            UvDilatePasses = UvIslandDilation.DefaultPasses,
            MultiSampleColour = true,
            PottsStrength = 0.5f,
            ColourMode = ColourMode.PerModelPalette,
            PaletteSize = 8,
            TaubinPasses = 5,
            TaubinLambda = 0.5f,
            TaubinMu = 0.53f,
        };
    }
}
