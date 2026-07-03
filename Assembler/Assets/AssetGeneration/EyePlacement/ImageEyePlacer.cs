using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;
using UnityEngine;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>The parsed 2D eye picks from a vision call, with the raw reply kept for display/logging.</summary>
    public sealed record EyePicks(IReadOnlyList<Vector2> Points, string RawResponse);

    /// <summary>
    /// Headless core: asks Claude to point at where the eyes go on the creature in an
    /// image, returning each eye as a normalised (x, y) image coordinate — x right, y
    /// down, both in [0, 1], top-left origin. This is only the 2D half; turning a pick
    /// into a 3D grid coordinate is <see cref="VoxelViewProjection"/> + <see cref="VoxelRaycaster"/>,
    /// which is why the image passed here should be the render of the model (or a view
    /// that matches the projection used to reproject). Mirrors
    /// <c>ImageFacingDirection</c>: every input is an argument, the result is returned,
    /// and there is no editor or shared state, so it runs from a window, batch mode, a
    /// test, or a player build.
    /// </summary>
    public static class ImageEyePlacer
    {
        public const string DefaultModel = "claude-haiku-4-5-20251001";

        private const string SystemPrompt =
            "You are a vision assistant that marks where the EYES belong on the main creature or " +
            "character shown in an image. The image is a three-quarter (isometric) render of a voxel " +
            "model, so you can usually see a front, one side and the top.\n\n" +
            "Report each eye as a coordinate in the image using this convention:\n" +
            "  x = 0 is the LEFT edge, x = 1 is the RIGHT edge\n" +
            "  y = 0 is the TOP edge,  y = 1 is the BOTTOM edge\n\n" +
            "Place eyes where they read naturally on the head: symmetric about the face, on whichever " +
            "surface faces the viewer (front OR side), typically in the upper portion of the head. If " +
            "the creature clearly has eyes on the side of its head (a fish, a horse), put them on the " +
            "visible side. Aim for the surface of the model, not empty space around it.\n\n" +
            "Respond with ONE line per eye, each line exactly \"x, y\" with decimals in [0, 1], and " +
            "NOTHING else — no labels, no prose, no extra punctuation.";

        /// <summary>
        /// Determines eye picks from raw image bytes, building (and disposing) its own
        /// <see cref="AnthropicClient"/> from <paramref name="apiKey"/>.
        /// </summary>
        /// <param name="apiKey">Anthropic API key.</param>
        /// <param name="imageData">Encoded image bytes (PNG/JPEG/GIF/WebP).</param>
        /// <param name="mediaType">MIME type of <paramref name="imageData"/>, e.g. "image/png".</param>
        /// <param name="eyeCount">How many eyes to request (clamped to at least 1).</param>
        /// <param name="model">Model id; falls back to <see cref="DefaultModel"/> (Haiku) when null/blank.</param>
        public static async Task<EyePicks> DetermineAsync(
            string apiKey,
            byte[] imageData,
            string mediaType,
            int eyeCount = 2,
            string? model = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key is required.", nameof(apiKey));
            }

            var resolvedModel = string.IsNullOrWhiteSpace(model) ? DefaultModel : model!;
            using var client = new AnthropicClient(apiKey, resolvedModel, maxTokens: 256);
            return await DetermineAsync(client, new AnthropicImage(mediaType, imageData), eyeCount, cancellationToken);
        }

        /// <summary>Determines eye picks using a caller-owned client, so one client can serve many images.</summary>
        public static async Task<EyePicks> DetermineAsync(
            AnthropicClient client,
            AnthropicImage image,
            int eyeCount = 2,
            CancellationToken cancellationToken = default)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }
            if (image.IsEmpty)
            {
                throw new ArgumentException("An image is required.", nameof(image));
            }

            int wanted = Mathf.Max(1, eyeCount);
            string instruction =
                $"Mark {wanted} {(wanted == 1 ? "eye" : "eyes")} on the main creature in this image. " +
                $"Answer with {wanted} line{(wanted == 1 ? "" : "s")}, one \"x, y\" pair each.";

            var message = new AnthropicMessage("user", instruction, new[] { image });
            var response = await client.SendAsync(SystemPrompt, new[] { message }, cancellationToken);
            return new EyePicks(Parse(response, wanted), response.Trim());
        }

        // Numbers may arrive as "0.42, 0.31" per line, but tolerate stray labels/prose by
        // scanning all decimals in order and pairing them (x, y). Values are clamped to the
        // image, and we return at most the requested count.
        private static readonly Regex NumberPattern =
            new(@"[-+]?\d*\.?\d+", RegexOptions.Compiled);

        public static IReadOnlyList<Vector2> Parse(string response, int eyeCount)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return Array.Empty<Vector2>();
            }

            var numbers = NumberPattern.Matches(response)
                .Cast<Match>()
                .Select(m => float.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f)
                    ? (float?)f
                    : null)
                .Where(f => f.HasValue)
                .Select(f => f!.Value)
                .ToList();

            var points = new List<Vector2>();
            for (int i = 0; i + 1 < numbers.Count && points.Count < Mathf.Max(1, eyeCount); i += 2)
            {
                points.Add(new Vector2(Mathf.Clamp01(numbers[i]), Mathf.Clamp01(numbers[i + 1])));
            }

            return points;
        }
    }
}
