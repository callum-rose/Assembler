using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Voxels.Pipeline.Stages
{
	/// <summary>
	/// Closes the gap between the rendered model and its target. Sends Claude the
	/// current model (as <c>&lt;current_model&gt;</c> text), the original reference
	/// image, and the fresh renders of the current model (3/4-front, side, top),
	/// then asks it to fix discrepancies — symmetry, floating voxels, proportions,
	/// silhouette, colour — and return the corrected model. Any
	/// <see cref="GeometryReport"/> from <see cref="ValidateGeometryStage"/> is
	/// folded into the prompt.
	///
	/// No-op (returns ctx unchanged) when there are no renders to critique, so the
	/// loop controller is safe to run even if rendering failed.
	/// </summary>
	public sealed class VisionCritiqueRefineStage : IVoxelStage
	{
		public string Name => "VisionCritiqueRefine";

		public async Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			// Nothing to critique → skip entirely (no client needed). Keeps the loop
			// controller safe when a render failed.
			if (ctx.RenderedImages is not { Count: > 0 })
			{
				ctx.Observer.OnLog("No rendered images to critique — skipping vision refine.");
				return ctx;
			}

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

			var images = new List<AnthropicImage>();
			var hasReference = ctx.ReferenceImages is { Count: > 0 };
			if (hasReference)
			{
				images.Add(new AnthropicImage(ctx.ReferenceImages![0]));
			}

			foreach (var render in ctx.RenderedImages!)
			{
				images.Add(new AnthropicImage(render));
			}

			var currentYUp = GoxelCoordinateConverter.SwapYAndZ(ctx.GoxelTextZUp!);
			var instruction = BuildInstruction(hasReference, ctx.RenderedImages!.Count, ctx.Geometry);
			var userMessage = GoxelGenerationCore.BuildRefinementMessage(currentYUp, instruction);

			var messages = new List<AnthropicMessage> { new("user", userMessage, images) };

			var sent = await GoxelGenerationCore.SendAndExtractAsync(ctx, messages, ct).ConfigureAwait(false);

			return ctx with
			{
				RawAssistantText = sent.RawAssistantText,
				GoxelTextZUp = sent.GoxelTextZUp,
				LastScript = sent.ScriptUsed ? sent.LastScript : null,
			};
		}

		private static string BuildInstruction(bool hasReference, int renderCount, GeometryReport? geometry)
		{
			var sb = new StringBuilder();
			if (hasReference)
			{
				sb.AppendLine("The first attached image is the TARGET reference. The remaining " + renderCount +
							  " image(s) are renders of your CURRENT model from roughly 3/4-front, side, and top.");
				sb.AppendLine("Compare the current renders against the target and correct the model so it reads " +
							  "like the reference from the same angles.");
			}
			else
			{
				sb.AppendLine("The attached " + renderCount + " image(s) are renders of your CURRENT model from " +
							  "roughly 3/4-front, side, and top. Study them and improve the model.");
			}

			sb.AppendLine("Check and fix: bilateral symmetry where expected, floating or disconnected voxels, " +
						  "proportions, overall silhouette, and colour choices.");
			if (geometry != null)
			{
				sb.AppendLine("Deterministic geometry check: " + geometry.Summarise() + ".");
				if (geometry.ComponentCount > 1)
				{
					sb.AppendLine("There are disconnected fragments — reattach them to the main body or remove them.");
				}

				if (geometry.SymmetryScore < 0.9f)
				{
					sb.AppendLine("Symmetry is poor — make the model bilaterally symmetric about its centre where the subject should be.");
				}
			}

			sb.Append("Output the full corrected model, not a diff.");
			return sb.ToString();
		}
	}
}
