# Generation

LLM-driven YAML game-descriptor generation. Wraps the Anthropic client with a structured system prompt and a multi-turn conversation that can request fixes when a generated descriptor fails to build.

The system prompt is assembled at runtime from a few text assets describing the supported behaviour catalogue, the expression-language syntax, and the overall authoring skill; these are kept in sync with the codebase via editor menu items. Model responses are expected to contain a YAML block and a feedback block, which are extracted and either persisted to disk or fed back in for a follow-up fix request.
