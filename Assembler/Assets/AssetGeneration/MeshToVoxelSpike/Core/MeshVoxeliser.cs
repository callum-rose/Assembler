using System;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// The engine-free core of the mesh → voxel spike: fine SDF → fine-grid analysis → scored
    /// grid-placement search (or the identity placement) → floater removal → protected morphology →
    /// multi-sample colour reprojection → palette + Potts smoothing → corner-fill/symmetry finishing
    /// — producing the blocky occupancy grid + flat per-voxel colours. Pure: it takes a pre-loaded
    /// <see cref="LoadedModel"/> and a <see cref="SpikeSettings"/> and returns a
    /// <see cref="VoxelResult"/>, with no file IO and no <c>UnityEngine</c> dependency, so it can run
    /// headlessly (batch mode) or outside Unity entirely. Loading the mesh (.obj/.fbx, texture
    /// decode) and building renderable <c>UnityEngine.Mesh</c> previews are the editor layer's job.
    ///
    /// <see cref="Voxelise"/> returns just the voxel data. <see cref="VoxeliseWithPreview"/> also
    /// returns the smooth-comparison intermediates (as portable <see cref="g3.DMesh3"/> meshes) for
    /// the spike window's A/B preview.
    /// </summary>
    public static class MeshVoxeliser
    {
        /// <summary>Strong-edge threshold (Oklab) for the S_col plumbing; only used when ColWeight &gt; 0.</summary>
        private const float ColourEdgeThreshold = 0.1f;

        /// <summary>
        /// Voxelise <paramref name="model"/> to the lean occupancy + colours + metrics.
        /// <paramref name="progress"/> receives <c>(fraction, stageName)</c> at stage boundaries.
        /// </summary>
        public static VoxelResult Voxelise(LoadedModel model, SpikeSettings settings, Action<float, string>? progress = null)
            => RunVoxelStage(model, settings, progress).Result;

        /// <summary>
        /// Voxelise and additionally build the smooth-comparison intermediates (Taubin-smoothed
        /// isosurface, optional SDF reprojection, per-vertex reprojected colours) as portable g3
        /// meshes for the previewer. Does the extra smooth-path work the lean <see cref="Voxelise"/>
        /// skips.
        /// </summary>
        public static VoxelPreview VoxeliseWithPreview(LoadedModel model, SpikeSettings settings, Action<float, string>? progress = null)
        {
            Stage stage = RunVoxelStage(model, settings, progress);

            // --- Comparison: smooth SDF remesh ---
            progress?.Invoke(0.7f, "Taubin smoothing");
            g3.DMesh3 smooth = TaubinSmoother.Apply(stage.Sdf.Iso, settings.TaubinPasses, settings.TaubinLambda, settings.TaubinMu);

            g3.DMesh3 finalSmooth = smooth;
            g3.DMesh3? reprojected = null;
            if (settings.SurfaceReproject)
            {
                progress?.Invoke(0.78f, "SDF surface reprojection");
                finalSmooth = new g3.DMesh3(smooth);
                SurfaceReprojection.Apply(finalSmooth, stage.Sdf.Field);
                reprojected = finalSmooth;
            }

            progress?.Invoke(0.85f, "Reprojecting vertex colours");
            Rgba32[] vertexColours = ColourReprojector.SampleVertices(
                finalSmooth, stage.Model, stage.Tree, settings.NormalConsistency, stage.Sdf.Field);
            vertexColours = ColourModes.Apply(
                vertexColours, VertexMask(finalSmooth), settings.ColourMode, settings.ColourOptions);

            return new VoxelPreview
            {
                Voxel = stage.Result,
                Model = stage.Model,
                Iso = stage.Sdf.Iso,
                Smoothed = smooth,
                Reprojected = reprojected,
                SmoothColoured = finalSmooth,
                SmoothVertexColours = vertexColours,
            };
        }

        // The shared voxel pipeline (everything up to the finished blocky grid), plus the SDF/tree/
        // model the preview's smooth path reuses so it never recomputes them.
        private readonly struct Stage
        {
            public VoxelResult Result { get; init; }
            public LoadedModel Model { get; init; }
            public g3.DMeshAABBTree3 Tree { get; init; }
            public SdfIsosurface.Result Sdf { get; init; }
        }

        private static Stage RunVoxelStage(LoadedModel model, SpikeSettings settings, Action<float, string>? progress)
        {
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
            var fineDims = new g3.Vector3i(fine.NX, fine.NY, fine.NZ);

            progress?.Invoke(0.35f, "Analysing fine grid");
            var analysis = FineGridAnalysis.Build(fine, factor);

            GridPlacementSearch.Options searchOptions = settings.SearchOptions;
            System.Collections.Generic.List<GridPlacementSearch.ColourEdge>? colourEdges = null;
            if (settings.GridSearch && searchOptions.ColWeight > 0f)
            {
                // S_col plumbing: per-fine-voxel colours are expensive, so only when the weight is live.
                Rgba32[] fineColours = ColourReprojector.SampleVoxels(
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

            // --- Crossy-Road blocky voxel colours ---
            progress?.Invoke(0.55f, "Reprojecting voxel colours");
            Rgba32[] sampledColours = settings.MultiSampleColour
                ? MultiSampleColour.Sample(occupancy, model, tree, settings.NormalConsistency, sdf.Field)
                : ColourReprojector.SampleVoxels(occupancy, model, tree, settings.NormalConsistency, sdf.Field);

            ColourModes.PaletteAssignment assignment = ColourModes.AssignPalette(
                sampledColours, occupancy.Occupied, settings.ColourMode, settings.ColourOptions);
            Rgba32[] voxelColours = assignment.Colours;

            if (settings.PottsStrength > 0f && assignment.Palette is { Length: > 1 } && assignment.Labels is not null)
            {
                progress?.Invoke(0.62f, "Potts label smoothing");
                int[] labels = PottsLabelSmoother.Smooth(
                    occupancy, sampledColours, assignment.Labels, assignment.Palette, settings.PottsStrength);
                voxelColours = (Rgba32[])voxelColours.Clone();
                for (int i = 0; i < labels.Length; i++)
                {
                    if (labels[i] >= 0)
                    {
                        voxelColours[i] = assignment.Palette[labels[i]];
                    }
                }
            }

            // Boxiness / symmetry finishing passes, on the final coloured grid. Corner-fill first
            // (fills concave notches with their neighbours' modal colour), then symmetry has the
            // last word so the silhouette is guaranteed mirror-symmetric on the chosen axes.
            if (settings.FillCorners)
            {
                progress?.Invoke(0.66f, "Filling corners");
                CornerFill.Apply(
                    occupancy, voxelColours, placement.GapFraction,
                    settings.FillCornersRecursive, settings.CornerFillColourTolerance);
            }
            if (settings.Symmetry != SymmetryAxes.None)
            {
                progress?.Invoke(0.68f, "Forcing symmetry");
                SymmetryEnforcer.Apply(occupancy, voxelColours, settings.Symmetry, settings.ForceMirror);
            }

            var result = new VoxelResult
            {
                Occupancy = occupancy,
                VoxelColours = voxelColours,
                GridX = occupancy.NX,
                GridY = occupancy.NY,
                GridZ = occupancy.NZ,
                VoxelCount = occupancy.OccupiedCount,
                Metrics = SpikeMetrics.Compute(occupancy, voxelColours, placement, floatersRemoved, fineDims),
            };

            return new Stage { Result = result, Model = model, Tree = tree, Sdf = sdf };
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
