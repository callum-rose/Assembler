using System;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Voxels.Generation;
using UnityEngine;

namespace Assembler.Voxels.Pipeline.Stages
{
	/// <summary>
	/// Asks the configured <see cref="IImageGenerator"/> for one or more 2D
	/// voxel-art reference images of the subject and stores the PNG bytes on
	/// <c>ReferenceImages</c>. The subject prompt is wrapped with
	/// <see cref="ReferenceImageStyle"/> first.
	///
	/// Degrades gracefully: with no provider (or an empty result) the stage is a
	/// no-op and downstream generation falls back to plain text — so the
	/// existing, image-free path is unaffected.
	/// </summary>
	public sealed class GenerateReferenceImageStage : IVoxelStage
	{
		public string Name => "GenerateReferenceImage";

		public async Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			if (ctx.ImageGenerator == null || ctx.ImageGenerator is NullImageGenerator)
			{
				ctx.Observer.OnLog("No image generator configured — proceeding without a reference image.");
				return ctx;
			}

			if (string.IsNullOrWhiteSpace(ctx.UserPrompt))
			{
				throw new InvalidOperationException($"{Name}: UserPrompt is required (call .WithPrompt(...)).");
			}

			var styled = ReferenceImageStyle.Wrap(ctx.UserPrompt!);
			var variations = Mathf.Max(1, ctx.ReferenceVariations);

			var images = await ctx.ImageGenerator.GenerateAsync(styled, variations, ct).ConfigureAwait(false);
			if (images == null || images.Count == 0)
			{
				ctx.Observer.OnLog("Image generator returned no images — proceeding without a reference image.");
				return ctx;
			}

			ctx.Observer.OnLog($"Generated {images.Count} reference image(s).");
			return ctx with { ReferenceImages = images };
		}
	}
}
