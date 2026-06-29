#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assembler.ImageGeneration;
using Assembler.MeshyImageTo3D;
using UnityEngine;
using VoxelsFromMeshSpike;

namespace Assembler.TextToVoxelPipeline
{
    /// <summary>
    /// UI-free core that chains the three asset-generation spikes end to end —
    /// text → image (<see cref="ImageGenerationCore"/>) → mesh (<see cref="MeshyConversionCore"/>)
    /// → voxels (<see cref="VoxConversion"/>) — driving each stage's existing core so this and
    /// the editor window (<see cref="VoxelPipelineWindow"/>) run an identical path. No EditorPrefs,
    /// preview textures, or window state lives here: callers pass a <see cref="VoxelPipelineSettings"/>,
    /// an optional status sink, and optional per-stage <i>review gates</i> awaited between stages so a
    /// caller can inspect the intermediate image/mesh before continuing (a headless caller omits them
    /// and the pipeline runs straight through). The three intermediate files share one base name in one
    /// output directory: <c>&lt;base&gt;.png</c> → <c>&lt;base&gt;.obj</c> (+ maps) → <c>&lt;base&gt;.vox</c>.
    /// </summary>
    public static class VoxelPipeline
    {
        /// <summary>A review gate: awaited after a stage so a caller can inspect <paramref name="stage"/>.
        /// Cancel via <paramref name="ct"/> (or by throwing) to abort the pipeline before the next stage.</summary>
        public delegate Task ReviewGate<in T>(T stage, CancellationToken ct);

        /// <summary>The output of all three stages, for inspection or further chaining.</summary>
        public readonly struct Result
        {
            public Result(
                ImageGenerationCore.Result image,
                MeshyConversionCore.Result mesh,
                VoxConversion.Summary voxels)
            {
                Image = image;
                Mesh = mesh;
                Voxels = voxels;
            }

            /// <summary>Stage 1 — the generated image and where it was written.</summary>
            public ImageGenerationCore.Result Image { get; }

            /// <summary>Stage 2 — the downloaded mesh and the completed Meshy task.</summary>
            public MeshyConversionCore.Result Mesh { get; }

            /// <summary>Stage 3 — the written <c>.vox</c> plus grid/voxel/colour counts.</summary>
            public VoxConversion.Summary Voxels { get; }

            public override string ToString() => Voxels.ToString();
        }

        /// <summary>
        /// Run the full text → voxel pipeline described by <paramref name="settings"/>.
        /// </summary>
        /// <param name="reviewImage">Optional gate awaited after stage 1 — inspect the image, then continue.</param>
        /// <param name="reviewMesh">Optional gate awaited after stage 2 — inspect the mesh, then continue.</param>
        /// <param name="voxelProgress">Optional sink for stage 3's slow per-voxel pass; return false to cancel.</param>
        /// <param name="pipelineProgress">Optional <c>(stepName, fraction)</c> sink for stage 3 post-processing.</param>
        /// <param name="onStatus">Optional sink for human-readable progress (UI status line / log).</param>
        /// <exception cref="VoxelPipelineException">The prompt or output directory is empty.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled (incl. by a review gate).</exception>
        public static async Task<Result> RunAsync(
            VoxelPipelineSettings settings,
            CancellationToken ct = default,
            Action<string>? onStatus = null,
            ReviewGate<ImageGenerationCore.Result>? reviewImage = null,
            ReviewGate<MeshyConversionCore.Result>? reviewMesh = null,
            IProgressReporter? voxelProgress = null,
            Action<string, float>? pipelineProgress = null)
        {
            if (settings is null)
                throw new ArgumentNullException(nameof(settings));
            if (string.IsNullOrWhiteSpace(settings.Prompt))
                throw new VoxelPipelineException("Enter a prompt.");
            if (string.IsNullOrWhiteSpace(settings.OutputDir))
                throw new VoxelPipelineException("Set an output directory.");

            var baseName = ResolveBaseName(settings);

            // Stage 1 — text → image.
            onStatus?.Invoke("Stage 1/3 — generating image…");
            var image = await ImageGenerationCore.GenerateAsync(
                settings.ImageProvider, settings.ImageApiKey, settings.ImageModel,
                settings.Prompt, settings.OutputDir, baseName, ct, onStatus);

            if (reviewImage != null)
            {
                onStatus?.Invoke("Review the image, then continue…");
                await reviewImage(image, ct);
            }
            ct.ThrowIfCancellationRequested();

            // Stage 2 — image → mesh. The image we just wrote is this stage's input.
            onStatus?.Invoke("Stage 2/3 — converting image to mesh…");
            var mesh = await MeshyConversionCore.ConvertAsync(
                settings.MeshyApiKey, image.OutputPath, settings.OutputDir, baseName,
                settings.MeshFormat, settings.GenerateTexture, settings.EnablePbr,
                settings.Remesh, settings.MeshAiModel, ct, onStatus);

            if (reviewMesh != null)
            {
                onStatus?.Invoke("Review the mesh, then continue…");
                await reviewMesh(mesh, ct);
            }
            ct.ThrowIfCancellationRequested();

            // Stage 3 — mesh → voxels. Synchronous + CPU-heavy (runs on the calling thread).
            onStatus?.Invoke("Stage 3/3 — voxelizing mesh…");
            var voxPath = Path.Combine(settings.OutputDir, baseName + ".vox");
            var voxels = VoxConversion.Run(
                mesh.OutputPath, voxPath, settings.MaxDimVoxels,
                settings.VoxSettings, settings.Palette, voxelProgress, pipelineProgress);

            onStatus?.Invoke($"Done. {voxels}");
            return new Result(image, mesh, voxels);
        }

        // An explicit base name wins; otherwise the prompt is slugged into one. The same base name
        // is shared by all three stages so the image, mesh, and .vox sit together in the output dir.
        public static string ResolveBaseName(VoxelPipelineSettings settings)
        {
            var explicitName = settings.BaseName?.Trim();
            return Slug(string.IsNullOrEmpty(explicitName) ? settings.Prompt : explicitName!);
        }

        // First few words of the text → a filesystem-safe slug, falling back to "model".
        private static string Slug(string text)
        {
            var words = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(6);
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(string.Join("_", words).Where(c => Array.IndexOf(invalid, c) < 0).ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? "model" : cleaned;
        }
    }

    /// <summary>
    /// All inputs for the three pipeline stages plus the shared output target. Mutable plain fields so
    /// the editor window can bind directly to it; <see cref="VoxSettings"/> reuses the voxel spike's own
    /// per-step settings, and <see cref="Palette"/> the shared master palette.
    /// </summary>
    public sealed class VoxelPipelineSettings
    {
        // Stage 1 — text → image.
        public ImageProvider ImageProvider = ImageProvider.GoogleGemini;
        public string ImageApiKey = "";
        public string ImageModel = ImageGeneratorFactory.DefaultModelFor(ImageProvider.GoogleGemini);
        public string Prompt = "";

        // Stage 2 — image → mesh.
        public string MeshyApiKey = "";
        public ModelFormat MeshFormat = ModelFormat.Obj;
        public string MeshAiModel = "meshy-6";
        public bool GenerateTexture = true;
        public bool EnablePbr = true;
        public bool Remesh = true;

        // Stage 3 — mesh → voxels.
        public int MaxDimVoxels = 32;
        public VoxPipelineSettings VoxSettings = VoxPipelinePresets.For(VoxPipelinePreset.Creature);
        public IReadOnlyList<Color32> Palette = DefaultMasterPalette.Colors;

        // Shared output. A blank base name is derived from the prompt; all three stages share it.
        public string OutputDir = "Assets/TextToVoxel";
        public string BaseName = "";
    }

    public sealed class VoxelPipelineException : Exception
    {
        public VoxelPipelineException(string message) : base(message) { }
    }
}
