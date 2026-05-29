# Assembler.Generation

LLM-driven YAML game-descriptor generation. Wraps `AnthropicClient` (from `Assembler.Anthropic`) with a structured system prompt and a multi-turn conversation that can request fixes when a generated descriptor fails to build.

`GameDescriptorGenerator` is the stateful conversation wrapper, with `RequestInitialAsync` to start a generation from a user prompt and `RequestFixAsync` to feed build errors back to the model. `SystemPromptBuilder` assembles the system prompt at runtime from three `Resources/GenerationPrompts/` text assets (`Skill.txt`, `Behaviours.txt`, `CompilerSyntax.txt`), which are kept in sync via the `Assembler > Sync Generation Prompts` and `Assembler > Generate Behaviour Docs` editor menu items. `ResponseExtractor` parses the model's reply into a `yaml` block and a `feedback` block, and `DescriptorFileWriter` persists the generated YAML to `Application.persistentDataPath/GeneratedGameDescriptors/`.
