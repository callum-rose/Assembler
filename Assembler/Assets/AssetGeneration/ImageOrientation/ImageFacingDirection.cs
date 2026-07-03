using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.AssetGeneration.ImageOrientation
{
    /// <summary>
    /// The parsed outcome of an orientation request: the <see cref="OrientationAnswer"/> discriminated
    /// union (a compass direction or a view index) plus the raw model text for display/logging. The
    /// <see cref="Direction"/>/<see cref="Index"/>/<see cref="Code"/> helpers are conveniences over the
    /// union for the common cases.
    /// </summary>
    public sealed record OrientationResult(OrientationAnswer Answer, string RawResponse)
    {
        /// <summary>The compass direction when the answer was a <see cref="OrientationAnswer.Facing"/>, else null.</summary>
        public FacingDirection? Direction => Answer is OrientationAnswer.Facing f ? f.Direction : null;

        /// <summary>The chosen view index when the answer was a <see cref="OrientationAnswer.ViewIndex"/>, else null.</summary>
        public int? Index => Answer is OrientationAnswer.ViewIndex v ? v.Index : null;

        public string Code => Answer switch
        {
            OrientationAnswer.Facing f => f.Direction.ToCode(),
            OrientationAnswer.ViewIndex v => $"#{v.Index}",
            _ => "(unrecognised)",
        };
    }

    /// <summary>
    /// Headless core logic: asks Claude which direction the front of the main object
    /// in an image is facing, constrained to the eight <see cref="FacingDirection"/>
    /// codes (L, R, U, D, LU, LD, RU, RD). Every input arrives as an argument and the
    /// result is returned — there is no editor or shared state — so it runs equally
    /// from an editor window, batch mode, a test, or a player build.
    /// </summary>
    public static class ImageFacingDirection
    {
        public const string DefaultModel = "claude-haiku-4-5-20251001";

        private const string SystemPrompt =
            "You are a vision assistant that identifies which direction the FRONT of the main object " +
            "in an image is facing, within the flat 2D plane of the image as the viewer sees it.\n\n" +
            "Directions are relative to the image edges:\n" +
            "  L  = front points toward the left edge\n" +
            "  R  = front points toward the right edge\n" +
            "  U  = front points toward the top edge\n" +
            "  D  = front points toward the bottom edge\n" +
            "  LU = front points toward the top-left corner\n" +
            "  LD = front points toward the bottom-left corner\n" +
            "  RU = front points toward the top-right corner\n" +
            "  RD = front points toward the bottom-right corner\n\n" +
            "For example, if the image shows a car whose front points at the bottom-left corner, answer LD.\n\n" +
            "Pick the single closest code. Respond with EXACTLY that code and nothing else — no punctuation, " +
            "no explanation. Your entire reply must be one of: L, R, U, D, LU, LD, RU, RD.";

        private const string Instruction =
            "Which direction is the front of the main object in this image facing? " +
            "Answer with one code only.";

        private const string SelectViewSystemPrompt =
            "You are a vision assistant that picks which rendered view best shows the FRONT (the face) " +
            "of the main creature or character. You are given several images of the same object from " +
            "different angles, provided in order and numbered starting at 0 (the first image is 0, the " +
            "next is 1, and so on).\n\n" +
            "Choose the single image in which the face / front is most clearly visible and facing toward " +
            "the viewer. Respond with EXACTLY that image's number and nothing else — no words, no " +
            "punctuation, just one integer.";

        private const string SelectViewInstruction =
            "The images above are numbered 0 to N-1 in order. Reply with the number of the one that best " +
            "shows the front/face. One integer only.";

        /// <summary>
        /// Determines the facing direction from raw image bytes, building (and disposing)
        /// its own <see cref="AnthropicClient"/> from <paramref name="apiKey"/>.
        /// </summary>
        /// <param name="apiKey">Anthropic API key.</param>
        /// <param name="imageData">Encoded image bytes (PNG/JPEG/GIF/WebP).</param>
        /// <param name="mediaType">MIME type of <paramref name="imageData"/>, e.g. "image/png".</param>
        /// <param name="model">Model id; falls back to <see cref="DefaultModel"/> (Haiku) when null/blank.</param>
        public static async Task<OrientationResult> DetermineAsync(
            string apiKey,
            byte[] imageData,
            string mediaType,
            string? model = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key is required.", nameof(apiKey));
            }

            var resolvedModel = string.IsNullOrWhiteSpace(model) ? DefaultModel : model!;
            using var client = new AnthropicClient(apiKey, resolvedModel, maxTokens: 64);
            return await DetermineAsync(client, new AnthropicImage(mediaType, imageData), cancellationToken);
        }

        /// <summary>
        /// Determines the facing direction using a caller-owned <see cref="AnthropicClient"/>,
        /// so a single client can be reused across many images.
        /// </summary>
        public static async Task<OrientationResult> DetermineAsync(
            AnthropicClient client,
            AnthropicImage image,
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

            var message = new AnthropicMessage("user", Instruction, new[] { image });
            var response = await client.SendAsync(SystemPrompt, new[] { message }, cancellationToken);
            OrientationAnswer answer = FacingDirectionExtensions.Parse(response) is { } direction
                ? new OrientationAnswer.Facing(direction)
                : new OrientationAnswer.Unrecognised();
            return new OrientationResult(answer, response.Trim());
        }

        /// <summary>
        /// Given several candidate images of the same object (e.g. a ring of isometric views), asks
        /// Claude which one best shows the front/face and returns its index as an
        /// <see cref="OrientationAnswer.ViewIndex"/>. Builds and disposes its own client.
        /// </summary>
        /// <param name="images">Candidate images, in order; index 0 is the first.</param>
        public static async Task<OrientationResult> SelectViewAsync(
            string apiKey,
            IReadOnlyList<byte[]> images,
            string mediaType = "image/png",
            string? model = null,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new ArgumentException("API key is required.", nameof(apiKey));
            }

            var resolvedModel = string.IsNullOrWhiteSpace(model) ? DefaultModel : model!;
            using var client = new AnthropicClient(apiKey, resolvedModel, maxTokens: 16);
            var anthropicImages = images.Select(bytes => new AnthropicImage(mediaType, bytes)).ToList();
            return await SelectViewAsync(client, anthropicImages, cancellationToken);
        }

        /// <summary>Selects the best-front view using a caller-owned client. See the apiKey overload.</summary>
        public static async Task<OrientationResult> SelectViewAsync(
            AnthropicClient client,
            IReadOnlyList<AnthropicImage> images,
            CancellationToken cancellationToken = default)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            var usable = images.Where(image => !image.IsEmpty).ToList();
            if (usable.Count == 0)
            {
                throw new ArgumentException("At least one image is required.", nameof(images));
            }

            var message = new AnthropicMessage("user", SelectViewInstruction, usable);
            var response = await client.SendAsync(SelectViewSystemPrompt, new[] { message }, cancellationToken);
            OrientationAnswer answer = ParseIndex(response) is { } index
                ? new OrientationAnswer.ViewIndex(index)
                : new OrientationAnswer.Unrecognised();
            return new OrientationResult(answer, response.Trim());
        }

        /// <summary>
        /// Reads the first integer out of a model reply (tolerating surrounding prose), for the
        /// view-index mode. Returns null when there is no integer. Callers clamp to their range.
        /// </summary>
        public static int? ParseIndex(string response)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return null;
            }

            Match match = Regex.Match(response, @"-?\d+");
            return match.Success && int.TryParse(match.Value, out int index) ? index : null;
        }
    }
}
