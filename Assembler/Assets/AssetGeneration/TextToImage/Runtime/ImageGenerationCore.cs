#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Assembler.AssetGeneration.TextToImage
{
    /// <summary>
    /// UI-free, editor-free core of the text → image stage: generate an image from a prompt and
    /// write it to disk. Lives in a runtime assembly so it can be driven headlessly (batch mode, a
    /// CLI, a player build) as well as from the editor window. The method is pure with respect to
    /// editor and UI state — no EditorPrefs, no <c>AssetDatabase</c>, no window state; its only inputs
    /// are its arguments plus the remote provider, and its only outputs are the file it writes to the
    /// caller-supplied directory and the returned <see cref="Result"/>. Editor callers that want the
    /// freshly-written file surfaced in the Project view call <c>AssetDatabase.Refresh</c> themselves.
    /// Mirrors <c>VoxConversion</c> so the three stages can be chained: this stage's
    /// <see cref="Result.OutputPath"/> is the next stage's input image.
    /// </summary>
    public static class ImageGenerationCore
    {
        /// <summary>Result of a completed generation: the bytes/MIME returned and where they were written.</summary>
        public readonly struct Result
        {
            public Result(string outputPath, GeneratedImage image)
            {
                OutputPath = outputPath;
                Image = image;
            }

            /// <summary>Path the image was written to — feed this into the image-to-mesh stage.</summary>
            public string OutputPath { get; }

            /// <summary>The raw generated image (bytes + MIME type).</summary>
            public GeneratedImage Image { get; }

            public override string ToString() =>
                $"{Image.Bytes.Length / 1024} KB {Image.MimeType} → {OutputPath}";
        }

        /// <summary>
        /// Generate an image for <paramref name="prompt"/> with <paramref name="provider"/> /
        /// <paramref name="model"/> and write it into <paramref name="outputDir"/>. A blank
        /// <paramref name="outputFile"/> falls back to a default base name; the extension is always
        /// derived from the returned image's MIME type.
        /// </summary>
        /// <param name="onStatus">Optional sink for human-readable progress (UI status line / log).</param>
        /// <param name="referenceImagePath">Optional image to condition generation on (style reference /
        /// edit). Blank or null for a pure text-to-image request; the MIME type is inferred from the
        /// file extension.</param>
        /// <returns>The written path plus the generated image, for chaining into the next stage.</returns>
        /// <exception cref="ImageGenerationException">The prompt or output directory is empty, or the
        /// reference image path was given but doesn't exist.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
        public static async Task<Result> GenerateAsync(
            ImageProvider provider,
            string apiKey,
            string model,
            string prompt,
            string outputDir,
            string outputFile,
            CancellationToken ct = default,
            Action<string>? onStatus = null,
            string? referenceImagePath = null)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ImageGenerationException("Enter a prompt.");
            if (string.IsNullOrWhiteSpace(outputDir))
                throw new ImageGenerationException("Set an output directory.");

            var reference = LoadReferenceImage(referenceImagePath);

            using var generator = ImageGeneratorFactory.Create(provider, apiKey);

            onStatus?.Invoke(
                reference is null
                    ? $"Generating with {generator.DisplayName}…"
                    : $"Generating with {generator.DisplayName} (with reference image)…");
            var image = await generator.GenerateAsync(new ImageGenerationRequest(prompt, model, reference), ct);

            // No filename given → fall back to a default base name; the extension is always
            // derived from the returned image's MIME type.
            var fileName = string.IsNullOrWhiteSpace(outputFile) ? "image" : outputFile.Trim();
            var path = EnsureExtension(Path.Combine(outputDir, fileName), image.MimeType);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllBytes(path, image.Bytes);

            onStatus?.Invoke($"Done ({image.Bytes.Length / 1024} KB). Saved to {path}");

            return new Result(path, image);
        }

        private static ReferenceImage? LoadReferenceImage(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            if (!File.Exists(path))
                throw new ImageGenerationException($"Reference image not found: {path}");

            return new ReferenceImage(File.ReadAllBytes(path), MimeFromExtension(path));
        }

        private static string MimeFromExtension(string path) =>
            Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".webp" => "image/webp",
                _ => "image/png",
            };

        public static string EnsureExtension(string path, string mimeType)
        {
            if (!string.IsNullOrEmpty(Path.GetExtension(path)))
                return path;

            var ext = mimeType switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                "image/webp" => ".webp",
                _ => ".png",
            };
            return path + ext;
        }
    }
}
