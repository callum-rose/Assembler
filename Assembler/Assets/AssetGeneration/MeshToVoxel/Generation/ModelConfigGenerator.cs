using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.AssetGeneration.MeshToVoxel.Generation
{
    /// <summary>
    /// Public entry point: given one model's description and the shared art-direction blurb, returns
    /// a single <see cref="ModelConfig"/> in one call. Stateless — each call sends a fresh
    /// one-message conversation. The caller owns the <see cref="AnthropicClient"/> (and thus the
    /// key/model), matching the <c>GameDescriptorGenerator</c> precedent.
    /// </summary>
    public sealed class ModelConfigGenerator
    {
        private readonly AnthropicClient _client;
        private readonly StyleRules _rules;
        private readonly SettingsRules _settingsRules;
        private readonly string _systemPrompt;

        public ModelConfigGenerator(
            AnthropicClient client, StyleRules? rules = null, SettingsRules? settingsRules = null)
        {
            _client = client;
            _rules = rules ?? StyleRules.Load();
            _settingsRules = settingsRules ?? SettingsRules.Load();
            _systemPrompt = ConfigPromptBuilder.Build(_rules, _settingsRules);
        }

        public async Task<ModelConfig> ChooseAsync(string assetDescription, string artContext, CancellationToken ct)
        {
            var history = new List<AnthropicMessage> { new("user", BuildUserMessage(assetDescription, artContext)) };
            var raw = await _client.SendAsync(_systemPrompt, history, ct);
            return ConfigParser.Parse(raw, _rules, _settingsRules);
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
