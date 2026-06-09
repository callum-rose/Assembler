using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Anthropic.Core;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;

namespace Assembler.Anthropic
{
	/// <summary>
	/// Thin wrapper around the official Anthropic C# SDK
	/// (https://www.nuget.org/packages/Anthropic, installed via NuGetForUnity).
	/// Translates our generation-domain types into SDK params, sets ephemeral
	/// cache_control on the system prompt, and pulls back assistant text.
	/// When <c>tools</c> + <c>onToolUse</c> are supplied it runs a client-side
	/// tool loop: tool_use blocks are streamed and accumulated, the handler runs
	/// in-process, and tool_result blocks are fed back until Claude stops
	/// requesting tools (or the iteration cap is hit). Token streaming via
	/// <c>onDelta</c> stays live across every iteration.
	/// </summary>
	public sealed class AnthropicClient : IDisposable
	{
		private const long DefaultMaxTokens = 16000;
		public const int DefaultMaxToolIterations = 8;

		private readonly global::Anthropic.AnthropicClient _client;
		private readonly ApiEnum<string, Model> _model;
		private readonly long _maxTokens;

		public AnthropicClient(string apiKey, string? model = null, long? maxTokens = null)
		{
			if (string.IsNullOrWhiteSpace(apiKey))
			{
				throw new ArgumentException("API key is required", nameof(apiKey));
			}

			_client = new global::Anthropic.AnthropicClient { ApiKey = apiKey };
			_model = string.IsNullOrWhiteSpace(model) ? Model.ClaudeOpus4_7 : (ApiEnum<string, Model>)model!;
			_maxTokens = maxTokens ?? DefaultMaxTokens;
		}

		public async Task<string> SendAsync(
			string cachedSystemPrompt,
			IReadOnlyList<AnthropicMessage> messages,
			CancellationToken cancellationToken,
			Action<string>? onDelta = null,
			IReadOnlyList<AnthropicTool>? tools = null,
			Func<AnthropicToolUse, CancellationToken, Task<AnthropicToolResult>>? onToolUse = null,
			int maxToolIterations = DefaultMaxToolIterations)
		{
			// Seed the SDK message list from our simple (role, text[, images])
			// messages. Intermediate tool_use / tool_result turns are appended here
			// as the loop runs; they never escape into the caller's history.
			var sdkMessages = messages.Select(ToMessageParam).ToList();

			IReadOnlyList<ToolUnion>? sdkTools = tools is { Count: > 0 }
				? tools.Select(BuildTool).ToArray()
				: null;

			var fullText = new StringBuilder();
			var anyToolInvoked = false;

			try
			{
				for (var iteration = 0; ; iteration++)
				{
					// Tools is an init-only property, so it has to be set inside the
					// object initializer — branch rather than mutate after the fact.
					var system = new List<TextBlockParam> { new(cachedSystemPrompt) { CacheControl = new CacheControlEphemeral() }, };
					var parameters = sdkTools == null
						? new MessageCreateParams
						{
							Model = _model,
							MaxTokens = _maxTokens,
							System = system,
							Messages = sdkMessages.ToArray(),
						}
						: new MessageCreateParams
						{
							Model = _model,
							MaxTokens = _maxTokens,
							System = system,
							Messages = sdkMessages.ToArray(),
							Tools = sdkTools,
						};

					var iterationText = new StringBuilder();
					var toolUses = new Dictionary<long, ToolUseAccumulator>();
					StopReason? stopReason = null;

					await foreach (var ev in _client.Messages.CreateStreaming(parameters, cancellationToken))
					{
						if (ev.TryPickContentBlockStart(out var start))
						{
							if (start.ContentBlock.TryPickToolUse(out var toolUseBlock))
							{
								toolUses[start.Index] = new ToolUseAccumulator(toolUseBlock.ID, toolUseBlock.Name);
							}
						}
						else if (ev.TryPickContentBlockDelta(out var blockDelta))
						{
							if (blockDelta.Delta.TryPickText(out var textDelta) && !string.IsNullOrEmpty(textDelta.Text))
							{
								iterationText.Append(textDelta.Text);
								onDelta?.Invoke(textDelta.Text);
							}
							else if (blockDelta.Delta.TryPickInputJson(out var inputJson) && inputJson.PartialJson != null)
							{
								if (toolUses.TryGetValue(blockDelta.Index, out var acc))
								{
									acc.Json.Append(inputJson.PartialJson);
								}
							}
						}
						else if (ev.TryPickDelta(out var messageDelta))
						{
							try { stopReason = messageDelta.Delta.StopReason.Value(); }
							catch { /* stop_reason not present on this delta */ }
						}
					}

					fullText.Append(iterationText);

					var wantsTools = onToolUse != null && toolUses.Count > 0 && stopReason == StopReason.ToolUse;
					if (!wantsTools)
					{
						break;
					}

					if (iteration + 1 >= maxToolIterations)
					{
						break;
					}

					// Replay the assistant turn (any prose + the tool_use blocks),
					// then answer each tool call with a tool_result block.
					var ordered = toolUses.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToList();

					var assistantBlocks = new List<ContentBlockParam>();
					if (iterationText.Length > 0)
					{
						assistantBlocks.Add(new TextBlockParam(iterationText.ToString()));
					}
					foreach (var acc in ordered)
					{
						assistantBlocks.Add(new ToolUseBlockParam
						{
							ID = acc.Id,
							Name = acc.Name,
							Input = ParseJsonObject(acc.Json.ToString()),
						});
					}
					sdkMessages.Add(new MessageParam { Role = Role.Assistant, Content = assistantBlocks });

					var resultBlocks = new List<ContentBlockParam>();
					foreach (var acc in ordered)
					{
						anyToolInvoked = true;
						var result = await onToolUse!(new AnthropicToolUse(acc.Id, acc.Name, acc.Json.ToString()), cancellationToken)
							.ConfigureAwait(false);
						resultBlocks.Add(new ToolResultBlockParam(result.ToolUseId)
						{
							Content = result.Content,
							IsError = result.IsError,
						});
					}
					sdkMessages.Add(new MessageParam { Role = Role.User, Content = resultBlocks });
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (AnthropicApiException ex)
			{
				throw new AnthropicRequestException(GetStatusCode(ex), ex.Message, ex);
			}
			catch (AnthropicException ex)
			{
				throw new AnthropicRequestException(0, ex.Message, ex);
			}

			if (fullText.Length == 0 && !anyToolInvoked)
			{
				throw new AnthropicRequestException(200, "Anthropic response contained no text deltas.");
			}

			return fullText.ToString();
		}

		/// <summary>
		/// Projects one domain message into an SDK <see cref="MessageParam"/>.
		/// Without images this is the legacy bare-string content; with images the
		/// content becomes a block list of [ text, image… ] so vision turns ride
		/// the same path as text. Tool loop / streaming are unaffected.
		/// </summary>
		private static MessageParam ToMessageParam(AnthropicMessage m)
		{
			var role = RoleFromString(m.Role);
			if (m.Images is not { Count: > 0 })
			{
				return new MessageParam { Role = role, Content = m.Content };
			}

			var blocks = new List<ContentBlockParam>();
			if (!string.IsNullOrEmpty(m.Content))
			{
				blocks.Add(new TextBlockParam(m.Content));
			}

			foreach (var image in m.Images)
			{
				blocks.Add(ImageBlockParam.FromRawUnchecked(image.ToWireDictionary()));
			}

			return new MessageParam { Role = role, Content = blocks };
		}

		private static ToolUnion BuildTool(AnthropicTool tool)
		{
			var schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(tool.InputJsonSchema)
						 ?? new Dictionary<string, JsonElement>();
			return new Tool
			{
				Name = tool.Name,
				Description = tool.Description,
				InputSchema = InputSchema.FromRawUnchecked(schema),
			};
		}

		private static IReadOnlyDictionary<string, JsonElement> ParseJsonObject(string json)
		{
			if (string.IsNullOrWhiteSpace(json))
			{
				return new Dictionary<string, JsonElement>();
			}

			return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
				   ?? new Dictionary<string, JsonElement>();
		}

		private static Role RoleFromString(string role)
		{
			return role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
				? Role.Assistant
				: Role.User;
		}

		private static int GetStatusCode(AnthropicApiException ex)
		{
			// AnthropicApiException doesn't surface the status code directly on every
			// version, but the SDK throws specific subclasses per status. Map the common ones.
			return ex switch
			{
				AnthropicBadRequestException => 400,
				AnthropicUnauthorizedException => 401,
				AnthropicForbiddenException => 403,
				AnthropicNotFoundException => 404,
				AnthropicUnprocessableEntityException => 422,
				AnthropicRateLimitException => 429,
				Anthropic5xxException => 500,
				_ => 0,
			};
		}

		public void Dispose()
		{
			_client.Dispose();
		}

		private sealed class ToolUseAccumulator
		{
			public string Id { get; }
			public string Name { get; }
			public StringBuilder Json { get; } = new();

			public ToolUseAccumulator(string id, string name)
			{
				Id = id;
				Name = name;
			}
		}
	}
}
