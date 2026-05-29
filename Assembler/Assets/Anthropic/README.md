# Assembler.Anthropic

A thin wrapper around the official Anthropic C# SDK (installed via NuGetForUnity). Provides a minimal streaming `AnthropicClient` that sends messages to the Anthropic API with prompt caching enabled on the system prompt, plus supporting types (`AnthropicMessage`, `AnthropicRequestException`) and a `FencedBlockExtractor` utility for pulling named fenced code blocks out of model responses.

Used exclusively by `Assembler.Generation` to drive LLM-based YAML game-descriptor generation; the SDK is fully wrapped so callers never touch SDK types directly.
