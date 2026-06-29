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
        OpenAi,
        BlackForestLabs,
        Recraft,
    }

    /// <summary>
    /// Get-only properties assigned in the constructor (not <c>init</c>) because
    /// editor-only asmdefs in this project lack the <c>IsExternalInit</c> polyfill.
    /// </summary>
    public readonly struct ImageGenerationRequest
    {
        public ImageGenerationRequest(string prompt, string model, ReferenceImage? referenceImage = null)
        {
            Prompt = prompt;
            Model = model;
            ReferenceImage = referenceImage;
        }

        public string Prompt { get; }

        /// <summary>Provider-specific model id; empty means "use the provider default".</summary>
        public string Model { get; }

        /// <summary>
        /// Optional image to condition generation on (style reference / image edit).
        /// Null for a pure text-to-image request. Providers that can take an input image
        /// (Gemini's <c>generateContent</c>, OpenAI edits, …) should honour it; a provider
        /// with no image-input support may ignore it.
        /// </summary>
        public ReferenceImage? ReferenceImage { get; }
    }

    /// <summary>Bytes + MIME type of an image handed *into* a generator as a reference.</summary>
    public readonly struct ReferenceImage
    {
        public ReferenceImage(byte[] bytes, string mimeType)
        {
            Bytes = bytes;
            MimeType = mimeType;
        }

        public byte[] Bytes { get; }
        public string MimeType { get; }
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
                ImageProvider.OpenAi => new OpenAiImageGenerator(apiKey),
                ImageProvider.BlackForestLabs => new FluxImageGenerator(apiKey),
                ImageProvider.Recraft => new RecraftImageGenerator(apiKey),
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
                    "gemini-3-pro-image",       // Nano Banana Pro — highest quality, paid
                    "gemini-3.1-flash-image",   // Nano Banana 2 — high-efficiency counterpart
                    "gemini-2.5-flash-image",   // original Nano Banana — fast, free tier
                    "gemini-2.5-flash-image-preview",
                    "gemini-2.0-flash-preview-image-generation",
                },
                ImageProvider.OpenAi => new[]
                {
                    "gpt-image-2",
                    "gpt-image-1.5",
                    "gpt-image-1",
                    "gpt-image-1-mini",
                },
                ImageProvider.BlackForestLabs => new[]
                {
                    "flux-2-pro",
                    "flux-2-flex",
                    "flux-2-dev",
                    "flux-kontext-pro",  // editing/style-reference specialist
                    "flux-kontext-max",
                },
                ImageProvider.Recraft => new[]
                {
                    "recraftv4",
                    "recraftv3",
                },
                _ => Array.Empty<string>(),
            };
    }

    public sealed class ImageGenerationException : Exception
    {
        public ImageGenerationException(string message) : base(message) { }
    }
}
