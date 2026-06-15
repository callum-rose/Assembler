using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Stage 0: one text call per game turning a brief into the set manifest /
	/// scale bible. A parse failure is retried once with the error fed back.
	/// </summary>
	public sealed class ManifestGenerator
	{
		public const string Stage = "0-manifest";

		private readonly IAnthropicGateway _gateway;
		private readonly VoxelizationConfig _config;

		public ManifestGenerator(IAnthropicGateway gateway, VoxelizationConfig config)
		{
			_gateway = gateway;
			_config = config;
		}

		public async Task<SetManifest> GenerateAsync(string gameBrief, CancellationToken ct)
		{
			var messages = new List<AnthropicMessage>
			{
				new("user", VoxelizationPrompts.ManifestUser(gameBrief, _config.StyleGuidance)),
			};

			for (var attempt = 0; ; attempt++)
			{
				var response = await _gateway.SendAsync(
					Stage, _config.ManifestModel, VoxelizationPrompts.ManifestSystem, messages, ct).ConfigureAwait(false);

				try
				{
					var yaml = FencedBlockExtractor.Extract(response, "yaml")
							   ?? throw new FormatException("Response contained no ```yaml fenced block.");
					var manifest = ManifestYaml.Read(yaml);
					if (manifest.Assets.Count == 0)
					{
						throw new FormatException("Manifest contained no assets.");
					}

					return manifest;
				}
				catch (FormatException ex) when (attempt == 0)
				{
					messages.Add(new AnthropicMessage("assistant", response));
					messages.Add(new AnthropicMessage("user",
						$"That manifest could not be parsed: {ex.Message}\nEmit the corrected ```yaml block."));
				}
				catch (FormatException ex)
				{
					throw new VoxelizationException($"Manifest generation failed: {ex.Message}", ex);
				}
			}
		}
	}
}
