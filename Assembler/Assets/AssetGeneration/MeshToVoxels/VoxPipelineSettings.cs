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
		[Tooltip("For voxel-style meshes (a baked voxel grid, e.g. from a voxel-art reference): voxelize a lossless master at the mesh's own native pitch, then resample to the target IN VOXEL SPACE — so any target resolution (incl. non-power-of-2) stays crisp instead of resampling the quantized mesh at an unaligned ratio (which muds). Auto-detects the pitch; falls back to direct/supersample when the mesh isn't grid-like (a smooth/organic mesh). Reuses the downres coverage/feature/colour levers below.")]
		public bool nativePitchMaster = false;

		[Range(0f, 1f)]
		[Tooltip("Min lattice-fit confidence to take the native-pitch master path. Below this the mesh is treated as non-voxel and the direct/supersample path runs. Higher = stricter (fewer false positives on smooth meshes).")]
		public float nativePitchConfidence = 0.8f;

		[Tooltip("Voxelize at a higher resolution then downres to the target, preserving sub-voxel detail (thin features, small colour details) that direct low-res voxelization aliases away. Costs factor³ more voxelization work.")]
		public bool supersample = false;

		[Range(2, 4)]
		[Tooltip("Voxelize at this multiple of the target dimension before downres. Each output voxel aggregates a factor³ block. Higher preserves more but is much slower (factor³ work).")]
		public int supersampleFactor = 2;

		[Range(0f, 1f)]
		[Tooltip("Downres occupancy: fill an output voxel when this fraction of its high-res block was occupied. Lower = fatter/more inclusive; higher = leaner.")]
		public float downresCoverageThreshold = 0.5f;

		[Tooltip("Force-keep features thinner than one output voxel (antennae, fins) that the coverage vote would otherwise erase. Off = plain coverage majority.")]
		public bool downresFeatureAware = true;

		[Range(0f, 5f)]
		[Tooltip("When collapsing a block to one colour, boost perceptually distinct minority colours so small details (an eye, a stripe) aren't outvoted into mush. 0 = pure majority vote.")]
		public float downresColourSalience = 1.0f;

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

		[Tooltip("Texture-space palette snap (C2): snap the source texture to the master palette in 2D BEFORE voxelizing, so colour boundaries come out straight and on-palette instead of ragged. Runs pre-voxelization on the master palette and only affects textured meshes; complements the per-voxel snapToPalette below, which stays on as a cheap backstop.")]
		public bool textureSpacePaletteSnap = false;

		[Tooltip("Before the texture-space snap, run an edge-preserving (Oklab bilateral) smooth that flattens within-region shading while keeping colour edges, so soft gradients don't snap to ragged boundaries. Off = snap the raw texels.")]
		public bool textureSmooth = true;

		[Range(1, 4)]
		[Tooltip("Bilateral smoothing radius in texels. Larger flattens broader shading but costs (2r+1)² taps per texel.")]
		public int textureSmoothRadius = 2;

		[Range(0.01f, 0.5f)]
		[Tooltip("Edge threshold (Oklab) for the smoothing: neighbours more perceptually distant than ~this are treated as across an edge and not blended in. Lower = preserves finer edges; higher = flattens more.")]
		public float textureSmoothRange = 0.10f;

		[Tooltip("Snap each colour to the nearest swatch in the shared master palette (Oklab) for cross-asset cohesion.")]
		public bool snapToPalette = true;

		[Tooltip("Mild geometric despeckle/fill. Off for organic models — erodes thin features (legs, antennae).")]
		public bool morphology = false;

		public VoxPipelineSettings Clone() => (VoxPipelineSettings)MemberwiseClone();
	}
}
