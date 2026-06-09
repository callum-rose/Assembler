using System.Collections.Generic;

namespace Assembler.Anthropic
{
	public sealed class AnthropicMessage
	{
		public string Role { get; }
		public string Content { get; }

		/// <summary>
		/// Optional images attached to this turn (only meaningful on user turns).
		/// Null/empty keeps the legacy bare-string projection — backward compatible.
		/// </summary>
		public IReadOnlyList<AnthropicImage>? Images { get; }

		public AnthropicMessage(string role, string content)
		{
			Role = role;
			Content = content;
		}

		public AnthropicMessage(string role, string content, IReadOnlyList<AnthropicImage>? images)
		{
			Role = role;
			Content = content;
			Images = images;
		}
	}
}
