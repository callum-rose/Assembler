using System;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxel.Generation
{
    /// <summary>
    /// The mutable, reflectable mirror of the engine-free <see cref="Settings"/> struct that the AI
    /// layer chooses. <see cref="Settings"/> is a readonly struct with <c>init</c> properties, so it
    /// carries no <c>UnityEngine</c> attributes and <see cref="UnityEngine.JsonUtility"/> cannot
    /// overwrite it. This <see cref="SerializableAttribute"/> class of public fields (each with the
    /// Mesh to Voxel window's <see cref="TooltipAttribute"/>/<see cref="RangeAttribute"/>) is what the prompt
    /// builder reflects over and what the parser <see cref="UnityEngine.JsonUtility.FromJsonOverwrite"/>s
    /// a partial object onto; <see cref="ToSettings"/> then builds the struct the pipeline runs.
    ///
    /// It mirrors every tunable field of <see cref="Settings"/> except the master palette (supplied by
    /// the window's palette asset, never by the AI). The resolution can be driven either directly via
    /// <see cref="MaxDimVoxels"/> or from physical scale via <see cref="ResolutionInput"/> =
    /// <see cref="MeshToVoxel.ResolutionInput.WorldSize"/> with <see cref="VoxelWorldSize"/> /
    /// <see cref="TargetWorldSize"/>.
    /// </summary>
    [Serializable]
    public sealed class SettingsConfig
    {
        // ---- Resolution -------------------------------------------------------

        [Tooltip("How the coarse resolution is chosen. MaxDimSlider uses MaxDimVoxels directly. WorldSize instead derives it from TargetWorldSize ÷ VoxelWorldSize (clamped to 4–96), so the object's physical scale drives the voxel count.")]
        public ResolutionInput ResolutionInput = ResolutionInput.MaxDimSlider;

        [Range(4, 96)]
        [Tooltip("MaxDimSlider mode: voxels along the longest bounding-box axis; the other axes scale to match. Keep it low (~10–16 for characters) for the chunky stylised read; the pipeline behaves across the whole 4–96 range.")]
        public int MaxDimVoxels = 24;

        [Range(0.01f, 1f)]
        [Tooltip("WorldSize mode: the edge length of one voxel in world metres. With TargetWorldSize this sets the resolution — e.g. a 2 m object at 0.1 gives 20 voxels along its longest axis.")]
        public float VoxelWorldSize = 0.1f;

        [Range(0.1f, 100f)]
        [Tooltip("WorldSize mode: your best estimate of the object's longest real-world dimension in metres (a mug ~0.12, a chair ~1, a car ~4, a house ~8). Ignored in MaxDimSlider mode.")]
        public float TargetWorldSize = 2f;

        // ---- Geometry ---------------------------------------------------------

        [Tooltip("Score candidate grid phases/scales against the fine grid (face economy, IoU, air-gap preservation) and voxelise on the winner. Off = the fixed identity placement.")]
        public bool GridSearch = true;

        [Tooltip("Let the search also stretch the voxel grid per-axis to snap the model's extent onto a whole voxel count (a 7.5-voxel-long bar becomes exactly 7 or 8), clamped to ±10%. Needs the grid search on.")]
        public bool ScaleFlex = true;

        [Tooltip("Force-keep sub-voxel silhouette features (legs, ears, antennae, a mug handle) that a plain coverage vote would erase — but only where they connect to the main body, so disconnected specks still die.")]
        public bool ThinFeatureKeep = true;

        [Range(2, 4)]
        [Tooltip("The grid search and thin-keep first voxelise at this multiple of the target resolution to analyse features, then vote down. Higher = finer analysis but the fine grid grows as factor³ — the main cost driver. Only used when the search or thin-keep is on.")]
        public int FineFactor = 3;

        [Range(0f, 1f)]
        [Tooltip("Fraction of a coarse voxel's fine cells that must be solid for it to fill (unless thin-keep forces it). Higher trims jagged one-voxel slivers off diagonal surfaces for a boxier read; lower keeps more bulk.")]
        public float Coverage = 0.5f;

        [Tooltip("Drop disconnected voxel islands whose fine support never touches the model's main connected component — the stray specks left by messy geometry. The largest island is always kept, so the model can never vanish.")]
        public bool RemoveFloaters = true;

        [Range(0, 2)]
        [Tooltip("Ranked morphological close→open passes: close fills lone pits/notches, open shaves lone bumps/spikes — flatter faces, cleaner silhouette. Never shaves kept thin features, never welds real air gaps. 1 = one pass, 2 = stronger, 0 = off.")]
        public int CleanupStrength = 1;

        [Tooltip("Fill concave corners/notches so the silhouette boxes out. An empty voxel fills when three same-colour neighbours meet it at a shared vertex, or it is walled in by a deep-enough pocket of occupied neighbours. Real air gaps are protected from the corner fill. Repeats until stable.")]
        public bool FillCorners = false;

        [Range(0f, 0.5f)]
        [Tooltip("How close two neighbour colours must be (Oklab distance) to count as the same for the corner-fill same-colour rule. 0 = exact match; ~0.1 is a good start. Too high and distinct regions merge.")]
        public float CornerFillColourTolerance = 0.1f;

        [Range(4, 6)]
        [Tooltip("How many occupied face-neighbours (out of 6) wall a cell in enough to fill it regardless of colour. Higher = more conservative (fewer pockets filled). 5 is a good start.")]
        public int CornerFillNeighbourThreshold = CornerFill.DefaultNeighbourThreshold;

        [Tooltip("Only fill a deep pocket when its modal (most common) neighbour colour is a strict majority, so a pocket where two colour regions meet is left open rather than smeared with an arbitrary side.")]
        public bool CornerFillRequireMajority = true;

        [Tooltip("Force mirror-symmetry across the centre of the occupied bounds on each ticked axis (X/Y/Z grid axes, Y is up). Applied last, so the silhouette is guaranteed symmetric. Comma-separated names, e.g. \"X\" or \"X,Y\"; None = off. Default (ForceMirror off) unions both halves' features.")]
        public SymmetryAxes Symmetry = SymmetryAxes.None;

        [Tooltip("Reflect the dominant half (more voxels) onto the other, overriding what was there — an exact mirror in geometry and colour, discarding the input's asymmetry. Leave off for the union that keeps features and colour from both sides.")]
        public bool ForceMirror = false;

        // ---- Search score weights (advanced) ----------------------------------

        [Range(0f, 4f)]
        [Tooltip("Rewards placements with fewer exposed faces per voxel — favours chunky, axis-aligned blocks over stair-stepped diagonals. Default 1.")]
        public float FaceWeight = 1f;

        [Range(0f, 4f)]
        [Tooltip("Rewards overlap between the coarse voxels and the fine occupancy — keeps the blocky model faithful to the source silhouette. Default 1.")]
        public float IouWeight = 1f;

        [Range(0f, 4f)]
        [Tooltip("Penalises covering air-gap cells (the space between a dog's four legs, a mug's handle hole). Weighted 2× by default because merging a gap is the worst failure mode.")]
        public float GapWeight = 2f;

        [Range(0f, 4f)]
        [Tooltip("Rewards placements whose block boundaries land on strong source colour edges. Speculative and costly, so it ships at 0. Raise it only during tuning if the geometry terms aren't enough.")]
        public float ColWeight = 0f;

        // ---- Colour -----------------------------------------------------------

        [Tooltip("At load, flood each UV island's colours outward into the surrounding texture gutter so a nearest-surface sample can't land on Meshy's purple UV-gutter bleed.")]
        public bool UvDilate = true;

        [Range(1, 32)]
        [Tooltip("How many texels of reach to flood island colour into the gutter (one 8-neighbour dilation pass = one texel). 8 is plenty for typical bleed; raise it for wide gutters.")]
        public int UvDilatePasses = UvIslandDilation.DefaultPasses;

        [Tooltip("Colour each surface voxel from the centre plus several jittered samples per exposed face, then take the Oklab medoid. A lone stray texel loses the vote instead of tinting an average. Off = one centre sample per voxel.")]
        public bool MultiSampleColour = true;

        [Range(0f, 2f)]
        [Tooltip("Edge-aware label smoothing after palette assignment: relabels each voxel toward its neighbours' colour, but the penalty melts away where the source colours genuinely disagree. 0 = off. Needs a palette (not Raw mode).")]
        public float PottsStrength = 0.5f;

        [Tooltip("Raw: reprojected colours untouched. PerModelPalette: cluster down to a fixed few colours with Oklab k-means (the Crossy-Road flat look). MasterPalette: snap each to the nearest swatch of a shared palette (the window supplies it). Consolidated: keep Raw's colours but merge near-identical shades into the model's fundamental colours.")]
        public ColourMode ColourMode = ColourMode.PerModelPalette;

        [Range(2, 32)]
        [Tooltip("Target number of colours to cluster the model down to for PerModelPalette. Fewer = flatter, more stylised; empty clusters are dropped, so the actual count may come out lower.")]
        public int PaletteSize = 8;

        [Range(0f, 0.3f)]
        [Tooltip("For Consolidated: how close two shades must be (Oklab distance) to collapse into one fundamental colour. 0 = exact (no merge); ~0.05–0.08 removes texture noise without blurring real regions; too high and distinct colours merge.")]
        public float ConsolidateTolerance = 0.06f;

        [Range(0, 32)]
        [Tooltip("For Consolidated: hard cap on the output colour count — locks the model to a known number. Set it to the source image's palette size to reproduce it. 0 = unlimited (tolerance only).")]
        public int ConsolidateMaxColours = 0;

        [Tooltip("On a thin wall the nearest triangle can be the back face, whose texels carry the interior/AO colour. This discards a sampled colour whose triangle faces away from the outward SDF gradient. Heuristic; off by default.")]
        public bool NormalConsistency = false;

        // ---- Smooth comparison path (does not affect the blocky .vox output) ----

        [Range(0, 30)]
        [Tooltip("λ/μ umbrella smoothing passes over the marching-cubes isosurface. Affects ONLY the smooth comparison mesh — the blocky voxel output is untouched. More passes = smoother but softer.")]
        public int TaubinPasses = 5;

        [Range(0f, 1f)]
        [Tooltip("The shrinking (positive) smoothing step per pass. Larger = more smoothing per pass but more volume loss before μ inflates it back. Affects only the smooth comparison mesh.")]
        public float TaubinLambda = 0.5f;

        [Range(0f, 1f)]
        [Tooltip("The inflating (negative) step that counteracts λ's shrinkage each pass. Should exceed λ so the mesh keeps its volume. Affects only the smooth comparison mesh.")]
        public float TaubinMu = 0.53f;

        [Tooltip("After smoothing, nudge each vertex back onto the SDF iso=0 surface along the gradient — recovers detail the smoothing rounded off. Affects only the smooth comparison mesh, not the blocky output.")]
        public bool SurfaceReproject = false;

        /// <summary>A fresh config seeded from <see cref="Settings.Defaults"/>, so the mirror can never
        /// drift from the core's defaults. The parser overwrites only the fields the AI supplied.</summary>
        public static SettingsConfig Seeded()
        {
            Settings d = Settings.Defaults;
            return new SettingsConfig
            {
                ResolutionInput = d.ResolutionInput,
                MaxDimVoxels = d.MaxDimVoxels,
                VoxelWorldSize = d.VoxelWorldSize,
                TargetWorldSize = d.TargetWorldSize,
                GridSearch = d.GridSearch,
                ScaleFlex = d.ScaleFlex,
                ThinFeatureKeep = d.ThinFeatureKeep,
                FineFactor = d.FineFactor,
                Coverage = d.Coverage,
                RemoveFloaters = d.RemoveFloaters,
                CleanupStrength = d.CleanupStrength,
                FillCorners = d.FillCorners,
                CornerFillColourTolerance = d.CornerFillColourTolerance,
                CornerFillNeighbourThreshold = d.CornerFillNeighbourThreshold,
                CornerFillRequireMajority = d.CornerFillRequireMajority,
                Symmetry = d.Symmetry,
                ForceMirror = d.ForceMirror,
                FaceWeight = d.FaceWeight,
                IouWeight = d.IouWeight,
                GapWeight = d.GapWeight,
                ColWeight = d.ColWeight,
                UvDilate = d.UvDilate,
                UvDilatePasses = d.UvDilatePasses,
                MultiSampleColour = d.MultiSampleColour,
                PottsStrength = d.PottsStrength,
                ColourMode = d.ColourMode,
                PaletteSize = d.PaletteSize,
                ConsolidateTolerance = d.ConsolidateTolerance,
                ConsolidateMaxColours = d.ConsolidateMaxColours,
                NormalConsistency = d.NormalConsistency,
                TaubinPasses = d.TaubinPasses,
                TaubinLambda = d.TaubinLambda,
                TaubinMu = d.TaubinMu,
                SurfaceReproject = d.SurfaceReproject,
            };
        }

        /// <summary>The reverse of <see cref="ToSettings"/>: a config mirroring an existing struct, for
        /// serialising a resolved <see cref="Settings"/> back to JSON (the struct itself can't be).</summary>
        public static SettingsConfig From(Settings s) => new()
        {
            ResolutionInput = s.ResolutionInput,
            MaxDimVoxels = s.MaxDimVoxels,
            VoxelWorldSize = s.VoxelWorldSize,
            TargetWorldSize = s.TargetWorldSize,
            GridSearch = s.GridSearch,
            ScaleFlex = s.ScaleFlex,
            ThinFeatureKeep = s.ThinFeatureKeep,
            FineFactor = s.FineFactor,
            Coverage = s.Coverage,
            RemoveFloaters = s.RemoveFloaters,
            CleanupStrength = s.CleanupStrength,
            FillCorners = s.FillCorners,
            CornerFillColourTolerance = s.CornerFillColourTolerance,
            CornerFillNeighbourThreshold = s.CornerFillNeighbourThreshold,
            CornerFillRequireMajority = s.CornerFillRequireMajority,
            Symmetry = s.Symmetry,
            ForceMirror = s.ForceMirror,
            FaceWeight = s.FaceWeight,
            IouWeight = s.IouWeight,
            GapWeight = s.GapWeight,
            ColWeight = s.ColWeight,
            UvDilate = s.UvDilate,
            UvDilatePasses = s.UvDilatePasses,
            MultiSampleColour = s.MultiSampleColour,
            PottsStrength = s.PottsStrength,
            ColourMode = s.ColourMode,
            PaletteSize = s.PaletteSize,
            ConsolidateTolerance = s.ConsolidateTolerance,
            ConsolidateMaxColours = s.ConsolidateMaxColours,
            NormalConsistency = s.NormalConsistency,
            TaubinPasses = s.TaubinPasses,
            TaubinLambda = s.TaubinLambda,
            TaubinMu = s.TaubinMu,
            SurfaceReproject = s.SurfaceReproject,
        };

        /// <summary>Builds the engine-free <see cref="Settings"/> struct the pipeline runs. The master
        /// palette is left null (the window supplies it); the resolution follows whichever
        /// <see cref="ResolutionInput"/> mode the AI chose.</summary>
        public Settings ToSettings() => Settings.Defaults with
        {
            ResolutionInput = this.ResolutionInput,
            MaxDimVoxels = MaxDimVoxels,
            VoxelWorldSize = VoxelWorldSize,
            TargetWorldSize = TargetWorldSize,
            GridSearch = GridSearch,
            ScaleFlex = ScaleFlex,
            ThinFeatureKeep = ThinFeatureKeep,
            FineFactor = FineFactor,
            Coverage = Coverage,
            RemoveFloaters = RemoveFloaters,
            CleanupStrength = CleanupStrength,
            FillCorners = FillCorners,
            CornerFillColourTolerance = CornerFillColourTolerance,
            CornerFillNeighbourThreshold = CornerFillNeighbourThreshold,
            CornerFillRequireMajority = CornerFillRequireMajority,
            Symmetry = Symmetry,
            ForceMirror = ForceMirror,
            FaceWeight = FaceWeight,
            IouWeight = IouWeight,
            GapWeight = GapWeight,
            ColWeight = ColWeight,
            UvDilate = UvDilate,
            UvDilatePasses = UvDilatePasses,
            MultiSampleColour = MultiSampleColour,
            PottsStrength = PottsStrength,
            ColourMode = ColourMode,
            PaletteSize = PaletteSize,
            ConsolidateTolerance = ConsolidateTolerance,
            ConsolidateMaxColours = ConsolidateMaxColours,
            NormalConsistency = NormalConsistency,
            TaubinPasses = TaubinPasses,
            TaubinLambda = TaubinLambda,
            TaubinMu = TaubinMu,
            SurfaceReproject = SurfaceReproject,
            MasterPalette = null,
        };
    }
}
