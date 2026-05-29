# Assembler.Anthropic

Thin wrapper around the official Anthropic C# SDK (installed via NuGetForUnity). Provides a minimal streaming client and supporting types consumed exclusively by `Assembler.Generation` to drive LLM-based YAML game-descriptor generation.

## Public API

### `AnthropicClient : IDisposable`
Core client. Constructed with an API key, optional model string, and optional max-tokens.

```csharp
Task<string> SendAsync(
    string cachedSystemPrompt,
    IReadOnlyList<AnthropicMessage> messages,
    CancellationToken cancellationToken,
    Action<string>? onDelta = null)
```

Streams a response from the Messages API. The system prompt is sent with `cache_control: ephemeral` to enable prompt caching. `onDelta` receives each text chunk as it arrives. Throws `AnthropicRequestException` on API or transport errors; swallows `OperationCanceledException`.

Default model: `claude-opus-4-7`. Default max tokens: 16000.

### `AnthropicMessage`
Simple role/content pair (`"user"` or `"assistant"`) passed into `SendAsync`.

### `AnthropicRequestException`
Wraps SDK-typed exceptions into a single exception with an `int StatusCode` property (HTTP code, or 0 for non-HTTP errors).

### `FencedBlockExtractor.Extract(string text, string blockName) : string?`
Static utility. Extracts the body of a named fenced block (e.g. ` ```yaml ... ``` `) from an LLM response string. Uses cached compiled regexes.

## Notes

- **Sole consumer**: `Assembler.Generation` — no other assembly references this one.
- **SDK dependency**: Requires the `Anthropic` NuGet package via NuGetForUnity. The SDK client is wrapped (not exposed) so callers never touch SDK types directly.
- `csc.rsp` enables nullable reference types, matching the project-wide convention.
