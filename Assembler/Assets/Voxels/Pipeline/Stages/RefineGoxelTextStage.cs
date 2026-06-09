using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Voxels.Pipeline.Stages
{
	/// <summary>
	/// Refines the current model. Reads <c>GoxelTextZUp</c> + <c>RefinementInstruction</c>,
	/// optionally <c>ChatHistory</c> when <c>UseChatHistory</c> is true. Writes
	/// the new <c>GoxelTextZUp</c> (Z-up, via internally chained
	/// <see cref="ExtractGoxelBlockStage"/> + <see cref="SwapYZAxesStage"/>)
	/// and stores <c>RawAssistantText</c>; appends the user+assistant pair to
	/// <c>ChatHistory</c> when chat mode is on. The internal pre-swap of the
	/// current model into Y-up for Claude is a transient transformation and
	/// does not touch the context.
	/// </summary>
	public sealed class RefineGoxelTextStage : IVoxelStage
	{
		public string Name => "RefineGoxelText";

		public async Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			if (ctx.AnthropicClient == null)
			{
				throw new InvalidOperationException($"{Name}: AnthropicClient is required.");
			}

			if (string.IsNullOrWhiteSpace(ctx.SystemPrompt))
			{
				throw new InvalidOperationException($"{Name}: SystemPrompt is required.");
			}

			if (string.IsNullOrWhiteSpace(ctx.GoxelTextZUp))
			{
				throw new InvalidOperationException($"{Name}: GoxelTextZUp is required (nothing to refine).");
			}

			if (string.IsNullOrWhiteSpace(ctx.RefinementInstruction))
			{
				throw new InvalidOperationException($"{Name}: RefinementInstruction is required.");
			}

			var currentYUp = GoxelCoordinateConverter.SwapYAndZ(ctx.GoxelTextZUp!);
			var userMessage = GoxelGenerationCore.BuildRefinementMessage(currentYUp, ctx.RefinementInstruction!);

			List<AnthropicMessage> messages = ctx.UseChatHistory
				? new List<AnthropicMessage>(ctx.ChatHistory) { new("user", userMessage) }
				: new List<AnthropicMessage> { new("user", userMessage) };

			var sent = await GoxelGenerationCore.SendAndExtractAsync(ctx, messages, ct).ConfigureAwait(false);
			var withRaw = ctx with { RawAssistantText = sent.RawAssistantText };

			// Script path: Claude rebuilt the model in code. Use it directly (Z-up).
			if (sent.ScriptUsed)
			{
				var assistantTurn = "I rebuilt the model with a procedural script:\n\n```csharp\n" + sent.LastScript + "\n```";
				var scriptHistory = ctx.UseChatHistory
					? ctx.ChatHistory
						.Add(new AnthropicMessage("user", userMessage))
						.Add(new AnthropicMessage("assistant", assistantTurn))
					: ctx.ChatHistory;

				return withRaw with
				{
					GoxelTextZUp = sent.GoxelTextZUp,
					LastScript = sent.LastScript,
					ChatHistory = scriptHistory,
				};
			}

			// Mentalised path: Claude returned a direct goxel block (Y-up, swapped).
			var nextHistory = ctx.UseChatHistory
				? ctx.ChatHistory
					.Add(new AnthropicMessage("user", userMessage))
					.Add(new AnthropicMessage("assistant", sent.RawAssistantText))
				: ctx.ChatHistory;

			return withRaw with { GoxelTextZUp = sent.GoxelTextZUp, ChatHistory = nextHistory, LastScript = null };
		}
	}
}
