using System;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels
{
    /// <summary>
    /// Serializable bundle of which steps are on and with what params — the data a preset
    /// produces and a per-asset override edits. The master-palette <i>reference</i> deliberately
    /// lives outside this (on the window): it is shared across all assets for cross-asset
    /// cohesion, while these settings vary per category/asset.
    /// </summary>
    [Serializable]
    public sealed class VoxPipelineSettings
    {
        [Tooltip("Delete small disconnected components (voxelization specks). Substantial detached parts are kept.")]
        public bool removeFloaters = true;

        [Range(0f, 10f)]
        [Tooltip("A component covering less than this % of voxels (and < 2 voxels) is removed.")]
        public float floaterMinPercent = 0.5f;

        [Tooltip("Force bilateral symmetry: mirror one half about a plane onto the other. Off by default — erases intentional asymmetry.")]
        public bool mirror = false;

        [Tooltip("Axis the mirror plane is perpendicular to. Left/right (X) is the usual bilateral plane.")]
        public SymmetryAxis mirrorAxis = SymmetryAxis.X;

        [Range(0f, 1f)]
        [Tooltip("Min mirror-overlap score to auto-apply. Below this the model is treated as not symmetric and left as-is.")]
        public float mirrorConfidence = 0.85f;

        [Tooltip("Apply the mirror at the best-scoring plane even when the confidence gate fails (for a stubborn asset).")]
        public bool mirrorForce = false;

        [Tooltip("Force rotational symmetry: revolve the radial profile into a true solid of revolution. Off by default — for standalone wheels/cylinders only.")]
        public bool revolve = false;

        [Tooltip("Spin axis the profile is revolved about. Up (Y) is the usual wheel axle.")]
        public SymmetryAxis revolveAxis = SymmetryAxis.Y;

        [Range(0f, 1f)]
        [Tooltip("A ring is filled when at least this fraction of its cells were occupied.")]
        public float revolveFillThreshold = 0.5f;

        [Tooltip("Flatten baked shading: grow material regions of similar colour and collapse each to one flat colour.")]
        public bool deLight = true;

        [Range(0f, 0.5f)]
        [Tooltip("Max perceptual (Oklab) distance between adjacent voxels to join one region. Higher = larger, flatter regions.")]
        public float deLightThreshold = 0.10f;

        [Tooltip("Reduce to the model's own dominant colours first: snap every voxel to a variety-selected set of peaks in its colour histogram (Oklab), spread out perceptually rather than just the most common.")]
        public bool snapToHistogramPeaks = false;

        [Range(0f, 0.5f)]
        [Tooltip("Variety threshold (Oklab): keep adding peaks while each new one is at least this distinct from the colours already kept; stop when the next-best is closer. Higher = fewer, more distinct colours. This is the primary control; the peak count is just a cap.")]
        public float histogramPeakVariety = 0.10f;

        [Range(1, 64)]
        [Tooltip("Safety cap on how many histogram peaks (distinct dominant colours) to keep. Selection usually stops earlier, once no remaining colour clears the variety threshold.")]
        public int histogramPeakCount = 8;

        [Tooltip("Snap each colour to the nearest swatch in the shared master palette (Oklab) for cross-asset cohesion.")]
        public bool snapToPalette = true;

        [Tooltip("Mild geometric despeckle/fill. Off for organic models — erodes thin features (legs, antennae).")]
        public bool morphology = false;

        public VoxPipelineSettings Clone() => (VoxPipelineSettings)MemberwiseClone();
    }
}
