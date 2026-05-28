using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
	/// </summary>
	public sealed class AnthropicClient : IDisposable
	{
		private const long DefaultMaxTokens = 16000;

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
			Action<string>? onDelta = null)
		{
			var parameters = new MessageCreateParams
			{
				Model = _model,
				MaxTokens = _maxTokens,
				System = new List<TextBlockParam> { new(cachedSystemPrompt) { CacheControl = new CacheControlEphemeral() }, },
				Messages = messages
					.Select(m => new MessageParam
					{
						Role = RoleFromString(m.Role),
						Content = new MessageParamContent(m.Content)
					})
					.ToArray(),
			};

			var sb = new StringBuilder();

			try
			{
				await foreach (var ev in _client.Messages.CreateStreaming(parameters, cancellationToken)
					                .ConfigureAwait(false))
				{
					// Per the SDK docs each streamed event stringifies to its
					// incremental text delta — that's how `fullText += msg` works
					// in the documented example. Non-text events stringify to
					// empty, so we just append unconditionally.
					var delta = ev?.ToString();
					if (string.IsNullOrEmpty(delta)) continue;

					sb.Append(delta);
					onDelta?.Invoke(delta);
				}
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (AnthropicApiException ex)
			{
				throw new AnthropicRequestException(GetStatusCode(ex), ex.Message, ex);
			}
			catch (AnthropicException ex)
			{
				throw new AnthropicRequestException(0, ex.Message, ex);
			}

			if (sb.Length == 0)
			{
				throw new AnthropicRequestException(200, "Anthropic response contained no text deltas.");
			}

			return sb.ToString();
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
	}
}