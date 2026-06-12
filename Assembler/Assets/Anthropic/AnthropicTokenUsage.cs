namespace Assembler.Anthropic
{
	/// <summary>
	/// Token counts for a single Messages API request, surfaced so callers can
	/// instrument cost per pipeline stage. Cache figures cover the ephemeral
	/// prompt-cache writes/reads on the system prompt.
	/// </summary>
	public sealed record AnthropicTokenUsage(
		long InputTokens,
		long OutputTokens,
		long CacheReadInputTokens,
		long CacheCreationInputTokens)
	{
		public static AnthropicTokenUsage Zero { get; } = new(0, 0, 0, 0);

		public AnthropicTokenUsage Add(AnthropicTokenUsage other) => new(
			InputTokens + other.InputTokens,
			OutputTokens + other.OutputTokens,
			CacheReadInputTokens + other.CacheReadInputTokens,
			CacheCreationInputTokens + other.CacheCreationInputTokens);
	}
}
