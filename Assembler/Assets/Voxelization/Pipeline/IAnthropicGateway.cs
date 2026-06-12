using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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

	/// <summary>
	/// One <see cref="AnthropicClient"/> per model id, all reporting into one
	/// usage tracker. <paramref name="onActivity"/> receives a live status line
	/// as responses stream in, so a UI can show progress during the long quiet
	/// stretches of a model call. <paramref name="onTranscript"/> receives one
	/// fully-formatted block per call — system prompt, every message (and any
	/// attached images), the tools offered, each tool call + its result, the
	/// final response and the token usage — so an operator can see the exact
	/// prompt and reply behind every pipeline stage when something goes wrong.
	/// </summary>
	public sealed class AnthropicGateway : IAnthropicGateway
	{
		private readonly string _apiKey;
		private readonly TokenUsageTracker _usage;
		private readonly Action<string>? _onActivity;
		private readonly IProgress<string>? _onTranscript;
		private readonly Dictionary<string, AnthropicClient> _clients = new();
		private readonly object _gate = new();
		private int _callCounter;

		public AnthropicGateway(
			string apiKey,
			TokenUsageTracker usage,
			Action<string>? onActivity = null,
			IProgress<string>? onTranscript = null)
		{
			_apiKey = apiKey;
			_usage = usage;
			_onActivity = onActivity;
			_onTranscript = onTranscript;
		}

		public async Task<string> SendAsync(
			string stage,
			string model,
			string systemPrompt,
			IReadOnlyList<AnthropicMessage> messages,
			CancellationToken ct,
			IReadOnlyList<AnthropicTool>? tools = null,
			Func<AnthropicToolUse, CancellationToken, Task<AnthropicToolResult>>? onToolUse = null,
			int maxToolIterations = AnthropicClient.DefaultMaxToolIterations)
		{
			// When no transcript sink is wired, stay on the cheap path: no per-call
			// bookkeeping, no string building, just stream and record usage.
			if (_onTranscript == null)
			{
				_onActivity?.Invoke($"{stage}: waiting for {model}...");
				var streamedQuiet = 0;
				return await ClientFor(model).SendAsync(
					systemPrompt,
					messages,
					ct,
					onDelta: delta =>
					{
						streamedQuiet += delta.Length;
						_onActivity?.Invoke($"{stage}: streaming... {streamedQuiet:n0} chars");
					},
					tools: tools,
					onToolUse: onToolUse,
					maxToolIterations: maxToolIterations,
					onUsage: usage => _usage.Record(stage, usage)).ConfigureAwait(false);
			}

			var callId = Interlocked.Increment(ref _callCounter);
			_onActivity?.Invoke($"{stage}: waiting for {model}...");
			var streamed = 0;

			// Tool calls and their results are accumulated here as the loop runs so
			// the whole interaction emits as one ordered block once the call finishes
			// — interleaved across concurrent assets only at the call boundary.
			var toolLog = new StringBuilder();
			var wrappedToolUse = onToolUse;
			if (onToolUse != null)
			{
				wrappedToolUse = async (use, token) =>
				{
					var result = await onToolUse(use, token).ConfigureAwait(false);
					lock (toolLog)
					{
						AppendToolInteraction(toolLog, use, result);
					}

					return result;
				};
			}

			// onUsage fires once per tool-loop iteration; sum them for the call total
			// while still recording each into the stage bucket as before.
			var total = AnthropicTokenUsage.Zero;
			var response = await ClientFor(model).SendAsync(
				systemPrompt,
				messages,
				ct,
				onDelta: delta =>
				{
					streamed += delta.Length;
					_onActivity?.Invoke($"{stage}: streaming... {streamed:n0} chars");
				},
				tools: tools,
				onToolUse: wrappedToolUse,
				maxToolIterations: maxToolIterations,
				onUsage: usage =>
				{
					total = total.Add(usage);
					_usage.Record(stage, usage);
				}).ConfigureAwait(false);

			_onTranscript.Report(BuildTranscript(callId, stage, model, systemPrompt, messages, tools, toolLog.ToString(), response, total));
			return response;
		}

		/// <summary>
		/// Formats one call into a self-contained, copyable block: the exact system
		/// prompt and every message sent (noting attached images), the tools offered,
		/// each tool call with its input and result, the full response and the token
		/// usage. Nothing is truncated — the operator asked to see every detail.
		/// </summary>
		private static string BuildTranscript(
			int callId,
			string stage,
			string model,
			string systemPrompt,
			IReadOnlyList<AnthropicMessage> messages,
			IReadOnlyList<AnthropicTool>? tools,
			string toolLog,
			string response,
			AnthropicTokenUsage usage)
		{
			var sb = new StringBuilder();
			sb.Append("┏━━ LLM CALL #").Append(callId).Append(" · ").Append(stage).Append(" · ").Append(model).Append(" ━━━\n");

			sb.Append("SYSTEM PROMPT (").Append(systemPrompt.Length).Append(" chars):\n").Append(systemPrompt).Append('\n');

			for (var i = 0; i < messages.Count; i++)
			{
				var message = messages[i];
				sb.Append("\nMESSAGE ").Append(i + 1).Append(" [").Append(message.Role).Append(']');
				if (message.Images.Count > 0)
				{
					sb.Append(" (").Append(message.Images.Count).Append(" image(s): ")
						.Append(string.Join(", ", message.Images.Select(img => $"{img.MediaType} {img.Data.Length:n0} bytes")))
						.Append(')');
				}

				sb.Append(":\n").Append(message.Content).Append('\n');
			}

			if (tools is { Count: > 0 })
			{
				sb.Append("\nTOOLS OFFERED: ").Append(string.Join(", ", tools.Select(t => t.Name))).Append('\n');
			}

			if (toolLog.Length > 0)
			{
				sb.Append(toolLog);
			}

			sb.Append("\nRESPONSE (").Append(response.Length).Append(" chars):\n")
				.Append(response.Length > 0 ? response : "(no response text — see tool calls above)").Append('\n');

			sb.Append("USAGE: in ").Append(usage.InputTokens.ToString("n0"))
				.Append(" · cache read ").Append(usage.CacheReadInputTokens.ToString("n0"))
				.Append(" · cache write ").Append(usage.CacheCreationInputTokens.ToString("n0"))
				.Append(" · out ").Append(usage.OutputTokens.ToString("n0")).Append('\n');
			sb.Append("┗━━ END CALL #").Append(callId).Append(" ━━━");
			return sb.ToString();
		}

		private static void AppendToolInteraction(StringBuilder sb, AnthropicToolUse use, AnthropicToolResult result)
		{
			sb.Append("\n  ┌─ TOOL CALL ").Append(use.Name).Append(" (").Append(use.Id).Append(")\n");
			sb.Append("  │  INPUT: ").Append(use.InputJson).Append('\n');
			sb.Append("  └─ RESULT [").Append(result.IsError ? "ERROR" : "ok").Append("]: ").Append(result.Content).Append('\n');
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
