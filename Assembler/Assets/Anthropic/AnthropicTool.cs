namespace Assembler.Anthropic
{
	/// <summary>
	/// Domain-neutral description of a client-side tool Claude may call. The
	/// <see cref="InputJsonSchema"/> is a JSON Schema object string (e.g.
	/// <c>{"type":"object","properties":{...},"required":[...]}</c>) describing
	/// the tool's input. <see cref="AnthropicClient"/> maps this onto the SDK's
	/// custom-tool type.
	/// </summary>
	public sealed record AnthropicTool(string Name, string Description, string InputJsonSchema);
}
