using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Stage 4: turns an operator note into a minimal set of edit operations over
	/// an already-accepted model, instead of re-planning from scratch. Follows the
	/// <see cref="ModelPlanner"/> shape — one call, retry-with-feedback up to
	/// <see cref="MaxAttempts"/> — and dry-runs <see cref="ModelEdits.Apply"/> so a
	/// referential mistake (unknown part/key, a remap on a script part) burns a
	/// retry rather than failing the whole refine. A lone <c>replan</c> op is the
	/// escape hatch and is returned verbatim for the orchestrator to act on.
	/// </summary>
	public sealed class ModelRefiner
	{
		public const string Stage = "4-refine";

		/// <summary>Total refine calls: the first proposal plus feedback rounds for parse/apply failures.</summary>
		public const int MaxAttempts = 3;

		private readonly IAnthropicGateway _gateway;
		private readonly VoxelizationConfig _config;

		public ModelRefiner(IAnthropicGateway gateway, VoxelizationConfig config)
		{
			_gateway = gateway;
			_config = config;
		}

		public async Task<IReadOnlyList<ModelEditOp>> ProposeAsync(
			VoxelRigModel model,
			ReferenceBrief brief,
			string note,
			CancellationToken ct,
			IProgress<string>? progress = null)
		{
			var messages = new List<AnthropicMessage>
			{
				new("user", VoxelizationPrompts.RefineUser(model, brief, note, _config.StyleGuidance)),
			};

			for (var attempt = 1; ; attempt++)
			{
				var response = await _gateway.SendAsync(
					Stage, _config.PlanningModel, VoxelizationPrompts.RefineSystem, messages, ct).ConfigureAwait(false);

				var (ops, feedback) = TryParse(response, model, brief);
				if (ops != null)
				{
					return ops;
				}

				// Why this proposal was bounced (no edits block, unparseable, or a
				// dry-run apply failure) so a refine that gives up still says what went
				// wrong rather than ending on a bare "failed".
				progress?.Report(
					$"{model.Id}: refine attempt {attempt}/{MaxAttempts} rejected — {feedback}");

				if (attempt >= MaxAttempts)
				{
					throw new VoxelizationException($"Refining '{model.Id}' failed after {MaxAttempts} attempts: {feedback}");
				}

				messages.Add(new AnthropicMessage("assistant", response));
				messages.Add(new AnthropicMessage("user", feedback));
			}
		}

		private static (IReadOnlyList<ModelEditOp>? Ops, string Feedback) TryParse(
			string response, VoxelRigModel model, ReferenceBrief brief)
		{
			var block = FencedBlockExtractor.Extract(response, "edits");
			if (block == null)
			{
				return (null, "Your response contained no ```edits fenced block. Emit exactly one ```edits block.");
			}

			IReadOnlyList<ModelEditOp> ops;
			try
			{
				ops = ModelEdits.Parse(block);
			}
			catch (FormatException ex)
			{
				return (null, $"Those edits could not be parsed: {ex.Message}\nEmit the corrected ```edits block.");
			}

			// A replan op is a signal, not a mutation — accept it without a dry run.
			if (ops.Any(o => o is ReplanOp))
			{
				return (ops, string.Empty);
			}

			try
			{
				ModelEdits.Apply(model, brief, ops);
			}
			catch (FormatException ex)
			{
				return (null, $"Those edits don't apply cleanly: {ex.Message}\nEmit the corrected ```edits block.");
			}

			return (ops, string.Empty);
		}
	}
}
