using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;
using Assembler.Voxels.Scripting;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Stage 2: one text call per unique authored part. Layers parts come back
	/// as a fenced `layers` block (blank-line-separated slices) and are decoded
	/// immediately so malformed output retries inside the same call budget;
	/// script parts run through the run_voxel_script tool loop (Y-up) and the
	/// last successful script is kept. Mirrors never reach here — they are free.
	/// </summary>
	public sealed class PartAuthor
	{
		public const string Stage = "2-authoring";

		private readonly IAnthropicGateway _gateway;
		private readonly VoxelizationConfig _config;
		private readonly Func<IVoxelScriptExecutor> _executorFactory;

		public PartAuthor(IAnthropicGateway gateway, VoxelizationConfig config, Func<IVoxelScriptExecutor>? executorFactory = null)
		{
			_gateway = gateway;
			_config = config;
			_executorFactory = executorFactory
							   ?? (() => new VoxelScriptExecutor(config.ScriptLimits, VoxelizationPrompts.ScriptToolDescription));
		}

		public Task<PartData> AuthorAsync(
			VoxelRigModel model,
			ReferenceBrief brief,
			VoxelPart part,
			PlannedPartData planned,
			string feedback,
			CancellationToken ct,
			IProgress<string>? progress = null)
		{
			return planned.PlannedEncoding switch
			{
				PartEncoding.Script => AuthorScriptAsync(model, brief, part, planned, feedback, ct, progress),
				PartEncoding.Primitives => AuthorPrimitivesAsync(model, brief, part, planned, feedback, ct, progress),
				_ => AuthorLayersAsync(model, brief, part, planned, feedback, ct, progress),
			};
		}

		private async Task<PartData> AuthorLayersAsync(
			VoxelRigModel model,
			ReferenceBrief brief,
			VoxelPart part,
			PlannedPartData planned,
			string feedback,
			CancellationToken ct,
			IProgress<string>? progress)
		{
			var messages = new List<AnthropicMessage>
			{
				new("user", VoxelizationPrompts.PartUser(model, brief, part, planned, feedback, _config.StyleGuidance)),
			};

			for (var attempt = 1; ; attempt++)
			{
				var response = await _gateway.SendAsync(
					Stage, _config.AuthoringModel, VoxelizationPrompts.LayersSystem, messages, ct).ConfigureAwait(false);

				try
				{
					var block = FencedBlockExtractor.Extract(response, "layers")
								?? throw new FormatException("Response contained no ```layers fenced block.");
					var data = new LayersPartData(planned.Size, planned.Offset, SplitLayers(block, planned.Size));

					// Decode now so dimension/key errors surface here, where we can
					// feed them straight back, rather than later in assembly.
					LayersCodec.Decode(data, model.Palette);
					return data;
				}
				catch (FormatException ex) when (attempt < _config.MaxPartAttempts)
				{
					progress?.Report(
						$"{model.Id}/{part.Id}: authoring (layers) attempt {attempt}/{_config.MaxPartAttempts} rejected, retrying — {ex.Message}");
					messages.Add(new AnthropicMessage("assistant", response));
					messages.Add(new AnthropicMessage("user",
						$"Those layers are invalid: {ex.Message}\nEmit the corrected ```layers block only."));
				}
				catch (FormatException ex)
				{
					progress?.Report(
						$"{model.Id}/{part.Id}: authoring (layers) failed after {attempt} attempt(s) — {ex.Message}");
					throw new VoxelizationException($"Authoring layers for '{part.Id}' failed: {ex.Message}", ex);
				}
			}
		}

		private async Task<PartData> AuthorPrimitivesAsync(
			VoxelRigModel model,
			ReferenceBrief brief,
			VoxelPart part,
			PlannedPartData planned,
			string feedback,
			CancellationToken ct,
			IProgress<string>? progress)
		{
			var messages = new List<AnthropicMessage>
			{
				new("user", VoxelizationPrompts.PartUser(model, brief, part, planned, feedback, _config.StyleGuidance)),
			};

			for (var attempt = 1; ; attempt++)
			{
				var response = await _gateway.SendAsync(
					Stage, _config.AuthoringModel, VoxelizationPrompts.PrimitivesSystem, messages, ct).ConfigureAwait(false);

				try
				{
					var block = FencedBlockExtractor.Extract(response, "primitives")
								?? throw new FormatException("Response contained no ```primitives fenced block.");
					var data = new PrimitivesPartData(
						planned.Size,
						planned.Offset,
						block.Replace("\r", string.Empty).Split('\n').ToList());

					// Rasterize now so grammar/key errors surface here, where we can
					// feed them straight back, rather than later in assembly.
					PrimitivesCodec.Decode(data, model.Palette);
					return data;
				}
				catch (FormatException ex) when (attempt < _config.MaxPartAttempts)
				{
					progress?.Report(
						$"{model.Id}/{part.Id}: authoring (primitives) attempt {attempt}/{_config.MaxPartAttempts} rejected, retrying — {ex.Message}");
					messages.Add(new AnthropicMessage("assistant", response));
					messages.Add(new AnthropicMessage("user",
						$"Those shapes are invalid: {ex.Message}\nEmit the corrected ```primitives block only."));
				}
				catch (FormatException ex)
				{
					progress?.Report(
						$"{model.Id}/{part.Id}: authoring (primitives) failed after {attempt} attempt(s) — {ex.Message}");
					throw new VoxelizationException($"Authoring primitives for '{part.Id}' failed: {ex.Message}", ex);
				}
			}
		}

		private async Task<PartData> AuthorScriptAsync(
			VoxelRigModel model,
			ReferenceBrief brief,
			VoxelPart part,
			PlannedPartData planned,
			string feedback,
			CancellationToken ct,
			IProgress<string>? progress)
		{
			var messages = new List<AnthropicMessage>
			{
				new("user", VoxelizationPrompts.PartUser(model, brief, part, planned, feedback, _config.StyleGuidance)),
			};

			// Like the layers/primitives encoders, a script that never builds gets a
			// fresh attempt with feedback before the part is given up on — a single
			// failed tool loop is recoverable.
			for (var attempt = 1; ; attempt++)
			{
				var executor = _executorFactory();
				await _gateway.SendAsync(
					Stage,
					_config.AuthoringModel,
					VoxelizationPrompts.ScriptSystem,
					messages,
					ct,
					tools: new[] { executor.Tool },
					onToolUse: executor.HandleToolUseAsync,
					maxToolIterations: _config.ScriptLimits.MaxToolIterations).ConfigureAwait(false);

				var script = executor.LastScript;
				if (!string.IsNullOrWhiteSpace(script))
				{
					return new ScriptPartData(planned.Size, planned.Offset, script!);
				}

				if (attempt >= _config.MaxPartAttempts)
				{
					progress?.Report(
						$"{model.Id}/{part.Id}: authoring (script) failed after {attempt} attempt(s) — no run_voxel_script call built successfully.");
					throw new VoxelizationException(
						$"Authoring script for '{part.Id}' failed after {_config.MaxPartAttempts} attempts: no run_voxel_script call succeeded.");
				}

				progress?.Report(
					$"{model.Id}/{part.Id}: authoring (script) attempt {attempt}/{_config.MaxPartAttempts} produced no working script, retrying.");
				messages.Add(new AnthropicMessage("assistant", "(no run_voxel_script call built successfully)"));
				messages.Add(new AnthropicMessage("user",
					"Your script never built — call run_voxel_script with a corrected script that ends in `return b.Build();`."));
			}
		}

		/// <summary>
		/// Groups the fenced block's rows into layers. Blank lines are the
		/// intended separators, but models often misplace them (between every
		/// row, or not at all), so when the grouping doesn't yield size.y layers
		/// and the total row count is exactly size.y * size.z, the rows are
		/// re-chunked deterministically instead of failing the attempt.
		/// </summary>
		private static IReadOnlyList<string> SplitLayers(string block, Vector3Int size)
		{
			var rows = new List<string>();
			var layers = new List<string>();
			var current = new List<string>();
			foreach (var raw in block.Replace("\r", string.Empty).Split('\n'))
			{
				var line = raw.TrimEnd();
				if (line.Length == 0)
				{
					Flush();
				}
				else
				{
					current.Add(line);
					rows.Add(line);
				}
			}

			Flush();

			if (layers.Count != size.y && rows.Count == size.y * size.z)
			{
				return Enumerable.Range(0, size.y)
					.Select(y => string.Join("\n", rows.Skip(y * size.z).Take(size.z)))
					.ToList();
			}

			return layers;

			void Flush()
			{
				if (current.Count > 0)
				{
					layers.Add(string.Join("\n", current));
					current.Clear();
				}
			}
		}
	}
}
