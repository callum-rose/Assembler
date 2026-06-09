using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Voxels.Pipeline.Stages
{
	/// <summary>
	/// Sends <c>UserPrompt</c> + <c>SystemPrompt</c> to Claude and produces
	/// <c>GoxelTextZUp</c>. When a <c>ScriptExecutor</c> is present the
	/// <c>run_voxel_script</c> tool is offered and Claude chooses per request:
	///
	/// - If Claude builds the model in code, the executor's serialised model is
	///   used directly as <c>GoxelTextZUp</c> (already Z-up — no axis swap) and
	///   the script is recorded in <c>LastScript</c>.
	/// - Otherwise Claude wrote a direct ```goxel``` block (Y-up), which is
	///   extracted and swapped to Z-up via the legacy
	///   <see cref="ExtractGoxelBlockStage"/> + <see cref="SwapYZAxesStage"/> path.
	///
	/// Also seeds <c>ChatHistory</c> with the round so later refines can continue
	/// the conversation.
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

			var sent = await GoxelGenerationCore.SendAndExtractAsync(ctx, messages, ct).ConfigureAwait(false);
			var withRaw = ctx with { RawAssistantText = sent.RawAssistantText };

			// Generate is a fresh start: reset chat history and seed it with this
			// one turn.
			if (sent.ScriptUsed)
			{
				var assistantTurn = "I built this model with a procedural script:\n\n```csharp\n" + sent.LastScript + "\n```";
				var scriptHistory = System.Collections.Immutable.ImmutableList.Create(
					new AnthropicMessage("user", ctx.UserPrompt!),
					new AnthropicMessage("assistant", assistantTurn));

				return withRaw with
				{
					GoxelTextZUp = sent.GoxelTextZUp,
					LastScript = sent.LastScript,
					ChatHistory = scriptHistory,
				};
			}

			// Store the raw reply (Y-up, fenced) — that's what Claude actually said
			// and what a follow-up chat-refine should replay verbatim.
			var newHistory = System.Collections.Immutable.ImmutableList.Create(
				new AnthropicMessage("user", ctx.UserPrompt!),
				new AnthropicMessage("assistant", sent.RawAssistantText));

			return withRaw with { GoxelTextZUp = sent.GoxelTextZUp, ChatHistory = newHistory, LastScript = null };
		}
	}
}
