using System;
using System.Collections.Generic;

namespace Assembler.Anthropic
{
	public sealed class AnthropicMessage
	{
		public string Role { get; }
		public string Content { get; }
		public IReadOnlyList<AnthropicImage> Images { get; }

		public AnthropicMessage(string role, string content)
			: this(role, content, Array.Empty<AnthropicImage>())
		{
		}

		public AnthropicMessage(string role, string content, IReadOnlyList<AnthropicImage> images)
		{
			Role = role;
			Content = content;
			Images = images;
		}
	}
}
