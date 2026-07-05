#nullable enable

using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;
using Polly.Retry;
using UnityEngine;

namespace Assembler.AssetGeneration.Resilience
{
    /// <summary>
    /// Thrown from inside a resilience-guarded HTTP call to signal a <i>transient</i> status the
    /// pipeline should retry (429, 503, 5xx, …). It is distinct from the fatal exceptions the callers
    /// throw for genuine, non-retryable failures (an <c>ImageGenerationException</c> / <c>MeshyException</c>
    /// for a 4xx, a bad payload, etc.) so the pipeline retries only the former and lets the latter fail fast.
    /// </summary>
    public sealed class TransientHttpException : Exception
    {
        public TransientHttpException(int statusCode)
            : base($"Transient HTTP status {statusCode}.")
        {
            StatusCode = statusCode;
        }

        public int StatusCode { get; }
    }

    /// <summary>
    /// Shared Polly (v8) resilience for the asset-generation HTTP clients (image providers + Meshy).
    /// Two pipelines encode the pipeline's two safety profiles:
    /// <list type="bullet">
    /// <item><see cref="IdempotentGet"/> — for reads that can be repeated freely (Meshy polling and the
    /// model/texture downloads, the FLUX poll, the Recraft/FLUX image fetch). Retries both connection-level
    /// faults (<see cref="HttpRequestException"/>/<see cref="IOException"/>) and transient HTTP statuses, so a
    /// blip mid-download doesn't discard a completed (paid) generation.</item>
    /// <item><see cref="Submit"/> — for the paid submit calls (Meshy create-task, image generation). Retries
    /// <i>only</i> when the server explicitly rejected the request (429/503) — no work was started, so a
    /// resend can't double-charge. It deliberately does <b>not</b> retry connection drops, where a lost
    /// response might mean the job already began server-side.</item>
    /// </list>
    /// Callers run their send-and-read inside <c>pipeline.ExecuteAsync(...)</c>, calling
    /// <see cref="ThrowIfTransient"/> (idempotent) or <see cref="ThrowIfServerRejected"/> (submit) on the
    /// response status to opt a retryable status into the retry; any other non-success status they turn into
    /// their own fatal exception, which the pipeline passes straight through.
    /// </summary>
    public static class HttpResilience
    {
        /// <summary>Retry profile for repeatable reads: connection faults + transient statuses, ~1/2/4/8s backoff.</summary>
        public static readonly ResiliencePipeline IdempotentGet = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<TransientHttpException>()
                    .Handle<HttpRequestException>()
                    .Handle<IOException>(),
                MaxRetryAttempts = 4,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = LogRetry("request"),
            })
            .Build();

        /// <summary>Retry profile for paid submits: only server-reject statuses (429/503), ~2/4/8s backoff.</summary>
        public static readonly ResiliencePipeline Submit = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                // Only TransientHttpException (429/503 via ThrowIfServerRejected) — NOT HttpRequestException,
                // so a connection drop after the request was sent fails fast rather than risking a re-charge.
                ShouldHandle = new PredicateBuilder().Handle<TransientHttpException>(),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                OnRetry = LogRetry("submit"),
            })
            .Build();

        /// <summary>
        /// Raise a retryable <see cref="TransientHttpException"/> if <paramref name="statusCode"/> is one an
        /// idempotent request should retry — request timeout (408), rate-limit (429), or any 5xx. Callers pass
        /// the response status here before their own success check so those statuses drive a retry.
        /// </summary>
        public static void ThrowIfTransient(int statusCode)
        {
            if (statusCode is 408 or 429 or (>= 500 and <= 599))
                throw new TransientHttpException(statusCode);
        }

        /// <summary>
        /// Raise a retryable <see cref="TransientHttpException"/> only if the server explicitly declined the
        /// request without acting on it — rate-limited (429) or temporarily unavailable (503) — so re-sending a
        /// paid submit cannot double-charge. Every other status (including other 5xx, which may mean the request
        /// was partially processed) is left for the caller to treat as fatal.
        /// </summary>
        public static void ThrowIfServerRejected(int statusCode)
        {
            if (statusCode is 429 or 503)
                throw new TransientHttpException(statusCode);
        }

        private static Func<OnRetryArguments<object>, ValueTask> LogRetry(string what) =>
            args =>
            {
                Debug.LogWarning(
                    $"[AssetGeneration] Transient {what} failure " +
                    $"({args.Outcome.Exception?.Message ?? "unknown"}); " +
                    $"retry {args.AttemptNumber + 1} in {args.RetryDelay.TotalSeconds:0.#}s.");
                return default;
            };
    }
}
