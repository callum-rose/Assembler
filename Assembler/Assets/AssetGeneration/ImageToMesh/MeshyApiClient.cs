#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Assembler.AssetGeneration.Resilience;
using UnityEngine;

namespace Assembler.AssetGeneration.ImageToMesh
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

            // Snapshot (with a skew buffer) the moment before submitting, so recovery below can recognise a
            // task this call created if the response is lost.
            var submittedAfterUnixMs = DateTimeOffset.UtcNow
                .AddSeconds(-CreateRecoveryClockSkewSeconds).ToUnixTimeMilliseconds();

            string json;
            try
            {
                // A paid submit: only retry when the server explicitly rejected it (429/503) so a resend can't
                // double-charge; a lost response (connection drop / timeout) is handled by recovery below.
                json = await HttpResilience.Submit.ExecuteAsync(async token =>
                {
                    using var content = new StringContent(body, Encoding.UTF8, "application/json");
                    using var response = await _http.PostAsync(BaseUrl, content, token);
                    var payload = await response.Content.ReadAsStringAsync();

                    HttpResilience.ThrowIfServerRejected((int)response.StatusCode);
                    if (!response.IsSuccessStatusCode)
                        throw new MeshyException($"Create task failed ({(int)response.StatusCode}): {payload}");

                    return payload;
                }, ct);
            }
            catch (Exception e) when (IsConnectionLoss(e, ct))
            {
                // The request may have reached Meshy and started a (charged) job before the connection dropped.
                // Rather than fail — or blindly resubmit and risk a second charge (Meshy has no idempotency key)
                // — look for a task this call just created and adopt it.
                var recovered = await TryRecoverRecentlyCreatedTaskAsync(submittedAfterUnixMs, ct);
                if (recovered != null)
                    return recovered;
                throw;
            }

            var parsed = JsonUtility.FromJson<CreateResponse>(json);
            if (parsed == null || string.IsNullOrEmpty(parsed.result))
                throw new MeshyException($"Create task returned no id: {json}");

            return parsed.result!;
        }

        /// <summary>Retrieve a task's current state by id. A free read — use it to resume or monitor a job
        /// whose id you already hold (e.g. after a run was interrupted mid-generation).</summary>
        public async Task<TaskResponse> GetTaskAsync(string taskId, CancellationToken ct)
        {
            var json = await FetchJsonAsync($"{BaseUrl}/{taskId}", ct);
            return JsonUtility.FromJson<TaskResponse>(json)
                ?? throw new MeshyException($"Could not parse task status: {json}");
        }

        /// <summary>List this account's most recent image-to-3D tasks, newest first. A free read; used to
        /// recover the id of a task whose create response was lost.</summary>
        public async Task<TaskResponse[]> ListRecentTasksAsync(CancellationToken ct, int pageSize = 20)
        {
            var url = $"{BaseUrl}?page_num=1&page_size={pageSize}&sort_by=-created_at";
            var json = await FetchJsonAsync(url, ct);

            // Meshy returns a bare JSON array; JsonUtility can't parse a top-level array, so wrap it first.
            var wrapper = JsonUtility.FromJson<TaskListWrapper>("{\"items\":" + json + "}");
            return wrapper?.items ?? Array.Empty<TaskResponse>();
        }

        /// <summary>Poll a task until it succeeds or fails, reporting progress 0..100.</summary>
        public async Task<TaskResponse> PollUntilCompleteAsync(
            string taskId, Action<int, string> onProgress, CancellationToken ct)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();

                // Each poll is a safe, repeatable read (retried internally against connection blips and
                // transient statuses), so a momentary hiccup doesn't abandon an in-progress (paid) generation.
                var task = await GetTaskAsync(taskId, ct);
                onProgress(task.progress, task.status ?? "");

                switch (task.status)
                {
                    case "SUCCEEDED":
                        return task;
                    case "FAILED":
                    case "CANCELED":
                    case "EXPIRED":
                        throw new MeshyTaskFailedException(
                            $"Task {task.status}: {task.task_error?.message ?? "no detail"}");
                    default:
                        await Task.Delay(TimeSpan.FromSeconds(3), ct);
                        break;
                }
            }
        }

        public async Task DownloadAsync(string url, string destinationPath, CancellationToken ct)
        {
            // Downloads are idempotent GETs: retry connection faults and transient statuses so a blip mid-
            // download doesn't discard the completed model/texture. A hard 4xx (e.g. an expired URL) fails fast.
            var bytes = await HttpResilience.IdempotentGet.ExecuteAsync(async token =>
            {
                using var response = await _http.GetAsync(url, token);
                HttpResilience.ThrowIfTransient((int)response.StatusCode);
                if (!response.IsSuccessStatusCode)
                    throw new MeshyException($"Download failed ({(int)response.StatusCode}) for {url}.");
                return await response.Content.ReadAsByteArrayAsync();
            }, ct);

            ct.ThrowIfCancellationRequested();
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
            File.WriteAllBytes(destinationPath, bytes);
        }

        // Buffer subtracted from the pre-submit timestamp when recognising a recovered task, to absorb
        // client/server clock skew (the created_at we compare against is Meshy's server clock).
        private const double CreateRecoveryClockSkewSeconds = 30;

        // A connection-level failure (drop / DNS / socket) or an HttpClient timeout — where the request may
        // have reached the server. Deliberately NOT a user cancellation (ct fired).
        private static bool IsConnectionLoss(Exception e, CancellationToken ct) =>
            !ct.IsCancellationRequested && e is HttpRequestException or IOException or TaskCanceledException;

        private async Task<string?> TryRecoverRecentlyCreatedTaskAsync(long createdAfterUnixMs, CancellationToken ct)
        {
            try
            {
                var recent = await ListRecentTasksAsync(ct);
                // Listed newest-first: adopt the most recent task plausibly from this submit that is still
                // alive (queued/running) or already done — never a terminally failed one.
                var candidate = recent.FirstOrDefault(t =>
                    t is { id: { Length: > 0 } } &&
                    t.created_at >= createdAfterUnixMs &&
                    t.status is "PENDING" or "IN_PROGRESS" or "SUCCEEDED");

                if (candidate?.id is { } id)
                {
                    Debug.LogWarning(
                        $"[Meshy] Create response was lost; adopted recently-created task {id} instead of " +
                        "resubmitting (avoids a double charge). Verify it matches your request.");
                    return id;
                }
            }
            catch
            {
                // Best-effort: if listing also fails, fall through to surfacing the original create failure.
            }

            return null;
        }

        // Resilient GET returning the raw body: retries connection blips and transient statuses so a momentary
        // hiccup doesn't abort a read; a hard non-2xx (e.g. 404) throws a fatal MeshyException that won't retry.
        private async Task<string> FetchJsonAsync(string url, CancellationToken ct) =>
            await HttpResilience.IdempotentGet.ExecuteAsync(async token =>
            {
                using var response = await _http.GetAsync(url, token);
                var payload = await response.Content.ReadAsStringAsync();

                HttpResilience.ThrowIfTransient((int)response.StatusCode);
                if (!response.IsSuccessStatusCode)
                    throw new MeshyException($"Request failed ({(int)response.StatusCode}) for {url}: {payload}");

                return payload;
            }, ct);

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
            public long created_at; // Unix milliseconds — used to recognise a task we just created.
            public ModelUrls? model_urls;
            public TextureUrl[]? texture_urls;
            public TaskError? task_error;
        }

        // List Tasks returns a bare JSON array; JsonUtility only parses a top-level object, so the array
        // is wrapped as {"items":[...]} before parsing.
        [Serializable]
        private sealed class TaskListWrapper
        {
            public TaskResponse[]? items;
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

    public class MeshyException : Exception
    {
        public MeshyException(string message) : base(message) { }
    }

    /// <summary>
    /// A Meshy task reached a terminal non-success state (FAILED / CANCELED / EXPIRED) server-side — distinct
    /// from a transient transport error, so a caller can treat a persisted task id as dead and stop resuming it.
    /// </summary>
    public sealed class MeshyTaskFailedException : MeshyException
    {
        public MeshyTaskFailedException(string message) : base(message) { }
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
