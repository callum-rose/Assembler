#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assembler.AssetGeneration.TextToImage.Editor
{
    /// <summary>
    /// Provider-agnostic image generator. Implement this once per backend
    /// (Gemini, OpenAI, Stability, a local model, …) and the editor window can
    /// drive any of them through the same call. Swapping providers is then a
    /// single new case in <see cref="ImageGeneratorFactory"/>.
    /// </summary>
    public interface IImageGenerator : IDisposable
    {
        /// <summary>Human-readable name shown in the editor status line.</summary>
        string DisplayName { get; }

        /// <summary>Generate one image from a text prompt.</summary>
        Task<GeneratedImage> GenerateAsync(ImageGenerationRequest request, CancellationToken ct);
    }

    /// <summary>The providers the module knows how to build. Add new backends here.</summary>
    public enum ImageProvider
    {
        GoogleGemini,
    }

    /// <summary>
    /// Get-only properties assigned in the constructor (not <c>init</c>) because
    /// editor-only asmdefs in this project lack the <c>IsExternalInit</c> polyfill.
    /// </summary>
    public readonly struct ImageGenerationRequest
    {
        public ImageGenerationRequest(string prompt, string model)
        {
            Prompt = prompt;
            Model = model;
        }

        public string Prompt { get; }

        /// <summary>Provider-specific model id; empty means "use the provider default".</summary>
        public string Model { get; }
    }

    /// <summary>Raw bytes plus the MIME type the provider returned them as.</summary>
    public sealed class GeneratedImage
    {
        public GeneratedImage(byte[] bytes, string mimeType)
        {
            Bytes = bytes;
            MimeType = mimeType;
        }

        public byte[] Bytes { get; }
        public string MimeType { get; }
    }

    /// <summary>Maps a provider choice to a concrete client.</summary>
    public static class ImageGeneratorFactory
    {
        public static IImageGenerator Create(ImageProvider provider, string apiKey) =>
            provider switch
            {
                ImageProvider.GoogleGemini => new GeminiImageGenerator(apiKey),
                _ => throw new ImageGenerationException($"Unsupported provider: {provider}"),
            };

        public static string DefaultModelFor(ImageProvider provider) =>
            AvailableModelsFor(provider) is { Length: > 0 } models ? models[0] : "";

        /// <summary>
        /// The model ids offered for a provider, newest/default first. Only models
        /// the concrete client can actually drive are listed — for Gemini that's the
        /// <c>generateContent</c> image family (the Imagen models use a different
        /// endpoint this module doesn't implement).
        /// </summary>
        public static string[] AvailableModelsFor(ImageProvider provider) =>
            provider switch
            {
                ImageProvider.GoogleGemini => new[]
                {
                    "gemini-2.5-flash-image",
                    "gemini-2.5-flash-image-preview",
                    "gemini-2.0-flash-preview-image-generation",
                },
                _ => Array.Empty<string>(),
            };
    }

    public sealed class ImageGenerationException : Exception
    {
        public ImageGenerationException(string message) : base(message) { }
    }
}
