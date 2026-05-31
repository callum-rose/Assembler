namespace Assembler.Anthropic
{
	/// <summary>
	/// A single tool invocation requested by Claude. <see cref="InputJson"/> is
	/// the raw JSON object Claude produced for the tool's input (accumulated from
	/// the streamed partial-json deltas).
	/// </summary>
	public sealed record AnthropicToolUse(string Id, string Name, string InputJson);
}
