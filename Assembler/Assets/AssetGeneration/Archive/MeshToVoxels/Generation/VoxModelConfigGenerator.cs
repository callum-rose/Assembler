using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.AssetGeneration.MeshToVoxels.Generation
{
    /// <summary>
    /// Public entry point: given one model's description and the shared art-direction blurb, returns
    /// a single <see cref="VoxModelConfig"/> in one call. Stateless — each call sends a fresh
    /// one-message conversation. The caller owns the <see cref="AnthropicClient"/> (and thus the
    /// key/model), matching the <c>GameDescriptorGenerator</c> precedent.
    /// </summary>
    public sealed class VoxModelConfigGenerator
    {
        private readonly AnthropicClient _client;
        private readonly VoxStyleRules _rules;
        private readonly string _systemPrompt;

        public VoxModelConfigGenerator(AnthropicClient client, VoxStyleRules? rules = null)
        {
            _client = client;
            _rules = rules ?? VoxStyleRules.Load();
            _systemPrompt = VoxConfigPromptBuilder.Build(_rules);
        }

        public async Task<VoxModelConfig> ChooseAsync(string assetDescription, string artContext, CancellationToken ct)
        {
            var history = new List<AnthropicMessage> { new("user", BuildUserMessage(assetDescription, artContext)) };
            var raw = await _client.SendAsync(_systemPrompt, history, ct);
            return VoxConfigParser.Parse(raw, _rules);
        }

        private static string BuildUserMessage(string assetDescription, string artContext) =>
            $$"""
              Shared art direction:
              {{artContext}}

              This model:
              {{assetDescription}}
              """;
    }
}
