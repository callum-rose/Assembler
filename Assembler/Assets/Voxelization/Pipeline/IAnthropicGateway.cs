using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Voxelization
{
	/// <summary>
	/// The pipeline's seam onto the Anthropic API: stages name themselves (for
	/// token accounting) and pick a model id per call (Decision 10). Tests stub
	/// this; production routes through <see cref="AnthropicGateway"/>.
	/// </summary>
	public interface IAnthropicGateway : IDisposable
	{
		Task<string> SendAsync(
			string stage,
			string model,
			string systemPrompt,
			IReadOnlyList<AnthropicMessage> messages,
			CancellationToken ct,
			IReadOnlyList<AnthropicTool>? tools = null,
			Func<AnthropicToolUse, CancellationToken, Task<AnthropicToolResult>>? onToolUse = null,
			int maxToolIterations = AnthropicClient.DefaultMaxToolIterations);
	}

	/// <summary>One <see cref="AnthropicClient"/> per model id, all reporting into one usage tracker.</summary>
	public sealed class AnthropicGateway : IAnthropicGateway
	{
		private readonly string _apiKey;
		private readonly TokenUsageTracker _usage;
		private readonly Dictionary<string, AnthropicClient> _clients = new();
		private readonly object _gate = new();

		public AnthropicGateway(string apiKey, TokenUsageTracker usage)
		{
			_apiKey = apiKey;
			_usage = usage;
		}

		public Task<string> SendAsync(
			string stage,
			string model,
			string systemPrompt,
			IReadOnlyList<AnthropicMessage> messages,
			CancellationToken ct,
			IReadOnlyList<AnthropicTool>? tools = null,
			Func<AnthropicToolUse, CancellationToken, Task<AnthropicToolResult>>? onToolUse = null,
			int maxToolIterations = AnthropicClient.DefaultMaxToolIterations)
		{
			return ClientFor(model).SendAsync(
				systemPrompt,
				messages,
				ct,
				tools: tools,
				onToolUse: onToolUse,
				maxToolIterations: maxToolIterations,
				onUsage: usage => _usage.Record(stage, usage));
		}

		public void Dispose()
		{
			lock (_gate)
			{
				foreach (var client in _clients.Values)
				{
					client.Dispose();
				}

				_clients.Clear();
			}
		}

		private AnthropicClient ClientFor(string model)
		{
			lock (_gate)
			{
				if (!_clients.TryGetValue(model, out var client))
				{
					client = new AnthropicClient(_apiKey, model);
					_clients[model] = client;
				}

				return client;
			}
		}
	}
}
