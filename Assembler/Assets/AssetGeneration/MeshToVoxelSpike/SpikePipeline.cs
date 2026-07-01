using System;
using Assembler.AssetGeneration.MeshToVoxels;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// UI-free orchestration of the spike: load → SDF/marching-cubes (+ optional feature-aware
    /// downres) → Taubin → optional SDF reprojection → colour reproject → colour mode → build the
    /// smooth-comparison and blocky Unity meshes. Returns a <see cref="SpikeStageResult"/> bundle
    /// holding every intermediate for the previewer. Runs synchronously — fine at the coarse voxel
    /// budgets the stylised look targets — with a coarse-grained progress callback, and must run on
    /// the main thread because it builds <see cref="Mesh"/> objects.
    /// </summary>
    public static class SpikePipeline
    {
        /// <summary>
        /// Run the full pipeline for <paramref name="meshPath"/> (.obj/.fbx). <paramref name="progress"/>
        /// receives <c>(fraction, stageName)</c> at stage boundaries.
        /// </summary>
        public static SpikeStageResult Run(string meshPath, SpikeSettings settings, Action<float, string>? progress = null)
        {
            progress?.Invoke(0.02f, "Importing mesh");
            ObjToVoxConverter.LoadedModel model = ObjToVoxConverter.LoadScene(meshPath);

            var tree = new g3.DMeshAABBTree3(model.Mesh);
            tree.Build();

            int factor = settings.FeatureAware ? Mathf.Max(1, settings.FeatureFactor) : 1;
            int sdfDim = settings.MaxDimVoxels * factor;

            progress?.Invoke(0.15f, "Signed distance field + marching cubes");
            SdfIsosurface.Result sdf = SdfIsosurface.Build(model.Mesh, sdfDim);

            VoxelGrid occupancy = settings.FeatureAware
                ? FeatureAwareDownsample.Apply(sdf.Occupancy, factor, settings.FeatureCoverage, forceThinFeatures: true)
                : sdf.Occupancy;

            // --- Primary: Crossy-Road blocky voxel model ---
            progress?.Invoke(0.45f, "Reprojecting voxel colours");
            Color32[] voxelColours = ColourReprojector.SampleVoxels(
                occupancy, model, tree, settings.NormalConsistency, sdf.Field);
            voxelColours = ColourModes.Apply(voxelColours, occupancy.Occupied, settings.ColourMode, settings.ColourOptions);
            Mesh blocky = BlockyVoxelMesher.Build(occupancy, voxelColours);

            // --- Comparison: smooth SDF remesh ---
            progress?.Invoke(0.65f, "Taubin smoothing");
            g3.DMesh3 smooth = TaubinSmoother.Apply(sdf.Iso, settings.TaubinPasses, settings.TaubinLambda, settings.TaubinMu);

            g3.DMesh3 finalSmooth = smooth;
            Mesh? reprojectedPreview = null;
            if (settings.SurfaceReproject)
            {
                progress?.Invoke(0.75f, "SDF surface reprojection");
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
