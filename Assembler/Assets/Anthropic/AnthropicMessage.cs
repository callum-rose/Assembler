namespace Assembler.Anthropic
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
}
