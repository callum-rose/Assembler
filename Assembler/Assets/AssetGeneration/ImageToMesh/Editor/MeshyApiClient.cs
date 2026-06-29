#nullable enable

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshyImageTo3D.AssetGeneration.ImageToMesh.Editor
{
    /// <summary>
    /// Minimal client for the Meshy.ai "Image to 3D" OpenAPI v1 endpoint.
    /// Minimal: one create call, polled until the task finishes, then the
    /// requested model format plus its texture maps are downloaded to disk.
    /// </summary>
    public sealed class MeshyApiClient : IDisposable
    {
        private const string BaseUrl = "https://api.meshy.ai/openapi/v1/image-to-3d";

        private readonly HttpClient _http;

        public MeshyApiClient(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("A Meshy API key is required.", nameof(apiKey));

            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey.Trim()}");
        }

        public void Dispose() => _http.Dispose();

        /// <summary>Submit the image-to-3D job and return the Meshy task id.</summary>
        public async Task<string> CreateTaskAsync(MeshyRequest request, CancellationToken ct)
        {
            var dataUri = BuildImageDataUri(request.ImagePath);
            var body = BuildRequestJson(dataUri, request);

            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync(BaseUrl, content, ct);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new MeshyException($"Create task failed ({(int)response.StatusCode}): {json}");

            var parsed = JsonUtility.FromJson<CreateResponse>(json);
            if (parsed == null || string.IsNullOrEmpty(parsed.result))
                throw new MeshyException($"Create task returned no id: {json}");

            return parsed.result!;
        }

        /// <summary>Poll a task until it succeeds or fails, reporting progress 0..100.</summary>
        public async Task<TaskResponse> PollUntilCompleteAsync(
            string taskId, Action<int, string> onProgress, CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                using var response = await _http.GetAsync($"{BaseUrl}/{taskId}", ct);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new MeshyException($"Poll failed ({(int)response.StatusCode}): {json}");

                var task = JsonUtility.FromJson<TaskResponse>(json);
                if (task == null)
                    throw new MeshyException($"Could not parse task status: {json}");

                onProgress(task.progress, task.status ?? "");

                switch (task.status)
                {
                    case "SUCCEEDED":
                        return task;
                    case "FAILED":
                    case "CANCELED":
                    case "EXPIRED":
                        throw new MeshyException(
                            $"Task {task.status}: {task.task_error?.message ?? "no detail"}");
                    default:
                        await Task.Delay(TimeSpan.FromSeconds(3), ct);
                        break;
                }
            }
        }

        public async Task DownloadAsync(string url, string destinationPath, CancellationToken ct)
        {
            var bytes = await _http.GetByteArrayAsync(url);
            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.WriteAllBytes(destinationPath, bytes);
        }

        private static string BuildImageDataUri(string imagePath)
        {
            if (!File.Exists(imagePath))
                throw new MeshyException($"Reference image not found: {imagePath}");

            var mime = Path.GetExtension(imagePath).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                var other => throw new MeshyException($"Unsupported image type '{other}' (use png/jpg/webp)."),
            };

            var base64 = Convert.ToBase64String(File.ReadAllBytes(imagePath));
            return $"data:{mime};base64,{base64}";
        }

        // Built by hand rather than JsonUtility so optional fields can be omitted cleanly.
        private static string BuildRequestJson(string dataUri, MeshyRequest request)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append($"\"image_url\":\"{dataUri}\"");
            AppendString(sb, "ai_model", request.AiModel);

            // Texturing.
            AppendBool(sb, "should_texture", request.GenerateTexture);
            AppendBool(sb, "enable_pbr", request.EnablePbr);
            AppendBool(sb, "hd_texture", request.HdTexture);

            // Geometry / remeshing.
            AppendBool(sb, "should_remesh", request.Remesh);
            AppendString(sb, "topology", request.Topology == MeshyTopology.Quad ? "quad" : "triangle");
            // decimation_mode and target_polycount are alternative ways to control the remeshed
            // polycount and only apply when remeshing; emit whichever the caller set, else let Meshy default.
            if (request.Remesh)
            {
                if (DecimationModeValue(request.Decimation) is { } mode)
                    AppendInt(sb, "decimation_mode", mode);
                if (request.TargetPolycount > 0)
                    AppendInt(sb, "target_polycount", request.TargetPolycount);
            }
            AppendBool(sb, "save_pre_remeshed_model", request.SavePreRemeshedModel);

            // Output presentation. remove_lighting is a meshy-6-only flag — omit it entirely for any
            // other model rather than sending it (even as false), which is an invalid request.
            if (request.AiModel == "meshy-6")
                AppendBool(sb, "remove_lighting", request.RemoveLighting);
            AppendBool(sb, "moderation", request.Moderation);
            AppendBool(sb, "auto_size", request.AutoSize);
            AppendString(sb, "origin_at", request.OriginAt == ModelOrigin.Center ? "center" : "bottom");
            AppendBool(sb, "multi_view_thumbnails", request.MultiViewThumbnails);
            AppendBool(sb, "alpha_thumbnail", request.AlphaThumbnail);

            // Ask Meshy to generate exactly the format we download, keeping generation and download in sync.
            var format = request.Format == ModelFormat.Fbx ? "fbx" : "obj";
            sb.Append($",\"target_formats\":[\"{format}\"]");

            sb.Append('}');
            return sb.ToString();

            static void AppendString(StringBuilder b, string key, string value) =>
                b.Append($",\"{key}\":\"{value}\"");
            static void AppendBool(StringBuilder b, string key, bool value) =>
                b.Append($",\"{key}\":{(value ? "true" : "false")}");
            static void AppendInt(StringBuilder b, string key, int value) =>
                b.Append($",\"{key}\":{value}");
            static int? DecimationModeValue(DecimationMode mode) => mode switch
            {
                DecimationMode.Low => 1,
                DecimationMode.Medium => 2,
                DecimationMode.High => 3,
                DecimationMode.Ultra => 4,
                _ => null,
            };
        }

        // --- DTOs (snake_case fields to match JsonUtility name-based mapping) ---

        [Serializable]
        private sealed class CreateResponse
        {
            public string? result;
        }

        [Serializable]
        public sealed class TaskResponse
        {
            public string? id;
            public string? status;
            public int progress;
            public ModelUrls? model_urls;
            public TextureUrl[]? texture_urls;
            public TaskError? task_error;
        }

        [Serializable]
        public sealed class ModelUrls
        {
            public string? glb;
            public string? fbx;
            public string? obj;
            public string? mtl;
            public string? usdz;
        }

        [Serializable]
        public sealed class TextureUrl
        {
            public string? base_color;
            public string? metallic;
            public string? roughness;
            public string? normal;
        }

        [Serializable]
        public sealed class TaskError
        {
            public string? message;
        }
    }

    public sealed class MeshyException : Exception
    {
        public MeshyException(string message) : base(message) { }
    }

    public enum ModelFormat
    {
        Obj,
        Fbx,
    }

    /// <summary>Mesh face topology Meshy targets when remeshing (<c>topology</c>).</summary>
    public enum MeshyTopology
    {
        Triangle,
        Quad,
    }

    /// <summary>Where the model's pivot/origin sits (<c>origin_at</c>).</summary>
    public enum ModelOrigin
    {
        Bottom,
        Center,
    }

    /// <summary>
    /// Remesh decimation preset (<c>decimation_mode</c> 1–4 = low/medium/high/ultra). <see cref="None"/>
    /// omits the field so Meshy uses its own default; an explicit <c>target_polycount</c> is the
    /// alternative way to control the remeshed polycount.
    /// </summary>
    public enum DecimationMode
    {
        None,
        Low,
        Medium,
        High,
        Ultra,
    }

    // Plain mutable struct (get/set, not init) because editor-only asmdefs in this
    // project don't have the IsExternalInit polyfill that init accessors require.
    // NOTE: bool fields default to false, which differs from Meshy's API default for
    // remove_lighting (true) — callers should set RemoveLighting explicitly.
    public struct MeshyRequest
    {
        public string ImagePath { get; set; }
        public ModelFormat Format { get; set; }
        public string AiModel { get; set; }

        // Texturing.
        public bool GenerateTexture { get; set; }
        public bool EnablePbr { get; set; }
        public bool HdTexture { get; set; }

        // Geometry / remeshing.
        public bool Remesh { get; set; }
        public MeshyTopology Topology { get; set; }
        public DecimationMode Decimation { get; set; }
        public int TargetPolycount { get; set; }
        public bool SavePreRemeshedModel { get; set; }

        // Output presentation.
        public bool RemoveLighting { get; set; }
        public bool Moderation { get; set; }
        public bool AutoSize { get; set; }
        public ModelOrigin OriginAt { get; set; }
        public bool MultiViewThumbnails { get; set; }
        public bool AlphaThumbnail { get; set; }
    }
}
