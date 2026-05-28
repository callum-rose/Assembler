using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;

namespace Assembler.Generation
{
	public sealed class AnthropicMessage
	{
		public string Role { get; }
		public string Content { get; }

		public AnthropicMessage(string role, string content)
		{
			Role = role;
			Content = content;
		}
	}

	public sealed class AnthropicRequestException : Exception
	{
		public int StatusCode { get; }

		public AnthropicRequestException(int statusCode, string message) : base(message)
		{
			StatusCode = statusCode;
		}

		public AnthropicRequestException(int statusCode, string message, Exception inner) : base(message, inner)
		{
			StatusCode = statusCode;
		}
	}

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
			// ApiEnum<string, Model> has implicit conversions from both string and Model.
			_model = model != null ? (ApiEnum<string, Model>)model : Model.ClaudeOpus4_7;
			_maxTokens = maxTokens ?? DefaultMaxTokens;
		}

		public async Task<string> SendAsync(
			string cachedSystemPrompt,
			IReadOnlyList<AnthropicMessage> messages,
			CancellationToken cancellationToken)
		{
			var sdkMessages = new List<MessageParam>(messages.Count);
			foreach (var m in messages)
			{
				sdkMessages.Add(new MessageParam
				{
					Role = RoleFromString(m.Role),
					Content = new MessageParamContent(m.Content),
				});
			}

			var systemBlocks = new List<TextBlockParam>
			{
				new(cachedSystemPrompt) { CacheControl = new CacheControlEphemeral() },
			};

			var parameters = new MessageCreateParams
			{
				Model = _model,
				MaxTokens = _maxTokens,
				System = systemBlocks,
				Messages = sdkMessages,
			};

			Message response;
			try
			{
				response = await _client.Messages.Create(parameters, cancellationToken).ConfigureAwait(false);
			}
			catch (AnthropicApiException ex)
			{
				throw new AnthropicRequestException(GetStatusCode(ex), ex.Message, ex);
			}
			catch (AnthropicException ex)
			{
				throw new AnthropicRequestException(0, ex.Message, ex);
			}

			return ExtractText(response);
		}

		private static Role RoleFromString(string role)
		{
			return role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
				? Role.Assistant
				: Role.User;
		}

		private static string ExtractText(Message response)
		{
			var sb = new System.Text.StringBuilder();
			foreach (var block in response.Content)
			{
				if (block.TryPickText(out var text) && text != null)
				{
					if (sb.Length > 0) sb.Append('\n');
					sb.Append(text.Text);
				}
			}
			if (sb.Length == 0)
			{
				throw new AnthropicRequestException(200, "Anthropic response contained no text blocks.");
			}
			return sb.ToString();
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
		}
	}
}
