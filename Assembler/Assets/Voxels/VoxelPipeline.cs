using System;
using System.Collections.Generic;
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

		public VoxelPipeline(string? systemPromptOverride = null)
		{
			_systemPrompt = systemPromptOverride ?? VoxelPromptBuilder.Build();
		}

		public async Task<string> GenerateGoxelTextAsync(
			string prompt,
			AnthropicClient client,
			CancellationToken cancellationToken)
		{
			var messages = new List<AnthropicMessage> { new("user", prompt) };
			var raw = await client.SendAsync(_systemPrompt, messages, cancellationToken).ConfigureAwait(false);

			var extracted = VoxelResponseExtractor.Extract(raw);
			if (string.IsNullOrWhiteSpace(extracted))
			{
				throw new InvalidOperationException(
					"Claude reply did not contain a ```goxel``` fenced block. Raw reply:\n" + raw);
			}

			return extracted!;
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
