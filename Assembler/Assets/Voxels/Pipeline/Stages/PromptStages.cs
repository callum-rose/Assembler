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
	/// Extracts the ```goxel``` fenced block from <c>RawAssistantText</c> into
	/// <c>GoxelTextZUp</c>. Does NOT transform coordinates — append a
	/// <see cref="SwapYZAxesStage"/> if the source was Y-up (as Claude
	/// produces) and you want Z-up storage.
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

			return Task.FromResult(ctx with { GoxelTextZUp = extracted });
		}
	}

	/// <summary>
	/// Swaps the Y and Z axes of every voxel line in <c>GoxelTextZUp</c>.
	/// Involutive: applying twice is a no-op. Use after extracting a Claude
	/// reply (Y-up) to land in Z-up storage form, or before sending the
	/// current model back to Claude for a refinement.
	/// </summary>
	public sealed class SwapYZAxesStage : IVoxelStage
	{
		public string Name => "SwapYZAxes";

		public Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			if (string.IsNullOrEmpty(ctx.GoxelTextZUp))
			{
				throw new InvalidOperationException($"{Name}: GoxelTextZUp is required.");
			}

			var swapped = GoxelCoordinateConverter.SwapYAndZ(ctx.GoxelTextZUp!);
			return Task.FromResult(ctx with { GoxelTextZUp = swapped });
		}
	}

	/// <summary>
	/// Rewrites <c>GoxelTextZUp</c> so that each (x, y, z) coordinate appears
	/// at most once — later occurrences overwrite earlier ones, mirroring the
	/// last-write-wins semantics that <see cref="GoxelTextParser"/> already
	/// uses internally. Comments and blank lines are preserved in place;
	/// duplicate-removed voxel lines drop out and the surviving line stays at
	/// the position of the LAST occurrence (so the final colour wins).
	/// </summary>
	public sealed class DedupeVoxelsStage : IVoxelStage
	{
		public string Name => "DedupeVoxels";

		public Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct)
		{
			if (ctx.GoxelTextZUp == null)
			{
				throw new InvalidOperationException($"{Name}: GoxelTextZUp is required.");
			}

			var deduped = DedupeText(ctx.GoxelTextZUp);
			return Task.FromResult(ctx with { GoxelTextZUp = deduped });
		}

		internal static string DedupeText(string text)
		{
			var lines = text.Split('\n');

			// First pass: for each voxel-line index, find the position key and
			// remember the LAST line index per key. Non-voxel lines (blank,
			// comment, malformed) are passed through unchanged.
			var lastSeenIndexForKey = new System.Collections.Generic.Dictionary<(int x, int y, int z), int>();
			var lineKeys = new (int x, int y, int z)?[lines.Length];
			for (var i = 0; i < lines.Length; i++)
			{
				if (TryParseVoxelKey(lines[i], out var key))
				{
					lineKeys[i] = key;
					lastSeenIndexForKey[key] = i;
				}
			}

			// Second pass: emit each line iff it's the final occurrence for its
			// key (or it's a non-voxel line).
			var sb = new System.Text.StringBuilder(text.Length);
			var firstEmitted = true;
			for (var i = 0; i < lines.Length; i++)
			{
				var key = lineKeys[i];
				if (key.HasValue && lastSeenIndexForKey[key.Value] != i)
				{
					continue; // earlier duplicate — drop.
				}

				if (!firstEmitted) sb.Append('\n');
				sb.Append(lines[i]);
				firstEmitted = false;
			}

			return sb.ToString();
		}

		private static bool TryParseVoxelKey(string line, out (int x, int y, int z) key)
		{
			key = default;
			var trimmed = line.TrimStart();
			if (trimmed.Length == 0 || trimmed[0] == '#') return false;

			var parts = trimmed.Split(new[] { ' ', '\t' }, 5, System.StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 4) return false;
			if (!int.TryParse(parts[0], out var x)) return false;
			if (!int.TryParse(parts[1], out var y)) return false;
			if (!int.TryParse(parts[2], out var z)) return false;

			key = (x, y, z);
			return true;
		}
	}
}
