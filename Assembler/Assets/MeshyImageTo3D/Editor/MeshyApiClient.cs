#nullable enable

using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Assembler.MeshyImageTo3D
{
    /// <summary>
    /// Minimal client for the Meshy.ai "Image to 3D" OpenAPI v1 endpoint.
    /// Spike-quality: one create call, polled until the task finishes, then the
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
            sb.Append($"\"image_url\":\"{dataUri}\",");
            sb.Append($"\"should_remesh\":{Lower(request.Remesh)},");
            sb.Append($"\"should_texture\":{Lower(request.GenerateTexture)},");
            sb.Append($"\"enable_pbr\":{Lower(request.EnablePbr)},");
            sb.Append($"\"ai_model\":\"{request.AiModel}\"");
            sb.Append('}');
            return sb.ToString();

            static string Lower(bool b) => b ? "true" : "false";
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

    // Plain mutable struct (get/set, not init) because editor-only asmdefs in this
    // project don't have the IsExternalInit polyfill that init accessors require.
    public struct MeshyRequest
    {
        public string ImagePath { get; set; }
        public ModelFormat Format { get; set; }
        public bool GenerateTexture { get; set; }
        public bool EnablePbr { get; set; }
        public bool Remesh { get; set; }
        public string AiModel { get; set; }
    }
}
