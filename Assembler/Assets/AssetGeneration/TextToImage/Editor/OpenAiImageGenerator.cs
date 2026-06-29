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
    /// Client for OpenAI's image API (the <c>gpt-image</c> family). Text-to-image posts JSON
    /// to <c>/v1/images/generations</c>; when a reference image is supplied it posts a
    /// multipart edit to <c>/v1/images/edits</c> instead. Both return base64 PNG in
    /// <c>data[0].b64_json</c>. Get an API key at https://platform.openai.com/api-keys —
    /// the gpt-image models are paid (no free tier).
    /// </summary>
    public sealed class OpenAiImageGenerator : IImageGenerator
    {
        private const string GenerationsUrl = "https://api.openai.com/v1/images/generations";
        private const string EditsUrl = "https://api.openai.com/v1/images/edits";
        private const string DefaultModel = "gpt-image-2";

        private readonly HttpClient _http;

        public OpenAiImageGenerator(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("An OpenAI API key is required.", nameof(apiKey));

            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey.Trim());
        }

        public string DisplayName => "OpenAI";

        public void Dispose() => _http.Dispose();

        public async Task<GeneratedImage> GenerateAsync(ImageGenerationRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                throw new ImageGenerationException("Prompt is empty.");

            var model = string.IsNullOrWhiteSpace(request.Model) ? DefaultModel : request.Model.Trim();

            // A reference image routes to the multipart edits endpoint; otherwise plain JSON generation.
            using var message = request.ReferenceImage is { } reference
                ? BuildEditRequest(model, request.Prompt, reference)
                : BuildGenerateRequest(model, request.Prompt);

            using var response = await _http.SendAsync(message, ct);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new ImageGenerationException(
                    $"OpenAI request failed ({(int)response.StatusCode}): {ProviderSupport.Truncate(json)}");

            return ExtractImage(json);
        }

        private static HttpRequestMessage BuildGenerateRequest(string model, string prompt)
        {
            var body = "{\"model\":\"" + ProviderSupport.EscapeJson(model) + "\"," +
                       "\"prompt\":\"" + ProviderSupport.EscapeJson(prompt) + "\"}";
            return new HttpRequestMessage(HttpMethod.Post, GenerationsUrl)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }

        private static HttpRequestMessage BuildEditRequest(string model, string prompt, ReferenceImage reference)
        {
            var form = new MultipartFormDataContent
            {
                { new StringContent(model), "model" },
                { new StringContent(prompt), "prompt" },
            };
            var image = new ByteArrayContent(reference.Bytes);
            image.Headers.ContentType = new MediaTypeHeaderValue(reference.MimeType);
            form.Add(image, "image", FileNameFor(reference.MimeType));

            return new HttpRequestMessage(HttpMethod.Post, EditsUrl) { Content = form };
        }

        private static string FileNameFor(string mime) =>
            mime switch
            {
                "image/jpeg" => "reference.jpg",
                "image/webp" => "reference.webp",
                _ => "reference.png",
            };

        private static GeneratedImage ExtractImage(string json)
        {
            var parsed = UnityEngine.JsonUtility.FromJson<OpenAiResponse>(json);
            var b64 = parsed?.data is { Length: > 0 } ? parsed.data[0].b64_json : null;

            if (string.IsNullOrEmpty(b64))
                throw new ImageGenerationException(
                    $"Response contained no image data. Raw: {ProviderSupport.Truncate(json)}");

            return new GeneratedImage(Convert.FromBase64String(b64!), "image/png");
        }

        // --- DTOs (field names match the REST JSON for JsonUtility mapping) ---

        [Serializable]
        private sealed class OpenAiResponse
        {
            public Datum[]? data;
        }

        [Serializable]
        private sealed class Datum
        {
            public string? b64_json;
        }
    }
}
