using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Resolves a manifest asset's <c>reference</c> string to image bytes
	/// (Decision 8). Implementations cover files on disk, in-memory bytes, and
	/// later an image-generation API. Returns <see cref="AnthropicImage.None"/>
	/// when the reference cannot be resolved.
	/// </summary>
	public interface IReferenceImageSource
	{
		Task<AnthropicImage> LoadAsync(string reference, CancellationToken ct);
	}

	/// <summary>Resolves references as paths relative to a base directory (absolute paths pass through).</summary>
	public sealed class FileReferenceImageSource : IReferenceImageSource
	{
		private readonly string _baseDirectory;

		public FileReferenceImageSource(string baseDirectory) => _baseDirectory = baseDirectory;

		public Task<AnthropicImage> LoadAsync(string reference, CancellationToken ct)
		{
			var path = Path.IsPathRooted(reference) ? reference : Path.Combine(_baseDirectory, reference);
			if (!File.Exists(path))
			{
				return Task.FromResult(AnthropicImage.None);
			}

			var bytes = File.ReadAllBytes(path);
			var mediaType = AnthropicImage.MediaTypeFromExtension(Path.GetExtension(path));
			return Task.FromResult(new AnthropicImage(mediaType, bytes));
		}
	}

	/// <summary>In-memory references, for tests and programmatic callers.</summary>
	public sealed class BytesReferenceImageSource : IReferenceImageSource
	{
		private readonly IReadOnlyDictionary<string, AnthropicImage> _images;

		public BytesReferenceImageSource(IReadOnlyDictionary<string, AnthropicImage> images) => _images = images;

		public Task<AnthropicImage> LoadAsync(string reference, CancellationToken ct) =>
			Task.FromResult(_images.TryGetValue(reference, out var image) ? image : AnthropicImage.None);
	}

	/// <summary>Null object: every reference resolves to no image.</summary>
	public sealed class NullReferenceImageSource : IReferenceImageSource
	{
		public static readonly NullReferenceImageSource Instance = new();

		public Task<AnthropicImage> LoadAsync(string reference, CancellationToken ct) =>
			Task.FromResult(AnthropicImage.None);
	}
}
