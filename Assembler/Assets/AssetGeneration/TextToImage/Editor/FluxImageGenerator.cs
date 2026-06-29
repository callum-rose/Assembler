#nullable enable

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assembler.AssetGeneration.TextToImage.Editor
{
    /// <summary>
    /// Client for Black Forest Labs' FLUX API. BFL is asynchronous: POST the prompt to the
    /// model endpoint (the model id is the path segment, e.g. <c>/v1/flux-2-pro</c>), receive
    /// a <c>polling_url</c>, then GET it until <c>status</c> is <c>"Ready"</c> and download
    /// <c>result.sample</c>. Signed result URLs expire ~10 minutes after completion, so the
    /// image is downloaded immediately. Auth is the <c>x-key</c> header; key at
    /// https://docs.bfl.ai (paid). A reference image is passed as <c>input_image</c> (base64).
    /// </summary>
    public sealed class FluxImageGenerator : IImageGenerator
    {
        private const string BaseUrl = "https://api.bfl.ai/v1";
        private const string DefaultModel = "flux-2-pro";

        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1.5);
        private const int MaxPolls = 200; // ~5 minutes at the interval above before giving up.

        private readonly HttpClient _http;

        public FluxImageGenerator(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("A Black Forest Labs API key is required.", nameof(apiKey));

            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            _http.DefaultRequestHeaders.Add("x-key", apiKey.Trim());
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        public string DisplayName => "Black Forest Labs (FLUX)";

        public void Dispose() => _http.Dispose();

        public async Task<GeneratedImage> GenerateAsync(ImageGenerationRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                throw new ImageGenerationException("Prompt is empty.");

            var model = string.IsNullOrWhiteSpace(request.Model) ? DefaultModel : request.Model.Trim();
            var pollingUrl = await SubmitAsync(model, request, ct);
            var sampleUrl = await PollForSampleAsync(pollingUrl, ct);
            return await ProviderSupport.DownloadImageAsync(_http, sampleUrl, ct);
        }

        private async Task<string> SubmitAsync(string model, ImageGenerationRequest request, CancellationToken ct)
        {
            var sb = new StringBuilder();
            sb.Append("{\"prompt\":\"").Append(ProviderSupport.EscapeJson(request.Prompt)).Append('"');
            sb.Append(",\"width\":1024,\"height\":1024");
            if (request.ReferenceImage is { } reference)
                sb.Append(",\"input_image\":\"").Append(Convert.ToBase64String(reference.Bytes)).Append('"');
            sb.Append('}');

            using var content = new StringContent(sb.ToString(), Encoding.UTF8, "application/json");
            using var response = await _http.PostAsync($"{BaseUrl}/{model}", content, ct);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new ImageGenerationException(
                    $"FLUX submit failed ({(int)response.StatusCode}): {ProviderSupport.Truncate(json)}");

            var submit = UnityEngine.JsonUtility.FromJson<SubmitResponse>(json);
            if (submit is null || string.IsNullOrEmpty(submit.polling_url))
                throw new ImageGenerationException(
                    $"FLUX submit returned no polling_url. Raw: {ProviderSupport.Truncate(json)}");

            return submit.polling_url!;
        }

        private async Task<string> PollForSampleAsync(string pollingUrl, CancellationToken ct)
        {
            for (var poll = 0; poll < MaxPolls; poll++)
            {
                await Task.Delay(PollInterval, ct);

                using var response = await _http.GetAsync(pollingUrl, ct);
                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    throw new ImageGenerationException(
                        $"FLUX poll failed ({(int)response.StatusCode}): {ProviderSupport.Truncate(json)}");

                var result = UnityEngine.JsonUtility.FromJson<PollResponse>(json);
                switch (result?.status)
                {
                    case "Ready" when !string.IsNullOrEmpty(result.result?.sample):
                        return result.result!.sample!;
                    case "Ready":
                        throw new ImageGenerationException("FLUX reported Ready but returned no image URL.");
                    case "Error" or "Failed":
                        throw new ImageGenerationException(
                            $"FLUX generation failed: {ProviderSupport.Truncate(json)}");
                    // "Pending" / "Request Moderated" / null → keep polling.
                }
            }

            throw new ImageGenerationException("FLUX generation timed out while polling for the result.");
        }

        // --- DTOs (field names match the REST JSON for JsonUtility mapping) ---

        [Serializable]
        private sealed class SubmitResponse
        {
            public string? id;
            public string? polling_url;
        }

        [Serializable]
        private sealed class PollResponse
        {
            public string? status;
            public PollResult? result;
        }

        [Serializable]
        private sealed class PollResult
        {
            public string? sample;
        }
    }
}
