using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;
using Assembler.Voxels.Generation;
using UnityEngine;

namespace Assembler.Voxels.Pipeline.Stages
{
	/// <summary>
	/// Like <see cref="GenerateGoxelTextStage"/>, but anchors the first generation
	/// to a reference image: the chosen <c>ReferenceImages</c> entry is attached to
	/// the user turn and Claude is told to match its proportions, silhouette and
	/// colours. A palette quantised from the image is passed as suggested colours
	/// (attacks the "flat colours" failure mode).
	///
	/// With no reference image present this delegates verbatim to
	/// <see cref="GenerateGoxelTextStage"/>, so the image-free path is unchanged.
	/// </summary>
	public sealed class ReferenceGuidedGenerateStage : IVoxelStage
	{
		private const int PaletteColors = 12;

		public string Name => "ReferenceGuidedGenerate";

		public async Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			if (ctx.ReferenceImages is not { Count: > 0 })
			{
				return await new GenerateGoxelTextStage().ExecuteAsync(ctx, ct).ConfigureAwait(false);
			}

			if (ctx.AnthropicClient == null)
			{
				throw new InvalidOperationException($"{Name}: AnthropicClient is required (call .WithAnthropic(...)).");
			}

			if (string.IsNullOrWhiteSpace(ctx.UserPrompt))
			{
				throw new InvalidOperationException($"{Name}: UserPrompt is required (call .WithPrompt(...)).");
			}

			if (string.IsNullOrWhiteSpace(ctx.SystemPrompt))
			{
				throw new InvalidOperationException($"{Name}: SystemPrompt is required (LoadSystemPromptStage was not run).");
			}

			// First entry is the chosen reference (the editor reorders the picked
			// variation to the front; default auto-pick is index 0).
			var chosen = ctx.ReferenceImages![0];

			// Palette seeding — decode + quantise on the main thread (Texture2D).
			var palette = Array.Empty<Color32>();
			await ctx.MainThread.RunAsync(() => palette = ImagePalette.Extract(chosen, PaletteColors)).ConfigureAwait(false);

			var instruction = BuildGuidedInstruction(ctx.UserPrompt!, palette);
			var messages = new List<AnthropicMessage>
			{
				new("user", instruction, new[] { new AnthropicImage(chosen) }),
			};

			var sent = await GoxelGenerationCore.SendAndExtractAsync(ctx, messages, ct).ConfigureAwait(false);
			var withRaw = ctx with { RawAssistantText = sent.RawAssistantText };

			if (sent.ScriptUsed)
			{
				var assistantTurn = "I built this model with a procedural script:\n\n```csharp\n" + sent.LastScript + "\n```";
				var scriptHistory = ImmutableList.Create(
					new AnthropicMessage("user", instruction),
					new AnthropicMessage("assistant", assistantTurn));

				return withRaw with
				{
					GoxelTextZUp = sent.GoxelTextZUp,
					LastScript = sent.LastScript,
					ChatHistory = scriptHistory,
				};
			}

			var newHistory = ImmutableList.Create(
				new AnthropicMessage("user", instruction),
				new AnthropicMessage("assistant", sent.RawAssistantText));

			return withRaw with { GoxelTextZUp = sent.GoxelTextZUp, ChatHistory = newHistory, LastScript = null };
		}

		private static string BuildGuidedInstruction(string subject, IReadOnlyList<Color32> palette)
		{
			var sb = new StringBuilder();
			sb.AppendLine("Build a voxel model of: " + subject);
			sb.AppendLine();
			sb.AppendLine("The attached image is a reference for this subject. Match its proportions, " +
						  "silhouette, and colour scheme — treat it as the target the voxel model should " +
						  "read as from the same 3/4 view.");
			sb.AppendLine("- Prefer the procedural script tool; use mirror symmetry where the subject is symmetric.");
			sb.AppendLine("- Keep the model connected (no floating voxels) and centred.");
			if (palette.Count > 0)
			{
				sb.AppendLine("- Suggested palette sampled from the reference (RRGGBB), dominant first: "
							  + PaletteQuantizer.ToHexList(palette) + ". Reuse these where they fit.");
			}

			return sb.ToString();
		}
	}
}
