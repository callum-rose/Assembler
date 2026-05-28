using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Voxels.Pipeline.Stages
{
	/// <summary>
	/// Sends <c>UserPrompt</c> + <c>SystemPrompt</c> to Claude, writes the raw
	/// reply to <c>RawAssistantText</c>, extracts the fenced block, and swaps
	/// Y↔Z to produce <c>GoxelTextZUp</c>. Internally composes
	/// <see cref="ExtractGoxelBlockStage"/> + <see cref="SwapYZAxesStage"/>.
	/// Also seeds <c>ChatHistory</c> with the round so later refines can
	/// continue the conversation.
	/// </summary>
	public sealed class GenerateGoxelTextStage : IVoxelStage
	{
		public string Name => "GenerateGoxelText";

		public async Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			if (ctx.AnthropicClient == null)
			{
				throw new InvalidOperationException($"{Name}: AnthropicClient is required (call .WithAnthropic(...))");
			}

			if (string.IsNullOrWhiteSpace(ctx.UserPrompt))
			{
				throw new InvalidOperationException($"{Name}: UserPrompt is required (call .WithPrompt(...))");
			}

			if (string.IsNullOrWhiteSpace(ctx.SystemPrompt))
			{
				throw new InvalidOperationException($"{Name}: SystemPrompt is required (LoadSystemPromptStage was not run).");
			}

			var messages = new List<AnthropicMessage> { new("user", ctx.UserPrompt!) };
			var raw = await ctx.AnthropicClient.SendAsync(ctx.SystemPrompt!, messages, ct, ctx.Observer.OnStreamDelta).ConfigureAwait(false);

			var withRaw = ctx with { RawAssistantText = raw };
			var extracted = await new ExtractGoxelBlockStage().ExecuteAsync(withRaw, ct).ConfigureAwait(false);
			var swapped = await new SwapYZAxesStage().ExecuteAsync(extracted, ct).ConfigureAwait(false);

			// Generate is a fresh start: reset chat history and seed it with
			// this one turn. Store the raw reply (Y-up, fenced) — that's what
			// Claude actually said and what a follow-up chat-refine should
			// replay verbatim.
			var newHistory = System.Collections.Immutable.ImmutableList.Create(
				new AnthropicMessage("user", ctx.UserPrompt!),
				new AnthropicMessage("assistant", raw));

			return swapped with { ChatHistory = newHistory };
		}
	}
}
