using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Assembler.Generation
{
	public sealed class GeneratorResponse
	{
		public string RawText { get; }
		public string? Yaml { get; }
		public string? Feedback { get; }

		public GeneratorResponse(string rawText, string? yaml, string? feedback)
		{
			RawText = rawText;
			Yaml = yaml;
			Feedback = feedback;
		}
	}

	public sealed class GameDescriptorGenerator
	{
		private readonly AnthropicClient _client;
		private readonly string _systemPrompt;
		private readonly List<AnthropicMessage> _history = new();

		public GameDescriptorGenerator(AnthropicClient client, string? systemPromptOverride = null)
		{
			_client = client;
			_systemPrompt = systemPromptOverride ?? SystemPromptBuilder.Build();
		}

		public Task<GeneratorResponse> RequestInitialAsync(string userPrompt, CancellationToken ct)
		{
			_history.Clear();
			_history.Add(new AnthropicMessage("user", userPrompt));
			return SendAndRecordAsync(ct);
		}

		public Task<GeneratorResponse> RequestFixAsync(string previousYaml, IReadOnlyList<string> errors, CancellationToken ct)
		{
			var errorLines = string.Join("\n", errors.Select(e => $"- {e}"));
			var message =
				$$"""
				  The previous YAML descriptor you produced failed to build. The errors are below.
				  Return a CORRECTED descriptor in the same two-fenced-block format (```yaml ...``` then ```feedback ...```).

				  Previous YAML:
				  ```yaml
				  {{previousYaml}}
				  ```

				  Errors:
				  {{errorLines}}
				  """;

			_history.Add(new AnthropicMessage("user", message));
			return SendAndRecordAsync(ct);
		}

		private async Task<GeneratorResponse> SendAndRecordAsync(CancellationToken ct)
		{
			var raw = await _client.SendAsync(_systemPrompt, _history, ct);
			_history.Add(new AnthropicMessage("assistant", raw));
			var extracted = ResponseExtractor.Extract(raw);
			return new GeneratorResponse(raw, extracted.Yaml, extracted.Feedback);
		}
	}
}