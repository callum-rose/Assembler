# Anthropic

A thin wrapper around the official Anthropic C# SDK. Provides a minimal streaming client for sending messages to the Anthropic API with prompt caching enabled on the system prompt, along with a small utility for extracting named fenced code blocks out of model responses.

Used exclusively by the LLM-driven game-descriptor generation layer; the SDK is fully wrapped so callers never touch SDK types directly.
