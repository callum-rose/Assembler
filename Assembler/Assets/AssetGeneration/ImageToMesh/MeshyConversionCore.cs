#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Assembler.AssetGeneration.ImageToMesh
{
    /// <summary>
    /// UI-free core of the image → mesh stage: submit a reference image to Meshy.ai, poll until the
    /// textured model is ready, and download it (plus its sidecar material/texture maps) to disk.
    /// Lives in a runtime assembly with no editor dependency, so it can be driven headlessly (batch,
    /// player build, or as one stage of the image → mesh → voxels pipeline) as well as by the editor
    /// window. No EditorPrefs, window state, or AssetDatabase touch lives here — callers supply an
    /// optional status sink, read the output path off the returned <see cref="Result"/>, and (in an
    /// editor context) refresh the AssetDatabase themselves via <see cref="IsUnderAssets"/>.
    /// Mirrors <c>VoxConversion</c> so the three stages can be chained: this stage's input image is
    /// the text-to-image stage's output, and this stage's <see cref="Result.OutputPath"/> is the
    /// mesh-to-voxels stage's input.
    /// </summary>
    public static class MeshyConversionCore
    {
        /// <summary>Result of a completed conversion: where the model was written + the raw Meshy task.</summary>
        public readonly struct Result
        {
            public Result(string outputPath, MeshyApiClient.TaskResponse task)
            {
                OutputPath = outputPath;
                Task = task;
            }

            /// <summary>Path the model (.obj/.fbx) was written to — feed this into the mesh-to-voxels stage.</summary>
            public string OutputPath { get; }

            /// <summary>The completed Meshy task (model + texture URLs, status, progress).</summary>
            public MeshyApiClient.TaskResponse Task { get; }

            public override string ToString() => OutputPath;
        }

        /// <summary>
        /// Generate a 3D model from <paramref name="request"/>'s image and download it into
        /// <paramref name="outputDir"/>. A blank <paramref name="outputFile"/> falls back to the name
        /// Meshy gave the model; the extension follows <see cref="MeshyRequest.Format"/>.
        /// </summary>
        /// <param name="onStatus">Optional sink for human-readable progress (UI status line / log).</param>
        /// <returns>The written model path plus the completed task, for chaining into the next stage.</returns>
        /// <exception cref="MeshyException">The output directory is empty, or the task returned no usable model.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
        public static async Task<Result> ConvertAsync(
            string apiKey,
            MeshyRequest request,
            string outputDir,
            string outputFile,
            CancellationToken ct = default,
            Action<string>? onStatus = null)
        {
            if (string.IsNullOrWhiteSpace(outputDir))
                throw new MeshyException("Set an output directory.");

            // PBR maps only exist alongside a generated texture.
            var enablePbr = request.GenerateTexture && request.EnablePbr;

            using var client = new MeshyApiClient(apiKey);

            // A generation is paid on submit but lives server-side and is free to re-query, so persist its id
            // the moment we have one. If a previous run into this same directory was interrupted after
            // submitting (network drop, crash, cancel), resume that task instead of paying for a new one; the
            // marker is cleared once the task terminates (success or terminal failure).
            var sidecar = Path.Combine(Path.GetFullPath(outputDir), TaskSidecarFileName);
            var resumeId = TryReadTaskSidecar(sidecar);

            string taskId;
            if (resumeId != null)
            {
                onStatus?.Invoke($"Resuming existing Meshy task {resumeId}…");
                taskId = resumeId;
            }
            else
            {
                onStatus?.Invoke("Submitting image…");
                taskId = await client.CreateTaskAsync(request, ct);
                WriteTaskSidecar(sidecar, taskId);
            }

            MeshyApiClient.TaskResponse task;
            try
            {
                onStatus?.Invoke($"Queued (task {taskId}). Generating…");
                task = await client.PollUntilCompleteAsync(
                    taskId, (p, s) => onStatus?.Invoke($"{s} — {p}%"), ct);
            }
            catch (MeshyTaskFailedException)
            {
                // The task is dead server-side; drop the marker so a rerun starts fresh rather than resuming it.
                // A transient/network failure or cancellation instead leaves the marker in place to resume.
                TryDeleteTaskSidecar(sidecar);
                throw;
            }

            var savedPath = await DownloadResultsAsync(
                client, task, outputDir, outputFile, request.Format, enablePbr, ct, onStatus);

            // Outputs are on disk and the task succeeded — the resume marker is no longer needed.
            TryDeleteTaskSidecar(sidecar);

            onStatus?.Invoke($"Done. Saved to {savedPath}");
            return new Result(savedPath, task);
        }

        private static async Task<string> DownloadResultsAsync(
            MeshyApiClient client,
            MeshyApiClient.TaskResponse task,
            string outputDir,
            string outputFile,
            ModelFormat format,
            bool enablePbr,
            CancellationToken ct,
            Action<string>? onStatus)
        {
            var urls = task.model_urls
                ?? throw new MeshyException("Task succeeded but returned no model URLs.");

            var modelUrl = format == ModelFormat.Obj ? urls.obj : urls.fbx;
            if (string.IsNullOrEmpty(modelUrl))
                throw new MeshyException($"No {format} URL in the result.");

            var ext = format == ModelFormat.Obj ? "obj" : "fbx";
            var dir = Path.GetFullPath(outputDir);
            var baseName = ResolveBaseName(outputFile, modelUrl!);
            var outputPath = Path.Combine(dir, $"{baseName}.{ext}");

            onStatus?.Invoke($"Downloading {format} model…");
            await client.DownloadAsync(modelUrl!, outputPath, ct);

            // OBJ ships its material library separately; keep it beside the model.
            string? mtlPath = null;
            if (format == ModelFormat.Obj && !string.IsNullOrEmpty(urls.mtl))
            {
                mtlPath = Path.Combine(dir, $"{baseName}.mtl");
                await client.DownloadAsync(urls.mtl!, mtlPath, ct);
            }

            // Texture maps are delivered as separate files; download them alongside.
            string? baseColorFile = null;
            var tex = task.texture_urls is { Length: > 0 } ? task.texture_urls[0] : null;
            if (tex != null)
            {
                onStatus?.Invoke("Downloading texture maps…");
                baseColorFile = await SaveMapAsync(client, tex.base_color, dir, $"{baseName}_basecolor", ct);
                if (enablePbr)
                {
                    await SaveMapAsync(client, tex.metallic, dir, $"{baseName}_metallic", ct);
                    await SaveMapAsync(client, tex.roughness, dir, $"{baseName}_roughness", ct);
                    await SaveMapAsync(client, tex.normal, dir, $"{baseName}_normal", ct);
                }
            }

            // Meshy's .mtl names its texture independently of how we save the base-colour map
            // (its map_Kd is e.g. "texture_0.png"), so the referenced file doesn't exist on disk.
            // Point map_Kd at the file we actually wrote, so the material carries the texture and
            // the downstream voxelizer samples it instead of falling back to a flat untextured colour.
            if (mtlPath != null && baseColorFile != null)
                PointMtlAtTexture(mtlPath, baseColorFile);

            return outputPath;
        }

        // The base filename for the downloaded model + its sidecar maps. An explicit
        // file name wins; otherwise fall back to the name Meshy gave the model URL.
        private static string ResolveBaseName(string outputFile, string modelUrl)
        {
            if (!string.IsNullOrWhiteSpace(outputFile))
                return Path.GetFileNameWithoutExtension(outputFile.Trim());

            var fromUrl = Path.GetFileNameWithoutExtension(modelUrl.Split('?')[0]);
            return string.IsNullOrEmpty(fromUrl) ? "model" : fromUrl;
        }

        // Downloads a texture map and returns the file name it was saved under (relative to
        // <paramref name="dir"/>), or null if there was no URL — so the caller can wire it into the .mtl.
        private static async Task<string?> SaveMapAsync(
            MeshyApiClient client, string? url, string dir, string baseName, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(url))
                return null;

            // Meshy serves PNGs; strip any query string before reading the extension.
            var clean = url!.Split('?')[0];
            var ext = Path.GetExtension(clean);
            if (string.IsNullOrEmpty(ext))
                ext = ".png";

            var fileName = baseName + ext;
            await client.DownloadAsync(url, Path.Combine(dir, fileName), ct);
            return fileName;
        }

        // Rewrite the .mtl's map_Kd to reference the base-colour texture we downloaded (a sibling
        // file, so a bare name suffices). Replaces every existing map_Kd line; if the material has
        // none, appends one so the texture is still picked up.
        private static void PointMtlAtTexture(string mtlPath, string textureFileName)
        {
            if (!File.Exists(mtlPath))
                return;

            var lines = File.ReadAllLines(mtlPath);
            var rewritten = false;
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("map_Kd", StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = $"map_Kd {textureFileName}";
                    rewritten = true;
                }
            }

            if (rewritten)
                File.WriteAllLines(mtlPath, lines);
            else
                File.AppendAllText(mtlPath, $"{Environment.NewLine}map_Kd {textureFileName}{Environment.NewLine}");
        }

        public static bool IsUnderAssets(string path)
        {
            var full = Path.GetFullPath(path);
            var assets = Path.GetFullPath(Application.dataPath);
            return full.StartsWith(assets, StringComparison.OrdinalIgnoreCase);
        }

        // A dotfile so Unity ignores it (no .meta churn) and it stays out of the way; one per output
        // directory, holding the in-flight Meshy task id so an interrupted run can resume rather than re-pay.
        private const string TaskSidecarFileName = ".meshy-task-id";

        private static string? TryReadTaskSidecar(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                var id = File.ReadAllText(path).Trim();
                return string.IsNullOrEmpty(id) ? null : id;
            }
            catch
            {
                return null; // Unreadable marker → just start fresh.
            }
        }

        private static void WriteTaskSidecar(string path, string taskId)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, taskId);
            }
            catch
            {
                // Non-fatal: persistence is a resilience nicety, not required for the run to complete.
            }
        }

        private static void TryDeleteTaskSidecar(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Ignore — a stale marker only costs a redundant resume attempt on the next run.
            }
        }
    }
}
