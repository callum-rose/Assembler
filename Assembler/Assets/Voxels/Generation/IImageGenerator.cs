using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Assembler.Voxels.Generation
{
	/// <summary>
	/// Text-to-image provider — the only external piece of the reference-guided
	/// voxel pipeline. Returns one PNG byte array per variation. Kept
	/// provider-agnostic so the hosting decision (cloud API vs. local model) is
	/// made by swapping the implementation only; nothing else in the pipeline
	/// changes.
	/// </summary>
	public interface IImageGenerator
	{
		Task<IReadOnlyList<byte[]>> GenerateAsync(string prompt, int variations, CancellationToken ct);
	}

	/// <summary>
	/// Placeholder provider used until a real text-to-image host is wired. Returns
	/// no images, so the reference stages degrade gracefully to plain text
	/// generation (the existing, unchanged path).
	/// </summary>
	public sealed class NullImageGenerator : IImageGenerator
	{
		public static readonly NullImageGenerator Instance = new();

		public Task<IReadOnlyList<byte[]>> GenerateAsync(string prompt, int variations, CancellationToken ct)
			=> Task.FromResult<IReadOnlyList<byte[]>>(System.Array.Empty<byte[]>());
	}
}
