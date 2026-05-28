using System;
using System.Threading;
using System.Threading.Tasks;

namespace Assembler.Voxels.Pipeline.Stages
{
	/// <summary>
	/// Loads the canonical voxel system prompt from Resources and concatenates
	/// any persistent instructions. Output: <c>SystemPrompt</c>.
	/// </summary>
	public sealed class LoadSystemPromptStage : IVoxelStage
	{
		public string Name => "LoadSystemPrompt";

		public Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			var basePrompt = VoxelPromptBuilder.Build();
			var systemPrompt = string.IsNullOrWhiteSpace(ctx.PersistentInstructions)
				? basePrompt
				: basePrompt + "\n\n# Additional persistent instructions\n\n" + ctx.PersistentInstructions;
			return Task.FromResult(ctx with { SystemPrompt = systemPrompt });
		}
	}

	/// <summary>
	/// Extracts the ```goxel``` fenced block from <c>RawAssistantText</c> and
	/// applies the Y→Z swap to produce <c>GoxelTextZUp</c>.
	/// </summary>
	public sealed class ExtractGoxelBlockStage : IVoxelStage
	{
		public string Name => "ExtractGoxelBlock";

		public Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			if (ctx.RawAssistantText == null)
			{
				throw new InvalidOperationException($"{Name}: RawAssistantText is required.");
			}

			var extracted = VoxelResponseExtractor.Extract(ctx.RawAssistantText);
			if (string.IsNullOrWhiteSpace(extracted))
			{
				throw new InvalidOperationException(
					"Claude reply did not contain a ```goxel``` fenced block. Raw reply:\n" + ctx.RawAssistantText);
			}

			var swapped = GoxelCoordinateConverter.SwapYAndZ(extracted!);
			return Task.FromResult(ctx with { GoxelTextZUp = swapped });
		}
	}
}
