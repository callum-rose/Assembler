#nullable enable

using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Assembler.AssetGeneration.TextToImage.Editor
{
    /// <summary>
    /// Small helpers shared by the HTTP-based image providers (JSON escaping, downloading
    /// an image a provider returned as a URL rather than inline bytes, log truncation).
    /// </summary>
    internal static class ProviderSupport
    {
        /// <summary>Escape a string for embedding inside a JSON string literal.</summary>
        public static string EscapeJson(string s)
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

        /// <summary>
        /// GET an image URL and return its bytes + MIME type. Used by providers that return
        /// a link to the result (BFL, Recraft) rather than inline base64 data.
        /// </summary>
        public static async Task<GeneratedImage> DownloadImageAsync(HttpClient http, string url, CancellationToken ct)
        {
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                throw new ImageGenerationException(
                    $"Failed to download generated image ({(int)response.StatusCode}).");

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var mime = response.Content.Headers.ContentType?.MediaType;
            if (string.IsNullOrEmpty(mime) || !mime!.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                mime = GuessMimeFromUrl(url);
            return new GeneratedImage(bytes, mime);
        }

        public static string Truncate(string s) => s.Length <= 600 ? s : s.Substring(0, 600) + "…";

        private static string GuessMimeFromUrl(string url)
        {
            var path = url;
            var query = path.IndexOf('?');
            if (query >= 0)
                path = path.Substring(0, query);

            return path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                   || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg"
                : path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ? "image/webp"
                : "image/png";
        }
    }
}
