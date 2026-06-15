using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Stage 1a seam: turns an asset's labelled reference images into a
	/// <see cref="ReferenceBrief"/>. The whole downstream pipeline consumes the
	/// brief and is indifferent to how it was produced, so the implementation can
	/// be the vision-model <see cref="BriefExtractor"/> or the pixel-deterministic
	/// <see cref="DeterministicBriefExtractor"/>.
	/// </summary>
	public interface IBriefExtractor
	{
		Task<ReferenceBrief> ExtractAsync(
			SetManifest manifest,
			ManifestAsset asset,
			IReadOnlyList<(ReferenceImage Label, AnthropicImage Image)> images,
			CancellationToken ct,
			IProgress<string>? progress = null);
	}
}
