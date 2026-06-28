using System;
using UnityEngine;

namespace VoxelsFromMeshSpike
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

        [Tooltip("Flatten baked shading: grow material regions of similar colour and collapse each to one flat colour.")]
        public bool deLight = true;

        [Range(0f, 0.5f)]
        [Tooltip("Max perceptual (Oklab) distance between adjacent voxels to join one region. Higher = larger, flatter regions.")]
        public float deLightThreshold = 0.10f;

        [Tooltip("Snap each colour to the nearest swatch in the shared master palette (Oklab) for cross-asset cohesion.")]
        public bool snapToPalette = true;

        [Tooltip("Mild geometric despeckle/fill. Off for organic models — erodes thin features (legs, antennae).")]
        public bool morphology = false;

        public VoxPipelineSettings Clone() => (VoxPipelineSettings)MemberwiseClone();
    }
}
