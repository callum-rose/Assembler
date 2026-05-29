# Assembler.Anthropic

Thin wrapper around the official Anthropic C# SDK (installed via NuGetForUnity). Provides a minimal streaming client and supporting types consumed exclusively by `Assembler.Generation` to drive LLM-based YAML game-descriptor generation.

## Public API

| Type / Member | Purpose |
|---|---|
| `AnthropicClient` | Disposable streaming client. Construct with API key, optional model (default `claude-opus-4-7`), and optional max-tokens (default 16000). |
| `AnthropicClient.SendAsync(cachedSystemPrompt, messages, ct, onDelta?)` | Streams a response from the Messages API. System prompt is sent with `cache_control: ephemeral` for prompt caching. `onDelta` receives each text chunk. |
| `AnthropicMessage` | Simple role/content pair (`"user"` or `"assistant"`) passed into `SendAsync`. |
| `AnthropicRequestException` | Wraps SDK exceptions with an `int StatusCode` property (HTTP code, or 0 for non-HTTP errors). |
| `FencedBlockExtractor.Extract(text, blockName)` | Static utility. Extracts the body of a named fenced block (e.g. ```` ```yaml ... ``` ````) using cached compiled regexes. |

## Gotchas

- **Sole consumer**: `Assembler.Generation` — no other assembly references this one.
- **SDK dependency**: requires the `Anthropic` NuGet package via NuGetForUnity. The SDK client is wrapped (not exposed), so callers never touch SDK types directly.
- `SendAsync` throws `AnthropicRequestException` on API or transport errors and swallows `OperationCanceledException`.
