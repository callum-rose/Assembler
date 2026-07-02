using System;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.AssetGeneration.ImageOrientation
{
    /// <summary>The parsed outcome of an orientation request, keeping the raw model text for display.</summary>
    public sealed record OrientationResult(FacingDirection? Direction, string RawResponse)
    {
        public string Code => Direction is { } direction ? direction.ToCode() : "(unrecognised)";
    }

    /// <summary>
    /// Asks Claude which direction the front of the main object in an image faces,
    /// constraining the reply to one of the eight <see cref="FacingDirection"/> codes.
    /// </summary>
    public sealed class OrientationClassifier
    {
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

        private readonly AnthropicClient _client;

        public OrientationClassifier(AnthropicClient client) => _client = client;

        public async Task<OrientationResult> ClassifyAsync(AnthropicImage image, CancellationToken cancellationToken)
        {
            if (image.IsEmpty)
            {
                throw new ArgumentException("An image is required.", nameof(image));
            }

            var message = new AnthropicMessage("user", Instruction, new[] { image });
            var response = await _client.SendAsync(SystemPrompt, new[] { message }, cancellationToken);
            return new OrientationResult(FacingDirectionExtensions.Parse(response), response.Trim());
        }
    }
}
