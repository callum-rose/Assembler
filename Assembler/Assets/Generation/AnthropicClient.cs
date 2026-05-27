using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

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
	}

	public sealed class AnthropicClient : IDisposable
	{
		private const string Endpoint = "https://api.anthropic.com/v1/messages";
		private const string AnthropicVersion = "2023-06-01";
		private const string DefaultModel = "claude-opus-4-7";
		private const int DefaultMaxTokens = 16000;

		private static readonly HttpClient SharedHttp = new()
		{
			Timeout = TimeSpan.FromMinutes(5),
		};

		private readonly string _apiKey;
		private readonly string _model;
		private readonly int _maxTokens;

		public AnthropicClient(string apiKey, string? model = null, int? maxTokens = null)
		{
			if (string.IsNullOrWhiteSpace(apiKey))
			{
				throw new ArgumentException("API key is required", nameof(apiKey));
			}
			_apiKey = apiKey;
			_model = model ?? DefaultModel;
			_maxTokens = maxTokens ?? DefaultMaxTokens;
		}

		public async Task<string> SendAsync(
			string cachedSystemPrompt,
			IReadOnlyList<AnthropicMessage> messages,
			CancellationToken cancellationToken)
		{
			var body = BuildRequestBody(cachedSystemPrompt, messages);

			using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
			request.Headers.Add("x-api-key", _apiKey);
			request.Headers.Add("anthropic-version", AnthropicVersion);
			request.Content = new StringContent(body, Encoding.UTF8);
			request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

			using var response = await SharedHttp.SendAsync(request, cancellationToken).ConfigureAwait(false);
			var responseText = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

			if (!response.IsSuccessStatusCode)
			{
				throw new AnthropicRequestException(
					(int)response.StatusCode,
					$"Anthropic API returned {(int)response.StatusCode}: {responseText}");
			}

			return ExtractAssistantText(responseText);
		}

		private string BuildRequestBody(string cachedSystemPrompt, IReadOnlyList<AnthropicMessage> messages)
		{
			var sb = new StringBuilder();
			sb.Append('{');
			sb.Append("\"model\":").Append(JsonString(_model)).Append(',');
			sb.Append("\"max_tokens\":").Append(_maxTokens).Append(',');
			sb.Append("\"system\":[{")
				.Append("\"type\":\"text\",")
				.Append("\"text\":").Append(JsonString(cachedSystemPrompt)).Append(',')
				.Append("\"cache_control\":{\"type\":\"ephemeral\"}")
				.Append("}],");
			sb.Append("\"messages\":[");
			for (var i = 0; i < messages.Count; i++)
			{
				if (i > 0) sb.Append(',');
				var m = messages[i];
				sb.Append('{');
				sb.Append("\"role\":").Append(JsonString(m.Role)).Append(',');
				sb.Append("\"content\":").Append(JsonString(m.Content));
				sb.Append('}');
			}
			sb.Append("]}");
			return sb.ToString();
		}

		private static readonly Regex TextFieldRegex = new(
			"\"type\"\\s*:\\s*\"text\"\\s*,\\s*\"text\"\\s*:\\s*\"(?<body>(?:\\\\.|[^\"\\\\])*)\"",
			RegexOptions.Compiled);

		private static string ExtractAssistantText(string responseJson)
		{
			var sb = new StringBuilder();
			foreach (Match m in TextFieldRegex.Matches(responseJson))
			{
				if (sb.Length > 0) sb.Append('\n');
				sb.Append(UnescapeJsonString(m.Groups["body"].Value));
			}
			if (sb.Length == 0)
			{
				throw new AnthropicRequestException(
					200,
					$"Could not find assistant text in Anthropic response: {responseJson}");
			}
			return sb.ToString();
		}

		private static string JsonString(string value)
		{
			var sb = new StringBuilder(value.Length + 2);
			sb.Append('"');
			foreach (var c in value)
			{
				switch (c)
				{
					case '\\': sb.Append("\\\\"); break;
					case '"': sb.Append("\\\""); break;
					case '\b': sb.Append("\\b"); break;
					case '\f': sb.Append("\\f"); break;
					case '\n': sb.Append("\\n"); break;
					case '\r': sb.Append("\\r"); break;
					case '\t': sb.Append("\\t"); break;
					default:
						if (c < 0x20)
						{
							sb.Append("\\u").Append(((int)c).ToString("X4"));
						}
						else
						{
							sb.Append(c);
						}
						break;
				}
			}
			sb.Append('"');
			return sb.ToString();
		}

		private static string UnescapeJsonString(string s)
		{
			var sb = new StringBuilder(s.Length);
			for (var i = 0; i < s.Length; i++)
			{
				var c = s[i];
				if (c != '\\' || i + 1 >= s.Length)
				{
					sb.Append(c);
					continue;
				}
				var next = s[++i];
				switch (next)
				{
					case '"': sb.Append('"'); break;
					case '\\': sb.Append('\\'); break;
					case '/': sb.Append('/'); break;
					case 'b': sb.Append('\b'); break;
					case 'f': sb.Append('\f'); break;
					case 'n': sb.Append('\n'); break;
					case 'r': sb.Append('\r'); break;
					case 't': sb.Append('\t'); break;
					case 'u':
						if (i + 4 < s.Length)
						{
							var hex = s.Substring(i + 1, 4);
							if (int.TryParse(hex, System.Globalization.NumberStyles.HexNumber,
								System.Globalization.CultureInfo.InvariantCulture, out var code))
							{
								sb.Append((char)code);
								i += 4;
							}
						}
						break;
					default: sb.Append(next); break;
				}
			}
			return sb.ToString();
		}

		public void Dispose()
		{
		}
	}
}
