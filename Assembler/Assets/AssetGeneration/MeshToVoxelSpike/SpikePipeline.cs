using System;
using Assembler.AssetGeneration.MeshToVoxels;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// UI-free orchestration of the spike: load (+ UV island dilation) → fine SDF → fine-grid
    /// analysis → scored grid-placement search (or the identity placement) → floater removal →
    /// protected morphology → multi-sample colour reprojection → palette + Potts smoothing → the
    /// blocky mesh, plus the smooth-comparison path and a metrics readout. Returns a
    /// <see cref="SpikeStageResult"/> bundle holding every intermediate for the previewer. Runs
    /// synchronously — fine at the coarse voxel budgets the stylised look targets — with a
    /// coarse-grained progress callback, and must run on the main thread because it builds
    /// <see cref="Mesh"/> objects.
    /// </summary>
    public static class SpikePipeline
    {
        /// <summary>Strong-edge threshold (Oklab) for the S_col plumbing; only used when ColWeight &gt; 0.</summary>
        private const float ColourEdgeThreshold = 0.1f;

        /// <summary>
        /// Run the full pipeline for <paramref name="meshPath"/> (.obj/.fbx). <paramref name="progress"/>
        /// receives <c>(fraction, stageName)</c> at stage boundaries.
        /// </summary>
        public static SpikeStageResult Run(string meshPath, SpikeSettings settings, Action<float, string>? progress = null)
        {
            progress?.Invoke(0.02f, "Importing mesh");
            ObjToVoxConverter.LoadedModel model = ObjToVoxConverter.LoadScene(meshPath);

            if (settings.UvDilate)
            {
                progress?.Invoke(0.06f, "Dilating UV islands");
                model = UvIslandDilation.Apply(model, settings.UvDilatePasses);
            }

            var tree = new g3.DMeshAABBTree3(model.Mesh);
            tree.Build();

            int maxDim = settings.ResolveMaxDimVoxels();
            int factor = settings.ResolveFineFactor();

            progress?.Invoke(0.15f, "Signed distance field + marching cubes");
            SdfIsosurface.Result sdf = SdfIsosurface.Build(model.Mesh, tree, maxDim * factor);
            VoxelGrid fine = sdf.Occupancy;
            var fineDims = new Vector3Int(fine.NX, fine.NY, fine.NZ);

            progress?.Invoke(0.35f, "Analysing fine grid");
            var analysis = FineGridAnalysis.Build(fine, factor);

            GridPlacementSearch.Options searchOptions = settings.SearchOptions;
            System.Collections.Generic.List<GridPlacementSearch.ColourEdge>? colourEdges = null;
            if (settings.GridSearch && searchOptions.ColWeight > 0f)
            {
                // S_col plumbing: per-fine-voxel colours are expensive, so only when the weight is live.
                Color32[] fineColours = ColourReprojector.SampleVoxels(
                    fine, model, tree, settings.NormalConsistency, sdf.Field);
                colourEdges = GridPlacementSearch.ExtractStrongColourEdges(fine, fineColours, ColourEdgeThreshold);
            }

            progress?.Invoke(0.4f, settings.GridSearch ? "Searching grid placements" : "Voting occupancy");
            GridPlacementSearch.Placement placement = settings.GridSearch
                ? GridPlacementSearch.Run(analysis, searchOptions, colourEdges)
                : GridPlacementSearch.Materialise(analysis, GridPlacementSearch.IdentityCandidate(analysis), searchOptions);

            VoxelGrid occupancy = placement.Grid;

            progress?.Invoke(0.5f, "Cleaning occupancy");
            int floatersRemoved = settings.RemoveFloaters
                ? OccupancyCleanup.RemoveFloaters(occupancy, placement.TouchesMain)
                : 0;
            if (settings.CleanupStrength > 0)
            {
                bool[] protectedMask = OccupancyCleanup.BuildProtectedMask(
                    placement.ThinKept, occupancy.NX, occupancy.NY, occupancy.NZ);
                OccupancyCleanup.CloseOpen(occupancy, protectedMask, placement.GapFraction, settings.CleanupStrength);
            }

            // --- Primary: Crossy-Road blocky voxel model ---
            progress?.Invoke(0.55f, "Reprojecting voxel colours");
            Color32[] sampledColours = settings.MultiSampleColour
                ? MultiSampleColour.Sample(occupancy, model, tree, settings.NormalConsistency, sdf.Field)
                : ColourReprojector.SampleVoxels(occupancy, model, tree, settings.NormalConsistency, sdf.Field);

            ColourModes.PaletteAssignment assignment = ColourModes.AssignPalette(
                sampledColours, occupancy.Occupied, settings.ColourMode, settings.ColourOptions);
            Color32[] voxelColours = assignment.Colours;

            if (settings.PottsStrength > 0f && assignment.Palette is { Length: > 1 } && assignment.Labels is not null)
            {
                progress?.Invoke(0.62f, "Potts label smoothing");
                int[] labels = PottsLabelSmoother.Smooth(
                    occupancy, sampledColours, assignment.Labels, assignment.Palette, settings.PottsStrength);
                voxelColours = (Color32[])voxelColours.Clone();
                for (int i = 0; i < labels.Length; i++)
                {
                    if (labels[i] >= 0)
                    {
                        voxelColours[i] = assignment.Palette[labels[i]];
                    }
                }
            }

            Mesh blocky = BlockyVoxelMesher.Build(occupancy, voxelColours);

            // --- Comparison: smooth SDF remesh ---
            progress?.Invoke(0.7f, "Taubin smoothing");
            g3.DMesh3 smooth = TaubinSmoother.Apply(sdf.Iso, settings.TaubinPasses, settings.TaubinLambda, settings.TaubinMu);

            g3.DMesh3 finalSmooth = smooth;
            Mesh? reprojectedPreview = null;
            if (settings.SurfaceReproject)
            {
                progress?.Invoke(0.78f, "SDF surface reprojection");
                finalSmooth = new g3.DMesh3(smooth);
                SurfaceReprojection.Apply(finalSmooth, sdf.Field);
                reprojectedPreview = G3MeshConversion.ToUnity(finalSmooth, null);
            }

            progress?.Invoke(0.85f, "Reprojecting vertex colours");
            Color32[] vertexColours = ColourReprojector.SampleVertices(
                finalSmooth, model, tree, settings.NormalConsistency, sdf.Field);
            vertexColours = ColourModes.Apply(
                vertexColours, VertexMask(finalSmooth), settings.ColourMode, settings.ColourOptions);

            progress?.Invoke(0.95f, "Building preview meshes");
            return new SpikeStageResult
            {
                Original = G3MeshConversion.OriginalToUnity(model),
                Iso = G3MeshConversion.ToUnity(sdf.Iso, null),
                Smoothed = G3MeshConversion.ToUnity(smooth, null),
                Reprojected = reprojectedPreview,
                SmoothColoured = G3MeshConversion.ToUnity(finalSmooth, vertexColours),
                Blocky = blocky,
                VoxelCount = occupancy.OccupiedCount,
                GridX = occupancy.NX,
                GridY = occupancy.NY,
                GridZ = occupancy.NZ,
                Occupancy = occupancy,
                VoxelColours = voxelColours,
                Metrics = SpikeMetrics.Compute(occupancy, voxelColours, placement, floatersRemoved, fineDims),
            };
        }

        // Validity mask over g3 vertex ids so ColourModes never clusters colours for dead/holed ids.
        private static bool[] VertexMask(g3.DMesh3 mesh)
        {
            var mask = new bool[mesh.MaxVertexID];
            foreach (int vid in mesh.VertexIndices())
            {
                mask[vid] = true;
            }
            return mask;
        }
    }
}
