using System.Collections.Generic;
using System.Text;
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
			var sb = new StringBuilder();
			sb.AppendLine("The previous YAML descriptor you produced failed to build. The errors are below.");
			sb.AppendLine("Return a CORRECTED descriptor in the same two-fenced-block format (```yaml ...``` then ```feedback ...```).");
			sb.AppendLine();
			sb.AppendLine("Previous YAML:");
			sb.AppendLine("```yaml");
			sb.AppendLine(previousYaml);
			sb.AppendLine("```");
			sb.AppendLine();
			sb.AppendLine("Errors:");
			foreach (var error in errors)
			{
				sb.Append("- ");
				sb.AppendLine(error);
			}

			_history.Add(new AnthropicMessage("user", sb.ToString()));
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
