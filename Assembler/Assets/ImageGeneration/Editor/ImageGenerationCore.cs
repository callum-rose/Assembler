#nullable enable

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Assembler.ImageGeneration
{
    /// <summary>
    /// UI-free core of the text → image spike: generate an image from a prompt and write it to disk.
    /// Shared by the editor window (<see cref="ImageGenerationWindow"/>) and any headless / pipeline
    /// caller, so both drive an identical path. No EditorPrefs, preview textures, or window state lives
    /// here — callers supply an optional status sink and read the output path off the returned
    /// <see cref="Result"/>. Mirrors <c>VoxConversion</c> so the three spikes can be chained:
    /// this stage's <see cref="Result.OutputPath"/> is the next stage's input image.
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
        /// <returns>The written path plus the generated image, for chaining into the next stage.</returns>
        /// <exception cref="ImageGenerationException">The prompt or output directory is empty.</exception>
        /// <exception cref="OperationCanceledException"><paramref name="ct"/> was cancelled.</exception>
        public static async Task<Result> GenerateAsync(
            ImageProvider provider,
            string apiKey,
            string model,
            string prompt,
            string outputDir,
            string outputFile,
            CancellationToken ct = default,
            Action<string>? onStatus = null)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ImageGenerationException("Enter a prompt.");
            if (string.IsNullOrWhiteSpace(outputDir))
                throw new ImageGenerationException("Set an output directory.");

            using var generator = ImageGeneratorFactory.Create(provider, apiKey);

            onStatus?.Invoke($"Generating with {generator.DisplayName}…");
            var image = await generator.GenerateAsync(new ImageGenerationRequest(prompt, model), ct);

            // No filename given → fall back to a default base name; the extension is always
            // derived from the returned image's MIME type.
            var fileName = string.IsNullOrWhiteSpace(outputFile) ? "image" : outputFile.Trim();
            var path = EnsureExtension(Path.Combine(outputDir, fileName), image.MimeType);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            File.WriteAllBytes(path, image.Bytes);

            onStatus?.Invoke($"Done ({image.Bytes.Length / 1024} KB). Saved to {path}");

            // Surface a freshly-written image to the project view when it lands inside Assets/.
            if (IsUnderAssets(path))
                AssetDatabase.Refresh();

            return new Result(path, image);
        }

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

        public static bool IsUnderAssets(string path)
        {
            var full = Path.GetFullPath(path);
            var assets = Path.GetFullPath(Application.dataPath);
            return full.StartsWith(assets, StringComparison.OrdinalIgnoreCase);
        }
    }
}
