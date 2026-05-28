using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Voxels
{
	/// <summary>
	/// Two-step façade: prompt -> Goxel text, and Goxel text -> .vox bytes.
	/// Callers that want a review gap call the steps separately; callers that
	/// don't can use <see cref="GenerateAndConvertAsync"/>.
	/// </summary>
	public sealed class VoxelPipeline
	{
		private readonly string _systemPrompt;

		public VoxelPipeline(string? systemPromptOverride = null, string? extraInstructions = null)
		{
			var basePrompt = systemPromptOverride ?? VoxelPromptBuilder.Build();
			_systemPrompt = string.IsNullOrWhiteSpace(extraInstructions)
				? basePrompt
				: basePrompt + "\n\n# Additional persistent instructions\n\n" + extraInstructions;
		}

		public async Task<string> GenerateGoxelTextAsync(
			string prompt,
			AnthropicClient client,
			CancellationToken cancellationToken,
			Action<string>? onDelta = null)
		{
			var messages = new List<AnthropicMessage> { new("user", prompt) };
			var raw = await client.SendAsync(_systemPrompt, messages, cancellationToken, onDelta).ConfigureAwait(false);

			var extracted = VoxelResponseExtractor.Extract(raw);
			if (string.IsNullOrWhiteSpace(extracted))
			{
				throw new InvalidOperationException(
					"Claude reply did not contain a ```goxel``` fenced block. Raw reply:\n" + raw);
			}

			return SwapYAndZ(extracted!);
		}

		// The prompt asks Claude to use Y as up, which matches Unity but not the
		// MagicaVoxel .vox format (Z-up). Rewrite each voxel line "x y z RRGGBB"
		// as "x z y RRGGBB" so the saved Goxel text is Z-up — meaning the .txt
		// also opens upright in the Goxel editor, where Z is up.
		private static string SwapYAndZ(string goxelText)
		{
			var sb = new StringBuilder(goxelText.Length);
			var lines = goxelText.Split('\n');
			for (var i = 0; i < lines.Length; i++)
			{
				var line = lines[i];
				var trimmed = line.TrimStart();
				if (trimmed.Length == 0 || trimmed[0] == '#')
				{
					sb.Append(line);
				}
				else
				{
					var parts = line.Split(new[] { ' ', '\t' }, 4, StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length >= 4
					    && int.TryParse(parts[0], out _)
					    && int.TryParse(parts[1], out _)
					    && int.TryParse(parts[2], out _))
					{
						sb.Append(parts[0]).Append(' ').Append(parts[2]).Append(' ').Append(parts[1]).Append(' ').Append(parts[3]);
					}
					else
					{
						sb.Append(line);
					}
				}
				if (i < lines.Length - 1) sb.Append('\n');
			}
			return sb.ToString();
		}

		public byte[] GoxelTextToVox(string goxelTxt)
		{
			var model = GoxelTextParser.Parse(goxelTxt);
			return VoxWriter.Write(model);
		}

		public async Task<(string goxelTxt, byte[] voxBytes)> GenerateAndConvertAsync(
			string prompt,
			AnthropicClient client,
			CancellationToken cancellationToken)
		{
			var goxelTxt = await GenerateGoxelTextAsync(prompt, client, cancellationToken).ConfigureAwait(false);
			var voxBytes = GoxelTextToVox(goxelTxt);
			return (goxelTxt, voxBytes);
		}
	}
}
