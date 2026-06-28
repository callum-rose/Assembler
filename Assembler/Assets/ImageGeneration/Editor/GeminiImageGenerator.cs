#nullable enable

using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assembler.ImageGeneration
{
    /// <summary>
    /// Minimal client for Google's Gemini image generation (free-tier "AI Studio"
    /// API key). Calls the <c>generateContent</c> endpoint and pulls the first
    /// inline image part out of the response.
    ///
    /// Get an API key at https://aistudio.google.com/apikey — the image models
    /// (e.g. <c>gemini-2.5-flash-image</c>) are available on the free tier.
    /// </summary>
    public sealed class GeminiImageGenerator : IImageGenerator
    {
        private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
        private const string DefaultModel = "gemini-2.5-flash-image";

        private readonly HttpClient _http;
        private readonly string _apiKey;

        public GeminiImageGenerator(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new ArgumentException("A Google Gemini API key is required.", nameof(apiKey));

            _apiKey = apiKey.Trim();
            _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        public string DisplayName => "Google Gemini";

        public void Dispose() => _http.Dispose();

        public async Task<GeneratedImage> GenerateAsync(ImageGenerationRequest request, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request.Prompt))
                throw new ImageGenerationException("Prompt is empty.");

            var model = string.IsNullOrWhiteSpace(request.Model) ? DefaultModel : request.Model.Trim();
            var url = $"{BaseUrl}/{model}:generateContent";
            var body = BuildRequestJson(request.Prompt);

            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            // Key goes in a header rather than the query string so it stays out of logs.
            message.Headers.Add("x-goog-api-key", _apiKey);

            using var response = await _http.SendAsync(message, ct);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new ImageGenerationException(
                    $"Gemini request failed ({(int)response.StatusCode}): {Truncate(json)}");

            return ExtractImage(json);
        }

        // Built by hand rather than JsonUtility so the prompt is escaped cleanly and
        // we can pin responseModalities (required by the preview image models).
        private static string BuildRequestJson(string prompt) =>
            "{\"contents\":[{\"parts\":[{\"text\":\"" + EscapeJson(prompt) + "\"}]}]," +
            "\"generationConfig\":{\"responseModalities\":[\"IMAGE\",\"TEXT\"]}}";

        private static GeneratedImage ExtractImage(string json)
        {
            var parsed = UnityEngine.JsonUtility.FromJson<GeminiResponse>(json);

            if (parsed?.promptFeedback != null && !string.IsNullOrEmpty(parsed.promptFeedback.blockReason))
                throw new ImageGenerationException($"Prompt blocked: {parsed.promptFeedback.blockReason}");

            var parts = parsed?.candidates is { Length: > 0 }
                ? parsed.candidates[0].content?.parts
                : null;

            if (parts != null)
            {
                foreach (var part in parts)
                {
                    var inline = part?.inlineData;
                    if (inline != null && !string.IsNullOrEmpty(inline.data))
                    {
                        var bytes = Convert.FromBase64String(inline.data);
                        var mime = string.IsNullOrEmpty(inline.mimeType) ? "image/png" : inline.mimeType!;
                        return new GeneratedImage(bytes, mime);
                    }
                }
            }

            throw new ImageGenerationException(
                $"Response contained no image data. Raw: {Truncate(json)}");
        }

        private static string EscapeJson(string s)
        {
            var sb = new StringBuilder(s.Length + 16);
            foreach (var c in s)
            {
                switch (c)
                {
                    case '\"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        private static string Truncate(string s) =>
            s.Length <= 600 ? s : s.Substring(0, 600) + "…";

        // --- DTOs (camelCase to match the v1beta REST JSON for JsonUtility mapping) ---

        [Serializable]
        private sealed class GeminiResponse
        {
            public Candidate[]? candidates;
            public PromptFeedback? promptFeedback;
        }

        [Serializable]
        private sealed class Candidate
        {
            public Content? content;
            public string? finishReason;
        }

        [Serializable]
        private sealed class Content
        {
            public Part[]? parts;
        }

        [Serializable]
        private sealed class Part
        {
            public string? text;
            public InlineData? inlineData;
        }

        [Serializable]
        private sealed class InlineData
        {
            public string? mimeType;
            public string? data;
        }

        [Serializable]
        private sealed class PromptFeedback
        {
            public string? blockReason;
        }
    }
}
