using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Voxels.Pipeline.Stages
{
	/// <summary>
	/// Shared send/extract tail for every generation stage (plain, reference-
	/// guided, refine, vision-critique). Builds the tool list from the optional
	/// script executor, streams the reply, and resolves <c>GoxelTextZUp</c> from
	/// either the executor's procedural output (already Z-up) or an extracted
	/// <c>```goxel```</c> block (Y-up, swapped to Z-up here). History building is
	/// left to each stage since it differs (generate resets; refine appends).
	/// </summary>
	internal static class GoxelGenerationCore
	{
		public readonly struct SendResult
		{
			public SendResult(string rawAssistantText, string goxelTextZUp, string? lastScript, bool scriptUsed)
			{
				RawAssistantText = rawAssistantText;
				GoxelTextZUp = goxelTextZUp;
				LastScript = lastScript;
				ScriptUsed = scriptUsed;
			}

			public string RawAssistantText { get; }
			public string GoxelTextZUp { get; }
			public string? LastScript { get; }
			public bool ScriptUsed { get; }
		}

		public static async Task<SendResult> SendAndExtractAsync(
			VoxelPipelineContext ctx, IReadOnlyList<AnthropicMessage> messages, CancellationToken ct)
		{
			var executor = ctx.ScriptExecutor;
			IReadOnlyList<AnthropicTool>? tools = executor != null ? new[] { executor.Tool } : null;
			Func<AnthropicToolUse, CancellationToken, Task<AnthropicToolResult>>? onToolUse =
				executor != null ? executor.HandleToolUseAsync : null;

			var raw = await ctx.AnthropicClient!
				.SendAsync(ctx.SystemPrompt!, messages, ct, ctx.Observer.OnStreamDelta, tools, onToolUse, ctx.Limits.MaxToolIterations)
				.ConfigureAwait(false);

			// Procedural path: a model was built in code. Use it directly (Z-up).
			if (executor != null && executor.LastGoxelTextZUp is { Length: > 0 } scriptText)
			{
				return new SendResult(raw, scriptText, executor.LastScript, scriptUsed: true);
			}

			// Mentalised path: Claude wrote a direct ```goxel``` block (Y-up).
			var extracted = VoxelResponseExtractor.Extract(raw);
			if (string.IsNullOrWhiteSpace(extracted))
			{
				throw new InvalidOperationException(
					"Claude reply did not contain a ```goxel``` fenced block. Raw reply:\n" + raw);
			}

			var zUp = GoxelCoordinateConverter.SwapYAndZ(extracted!);
			return new SendResult(raw, zUp, null, scriptUsed: false);
		}

		/// <summary>
		/// Builds a refinement user message wrapping the current model (Y-up, the
		/// convention Claude reads/writes) in a <c>&lt;current_model&gt;</c> block
		/// followed by a <c>Change:</c> instruction. Shared by the text-refine and
		/// vision-critique stages.
		/// </summary>
		public static string BuildRefinementMessage(string currentGoxelTextYUp, string instruction)
		{
			var sb = new StringBuilder();
			sb.AppendLine("Here is the current model:");
			sb.AppendLine();
			sb.AppendLine("<current_model>");
			sb.Append(currentGoxelTextYUp);
			if (!currentGoxelTextYUp.EndsWith("\n", StringComparison.Ordinal))
			{
				sb.AppendLine();
			}

			sb.AppendLine("</current_model>");
			sb.AppendLine();
			sb.Append("Change: ").Append(instruction);
			return sb.ToString();
		}
	}
}
