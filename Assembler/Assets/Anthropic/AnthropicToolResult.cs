namespace Assembler.Anthropic
{
	/// <summary>
	/// The result of handling an <see cref="AnthropicToolUse"/>, fed back to
	/// Claude as a tool_result block. Set <see cref="IsError"/> when the tool
	/// failed so Claude can self-correct within the same turn.
	/// </summary>
	public sealed record AnthropicToolResult(string ToolUseId, string Content, bool IsError);
}
