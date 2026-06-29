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
    /// Client for Recraft's image API. Text-to-image posts JSON to <c>/v1/images/generations</c>
    /// and gets back an image URL (<c>data[0].url</c>) which is then downloaded; when a reference
    /// image is supplied it posts a multipart image-to-image request instead. Bearer auth with a
    /// Recraft API token (https://www.recraft.ai/). Recraft requires a <c>style</c> — the look is
    /// driven by it (e.g. <c>digital_illustration</c>, <c>realistic_image</c>), so that's the knob
    /// to change for a different aesthetic; Recraft can also build reusable custom styles via its API.
    /// </summary>
    public sealed class RecraftImageGenerator : IImageGenerator
    {
        private const string GenerationsUrl = "https://external.api.recraft.ai/v1/images/generations";
        private const string ImageToImageUrl = "https://external.api.recraft.ai/v1/images/imageToImage";
        private const string DefaultModel = "recraftv3";

        // Recraft requires a style; this neutral default keeps generation working without forcing the
        // caller to pick one. Change it (or wire it to the window later) for a different look.
        private const string DefaultStyle = "digital_illustration";

        private readonly HttpClient _http;

        public RecraftImageGenerator(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("A Recraft API token is required.", nameof(apiKey));

            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }

        public string DisplayName => "Recraft";

        public void Dispose() => _http.Dispose();

        public async Task<GeneratedImage> GenerateAsync(ImageGenerationRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                throw new ImageGenerationException("Prompt is empty.");

            var model = string.IsNullOrWhiteSpace(request.Model) ? DefaultModel : request.Model.Trim();

            using var message = request.ReferenceImage is { } reference
                ? BuildImageToImage(model, request.Prompt, reference)
                : BuildGenerate(model, request.Prompt);

            using var response = await _http.SendAsync(message, ct);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new ImageGenerationException(
                    $"Recraft request failed ({(int)response.StatusCode}): {ProviderSupport.Truncate(json)}");

            var url = ExtractUrl(json);
            return await ProviderSupport.DownloadImageAsync(_http, url, ct);
        }

        private static HttpRequestMessage BuildGenerate(string model, string prompt)
        {
            var body = "{\"prompt\":\"" + ProviderSupport.EscapeJson(prompt) + "\"," +
                       "\"model\":\"" + ProviderSupport.EscapeJson(model) + "\"," +
                       "\"style\":\"" + DefaultStyle + "\"}";
            return new HttpRequestMessage(HttpMethod.Post, GenerationsUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }

        private static HttpRequestMessage BuildImageToImage(string model, string prompt, ReferenceImage reference)
        {
            var form = new MultipartFormDataContent
            {
                { new StringContent(prompt), "prompt" },
                { new StringContent(model), "model" },
                { new StringContent(DefaultStyle), "style" },
                { new StringContent("0.2"), "strength" }, // low strength → stay close to the reference
            };
            var image = new ByteArrayContent(reference.Bytes);
            image.Headers.ContentType = new MediaTypeHeaderValue(reference.MimeType);
            form.Add(image, "image", "reference" + ExtFor(reference.MimeType));

            return new HttpRequestMessage(HttpMethod.Post, ImageToImageUrl) { Content = form };
        }

        private static string ExtFor(string mime) =>
            mime switch
            {
                "image/jpeg" => ".jpg",
                "image/webp" => ".webp",
                _ => ".png",
            };

        private static string ExtractUrl(string json)
        {
            var parsed = UnityEngine.JsonUtility.FromJson<RecraftResponse>(json);
            var url = parsed?.data is { Length: > 0 } ? parsed.data[0].url : null;

            if (string.IsNullOrEmpty(url))
                throw new ImageGenerationException(
                    $"Recraft response contained no image URL. Raw: {ProviderSupport.Truncate(json)}");

            return url!;
        }

        // --- DTOs (field names match the REST JSON for JsonUtility mapping) ---

        [Serializable]
        private sealed class RecraftResponse
        {
            public Datum[]? data;
        }

        [Serializable]
        private sealed class Datum
        {
            public string? url;
        }
    }
}
