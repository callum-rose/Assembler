using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels
{
    /// <summary>
    /// UI-free core of the mesh → VOX conversion: solid-fill a mesh into a coloured <see cref="VoxResult"/>,
    /// run the post-processing <see cref="VoxPipeline"/> over it, then write the <c>.vox</c>. Shared by
    /// the editor window (<see cref="MeshToVoxelsWindow"/>) and the headless batch entry point
    /// (<see cref="MeshToVoxelsBatch"/>) so both drive an identical pipeline. No dialogs, progress
    /// bars, or window state here — callers supply optional progress sinks.
    ///
    /// The heavy voxelization runs on a background thread (<see cref="Run"/>) so the editor stays
    /// responsive: only the Unity-API parts — importing the mesh + snapshotting its textures
    /// (<see cref="ObjToVoxConverter.LoadScene"/>) and the final <c>AssetDatabase.Refresh</c> — stay on
    /// the main thread, with progress marshalled back to it. <see cref="RunSynchronous"/> runs the whole
    /// thing inline for headless/batch use, where there is no UI thread to keep free.
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
        /// result to <paramref name="voxPath"/>. The mesh import runs on the calling (main) thread; the
        /// voxelization, pipeline, and write run on a background thread, so the editor stays responsive.
        /// Must be called from the main thread (it captures the main-thread <see cref="SynchronizationContext"/>).
        /// </summary>
        /// <param name="voxelProgress">Optional sink for the slow per-voxel pass; return false to cancel.</param>
        /// <param name="pipelineProgress">Optional <c>(stepName, fraction)</c> sink for post-processing.</param>
        /// <exception cref="FileNotFoundException">The mesh does not exist.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="voxelProgress"/> requested cancellation.</exception>
        public static async Task<Summary> Run(
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

            // Main thread: import the mesh and snapshot its textures into Unity-free buffers, and
            // capture the context so progress and the closing AssetDatabase touch land back here.
            SynchronizationContext? main = SynchronizationContext.Current;
            ObjToVoxConverter.LoadedModel model = ObjToVoxConverter.LoadScene(meshPath);

            // The editor's progress reporters poke EditorUtility (main-thread only), so marshal the
            // background thread's reports back; cancellation flows back through a token the
            // main-thread callback trips.
            using var cancellation = new CancellationTokenSource();
            IProgressReporter computeProgress = MarshalProgress(voxelProgress, main, cancellation);
            Action<string, float>? computePipelineProgress = MarshalPipelineProgress(pipelineProgress, main);

            Summary summary = await Task.Run(() => ComputeAndWrite(
                model, voxPath, maxDimVoxels, settings, palette, computeProgress, computePipelineProgress));

            // Resumes on the captured (main-thread) context, so the AssetDatabase touch is legal.
            RefreshIfInsideProject(voxPath);
            return summary;
        }

        /// <summary>
        /// Blocking, single-threaded variant of <see cref="Run"/> for headless/batch use, where there
        /// is no interactive editor to keep responsive. Runs the entire pipeline inline on the calling
        /// (main) thread — no background thread, no progress marshalling, no deadlock risk under
        /// <c>-batchmode</c>.
        /// </summary>
        public static Summary RunSynchronous(
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

            ObjToVoxConverter.LoadedModel model = ObjToVoxConverter.LoadScene(meshPath);
            Summary summary = ComputeAndWrite(
                model, voxPath, maxDimVoxels, settings, palette,
                voxelProgress ?? NullProgressReporter.Instance, pipelineProgress);
            RefreshIfInsideProject(voxPath);
            return summary;
        }

        /// <summary>
        /// Pure CPU work — safe to run off the main thread: voxelize the loaded model, run the
        /// post-processing pipeline (floaters → de-light → histogram-peak snap → palette-snap →
        /// morphology) over the dense working model, and write the <c>.vox</c>.
        /// </summary>
        private static Summary ComputeAndWrite(
            ObjToVoxConverter.LoadedModel model,
            string voxPath,
            int maxDimVoxels,
            VoxPipelineSettings settings,
            IReadOnlyList<Color32> palette,
            IProgressReporter voxelProgress,
            Action<string, float>? pipelineProgress)
        {
            // Supersample-and-downres (§ detail preservation): voxelize at factor× the target so the
            // downres sees a coverage fraction (and sub-voxel features) per output cell, instead of a
            // single aliased centre sample. Off (factor 1) → unchanged direct voxelization, so this is
            // a clean A/B toggle. maxDim × factor may exceed the converter's 256 cap; it clamps, which
            // just yields a coarser-than-target result at extreme settings.
            int factor = settings.supersample ? Mathf.Clamp(settings.supersampleFactor, 1, 4) : 1;

            VoxResult result = ObjToVoxConverter.Convert(model, maxDimVoxels * factor, voxelProgress);

            VoxModel voxModel = VoxModel.FromResult(result);
            if (factor > 1)
            {
                pipelineProgress?.Invoke("Downres", 0f);
                voxModel = VoxDownres.Apply(voxModel, new VoxDownres.Options
                {
                    Factor = factor,
                    CoverageThreshold = settings.downresCoverageThreshold,
                    FeatureAware = settings.downresFeatureAware,
                    ColourSalience = settings.downresColourSalience,
                });
            }

            VoxPipeline pipeline = VoxPipeline.FromSettings(settings, palette);
            pipeline.Run(voxModel, pipelineProgress);
            result = voxModel.ToResult();

            int colorCount = CountDistinctColors(result);

            string? dir = Path.GetDirectoryName(voxPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            VoxWriter.Write(voxPath, result);

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

        // Surface a freshly-written .vox to the project view when it lands inside Assets/.
        // Reads Application.dataPath / AssetDatabase, so this must run on the main thread.
        private static void RefreshIfInsideProject(string voxPath)
        {
            if (voxPath.Replace('\\', '/').Contains(Application.dataPath.Replace('\\', '/')))
            {
                AssetDatabase.Refresh();
            }
        }

        private static IProgressReporter MarshalProgress(
            IProgressReporter? inner, SynchronizationContext? main, CancellationTokenSource cancellation)
        {
            if (inner == null)
            {
                return NullProgressReporter.Instance;
            }
            if (main == null)
            {
                return inner;
            }
            return new MainThreadProgressReporter(inner, main, cancellation);
        }

        private static Action<string, float>? MarshalPipelineProgress(
            Action<string, float>? inner, SynchronizationContext? main)
        {
            if (inner == null || main == null)
            {
                return inner;
            }

            Action<string, float> sink = inner;
            SynchronizationContext context = main;
            return (name, fraction) => context.Post(_ => sink(name, fraction), null);
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

        /// <summary>
        /// Forwards background-thread progress to a main-thread-only reporter via the captured
        /// <see cref="SynchronizationContext"/>, and relays its cancellation request back to the
        /// compute through a token. The post is fire-and-forget, so cancellation lags the user's
        /// click by a tick or two — harmless for a progress bar.
        /// </summary>
        private sealed class MainThreadProgressReporter : IProgressReporter
        {
            private readonly IProgressReporter _inner;
            private readonly SynchronizationContext _main;
            private readonly CancellationTokenSource _cancellation;

            public MainThreadProgressReporter(
                IProgressReporter inner, SynchronizationContext main, CancellationTokenSource cancellation)
            {
                _inner = inner;
                _main = main;
                _cancellation = cancellation;
            }

            public bool Report(float fraction, string message)
            {
                _main.Post(_ =>
                {
                    if (!_inner.Report(fraction, message))
                    {
                        _cancellation.Cancel();
                    }
                }, null);
                return !_cancellation.IsCancellationRequested;
            }
        }
    }
}
