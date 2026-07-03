using System;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Editor-side orchestration for the spike window: load a mesh from disk, run the engine-free
    /// <see cref="MeshVoxeliser"/> core, and build the renderable <see cref="UnityEngine.Mesh"/>
    /// previews the window/test-runner display. The voxelisation itself is now portable and lives in
    /// <see cref="MeshVoxeliser"/> (assembly <c>Assembler.AssetGeneration.MeshToVoxel</c>) so it can
    /// run headlessly; this wrapper only adds the editor-only bookends — file loading
    /// (<see cref="ModelLoader"/>) and g3 → Unity mesh building (<see cref="G3MeshConversion"/> /
    /// <see cref="BlockyVoxelMesher"/>). Must run on the main thread because it builds <c>Mesh</c>
    /// objects and reads textures.
    /// </summary>
    public static class SpikePipeline
    {
        /// <summary>
        /// Load <paramref name="meshPath"/> (.obj/.fbx), voxelise it with <paramref name="settings"/>,
        /// and build the full preview bundle. <paramref name="progress"/> receives
        /// <c>(fraction, stageName)</c> at stage boundaries.
        /// </summary>
        public static SpikeStageResult Run(string meshPath, SpikeSettings settings, Action<float, string>? progress = null)
        {
            progress?.Invoke(0.02f, "Importing mesh");
            LoadedModel model = ModelLoader.Load(meshPath);

            VoxelPreview preview = MeshVoxeliser.VoxeliseWithPreview(model, settings, progress);
            VoxelResult voxel = preview.Voxel;

            progress?.Invoke(0.95f, "Building preview meshes");
            return new SpikeStageResult
            {
                Original = G3MeshConversion.OriginalToUnity(preview.Model),
                Iso = G3MeshConversion.ToUnity(preview.Iso, null),
                Smoothed = G3MeshConversion.ToUnity(preview.Smoothed, null),
                Reprojected = preview.Reprojected != null ? G3MeshConversion.ToUnity(preview.Reprojected, null) : null,
                SmoothColoured = G3MeshConversion.ToUnity(preview.SmoothColoured, preview.SmoothVertexColours),
                Blocky = BlockyVoxelMesher.Build(voxel.Occupancy, voxel.VoxelColours),
                VoxelCount = voxel.VoxelCount,
                GridX = voxel.GridX,
                GridY = voxel.GridY,
                GridZ = voxel.GridZ,
                Occupancy = voxel.Occupancy,
                VoxelColours = voxel.VoxelColours,
                Metrics = voxel.Metrics,
            };
        }
    }
}
