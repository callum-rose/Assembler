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
        /// <summary>The reviewer's verdict on a just-completed stage.</summary>
        public enum ReviewDecision
        {
            /// <summary>Accept the stage's output and move on to the next stage.</summary>
            Continue,

            /// <summary>Discard this output and run the same stage again.</summary>
            Retry,
        }

        /// <summary>A review gate: awaited after a stage so a caller can inspect <paramref name="stage"/> and
        /// decide whether to <see cref="ReviewDecision.Continue"/> or <see cref="ReviewDecision.Retry"/> it.
        /// Cancel via <paramref name="ct"/> (or by throwing) to abort the pipeline before the next stage.</summary>
        public delegate Task<ReviewDecision> ReviewGate<in T>(T stage, CancellationToken ct);

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
        /// <param name="reviewImage">Optional gate awaited after stage 1 — inspect the image, then continue or retry it.</param>
        /// <param name="reviewMesh">Optional gate awaited after stage 2 — inspect the mesh, then continue or retry it.</param>
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
            var outputDir = ResolveOutputDir(settings, baseName, DateTime.Now);

            // Each gated stage runs in a loop: produce the output, let the optional review gate inspect it,
            // and re-run the same stage if the reviewer chose Retry (overwriting the shared-base-name file).
            // Continue (or no gate) breaks out with the accepted output; Cancel throws via the token.
            async Task<T> RunStage<T>(
                Func<Task<T>> run, ReviewGate<T>? review, string runStatus, string reviewStatus)
            {
                while (true)
                {
                    onStatus?.Invoke(runStatus);
                    var result = await run();

                    if (review is null)
                        return result;

                    onStatus?.Invoke(reviewStatus);
                    var decision = await review(result, ct);
                    ct.ThrowIfCancellationRequested();
                    if (decision is ReviewDecision.Continue)
                        return result;
                }
            }

            // Stage 1 — text → image.
            var image = await RunStage(
                () => ImageGenerationCore.GenerateAsync(
                    settings.ImageProvider, settings.ImageApiKey, settings.ImageModel,
                    settings.Prompt, outputDir, baseName, ct, onStatus),
                reviewImage,
                "Stage 1/3 — generating image…",
                "Review the image, then continue or retry…");

            // Stage 2 — image → mesh. The (possibly retried) image we just accepted is this stage's input.
            var mesh = await RunStage(
                () => MeshyConversionCore.ConvertAsync(
                    settings.MeshyApiKey, image.OutputPath, outputDir, baseName,
                    settings.MeshFormat, settings.GenerateTexture, settings.EnablePbr,
                    settings.Remesh, settings.MeshAiModel, ct, onStatus),
                reviewMesh,
                "Stage 2/3 — converting image to mesh…",
                "Review the mesh, then continue or retry…");

            // Stage 3 — mesh → voxels. The CPU-heavy voxelization runs on a background thread (only the
            // mesh import + final AssetDatabase touch stay on the main thread), so this must be awaited
            // from the main thread — which it is, called from the editor window.
            onStatus?.Invoke("Stage 3/3 — voxelizing mesh…");
            var voxPath = Path.Combine(outputDir, baseName + ".vox");
            var voxels = await VoxConversion.Run(
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

        // Where this run's three files land. By default each run gets its own
        // "<base>_<timestamp>" subfolder under OutputDir so repeated runs don't clobber each
        // other; turn AutoSubfolderPerRun off to write straight into OutputDir.
        public static string ResolveOutputDir(VoxelPipelineSettings settings, string baseName, DateTime startedAt) =>
            settings.AutoSubfolderPerRun
                ? Path.Combine(settings.OutputDir, $"{baseName}_{startedAt.ToString(RunFolderTimeFormat)}")
                : settings.OutputDir;

        // Sortable, filesystem-safe timestamp (no ':' — invalid on Windows/macOS).
        public const string RunFolderTimeFormat = "yyyy-MM-dd_HH-mm-ss";

        // First few words of the text → a filesystem-safe slug, falling back to "model".
        private static string Slug(string text)
        {
            var words = (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(6);
            var invalid = Path.GetInvalidFileNameChars();
            // Strip '.' too: it's a valid filename char, but an embedded dot would be mistaken for a
            // file extension downstream (Path.GetExtension), so EnsureExtension would skip adding .png
            // and the image-to-mesh stage would reject the bogus "extension".
            var cleaned = new string(string.Join("_", words).Where(c => c != '.' && Array.IndexOf(invalid, c) < 0).ToArray());
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

        // When true (default), each run's files are isolated in a "<base>_<timestamp>" subfolder of
        // OutputDir; when false they are written straight into OutputDir.
        public bool AutoSubfolderPerRun = true;
    }

    public sealed class VoxelPipelineException : Exception
    {
        public VoxelPipelineException(string message) : base(message) { }
    }
}
