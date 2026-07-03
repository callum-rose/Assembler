using System;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.AssetGeneration.ImageOrientation
{
    /// <summary>The parsed outcome of an orientation request, keeping the raw model text for display/logging.</summary>
    public sealed record OrientationResult(FacingDirection? Direction, OrientationOutcome Outcome, string RawResponse)
    {
        public string Code => Outcome switch
        {
            OrientationOutcome.Resolved when Direction is { } direction => direction.ToCode(),
            OrientationOutcome.Unsure => "?",
            _ => "(unrecognised)",
        };
    }

    /// <summary>
    /// Headless core logic: asks Claude which direction the front of the main object
    /// in an image is facing, constrained to the eight <see cref="FacingDirection"/>
    /// codes (L, R, U, D, LU, LD, RU, RD) or an explicit "unsure" answer when it cannot
    /// tell (<see cref="OrientationOutcome.Unsure"/>). Every input arrives as an argument and the
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
            "If you genuinely cannot tell which way the front faces — e.g. the object has no clear front, " +
            "faces straight toward or away from the viewer, or the image is too ambiguous — reply UNSURE " +
            "instead of guessing.\n\n" +
            "Pick the single closest code. Respond with EXACTLY that code and nothing else — no punctuation, " +
            "no explanation. Your entire reply must be one of: L, R, U, D, LU, LD, RU, RD, UNSURE.";

        private const string Instruction =
            "Which direction is the front of the main object in this image facing? " +
            "Answer with one code only, or UNSURE if you cannot tell.";

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
            var (direction, outcome) = FacingDirectionExtensions.Classify(response);
            return new OrientationResult(direction, outcome, response.Trim());
        }
    }
}
