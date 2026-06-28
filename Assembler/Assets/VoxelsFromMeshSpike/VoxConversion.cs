using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VoxelsFromMeshSpike
{
    /// <summary>
    /// UI-free core of the mesh → VOX spike: solid-fill a mesh into a coloured <see cref="VoxResult"/>,
    /// run the post-processing <see cref="VoxPipeline"/> over it, then write the <c>.vox</c>. Shared by
    /// the editor window (<see cref="VoxelsFromMeshSpikeWindow"/>) and the headless batch entry point
    /// (<see cref="VoxelsFromMeshSpikeBatch"/>) so both drive an identical pipeline. No dialogs, progress
    /// bars, or window state here — callers supply optional progress sinks.
    /// </summary>
    public static class VoxConversion
    {
        /// <summary>Result of a completed conversion: grid dims + voxel/colour counts + where it was written.</summary>
        public readonly struct Summary
        {
            public int VoxelCount { get; init; }
            public int GridX { get; init; }
            public int GridY { get; init; }
            public int GridZ { get; init; }
            public int ColorCount { get; init; }
            public string OutputPath { get; init; }

            public override string ToString() =>
                $"{VoxelCount:N0} voxels ({GridX}×{GridY}×{GridZ}), {ColorCount:N0} colour(s) → {OutputPath}";
        }

        /// <summary>
        /// Voxelize <paramref name="meshPath"/> (.obj/.fbx) at <paramref name="maxDimVoxels"/>, run the
        /// pipeline built from <paramref name="settings"/> + <paramref name="palette"/>, and write the
        /// result to <paramref name="voxPath"/>.
        /// </summary>
        /// <param name="voxelProgress">Optional sink for the slow per-voxel pass; return false to cancel.</param>
        /// <param name="pipelineProgress">Optional <c>(stepName, fraction)</c> sink for post-processing.</param>
        /// <exception cref="FileNotFoundException">The mesh does not exist.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="voxelProgress"/> requested cancellation.</exception>
        public static Summary Run(
            string meshPath,
            string voxPath,
            int maxDimVoxels,
            VoxPipelineSettings settings,
            IReadOnlyList<Color32> palette,
            IProgressReporter? voxelProgress = null,
            Action<string, float>? pipelineProgress = null)
        {
            if (!File.Exists(meshPath))
            {
                throw new FileNotFoundException($"Mesh not found: {meshPath}", meshPath);
            }

            VoxResult result = ObjToVoxConverter.Convert(
                meshPath, maxDimVoxels, voxelProgress ?? NullProgressReporter.Instance);

            // Post-processing runs over the dense working model in canonical order (floaters →
            // de-light → histogram-peak snap → palette-snap → morphology), built from the supplied
            // preset + overrides.
            VoxModel model = VoxModel.FromResult(result);
            VoxPipeline pipeline = VoxPipeline.FromSettings(settings, palette);
            pipeline.Run(model, pipelineProgress);
            result = model.ToResult();

            int colorCount = CountDistinctColors(result);

            string? dir = Path.GetDirectoryName(voxPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            VoxWriter.Write(voxPath, result);

            // Surface a freshly-written .vox to the project view when it lands inside Assets/.
            if (voxPath.Replace('\\', '/').Contains(Application.dataPath.Replace('\\', '/')))
            {
                AssetDatabase.Refresh();
            }

            return new Summary
            {
                VoxelCount = result.Cells.Count,
                GridX = result.GridX,
                GridY = result.GridY,
                GridZ = result.GridZ,
                ColorCount = colorCount,
                OutputPath = voxPath,
            };
        }

        private static int CountDistinctColors(VoxResult result)
        {
            var seen = new HashSet<int>();
            foreach (VoxCell cell in result.Cells)
            {
                seen.Add((cell.Color.r << 16) | (cell.Color.g << 8) | cell.Color.b);
            }
            return seen.Count;
        }

        /// <summary>A progress sink that never cancels and reports nothing — for headless/silent runs.</summary>
        private sealed class NullProgressReporter : IProgressReporter
        {
            public static readonly NullProgressReporter Instance = new();

            public bool Report(float fraction, string message) => true;
        }
    }
}
