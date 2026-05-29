# Assembler.Generation

LLM-driven YAML game descriptor generation. Wraps `AnthropicClient` with a structured system prompt and a multi-turn conversation loop that can automatically request fixes when a generated descriptor fails to build.

## Key Types

**`GameDescriptorGenerator`** — stateful conversation wrapper. Create once per generation session.
- `RequestInitialAsync(userPrompt, ct)` — start a new generation from a text prompt.
- `RequestFixAsync(previousYaml, errors, ct)` — send build errors back to the model and request a corrected descriptor. Maintains conversation history across calls.
- Returns `GeneratorResponse { RawText, Yaml?, Feedback? }`.

**`SystemPromptBuilder.Build()`** — assembles the system prompt from three `Resources/GenerationPrompts/` text assets: `Skill.txt`, `Behaviours.txt`, `CompilerSyntax.txt`. Throws `FileNotFoundException` if any are missing — run `Assembler > Sync Generation Prompts` from the Unity Editor menu to populate them.

**`ResponseExtractor.Extract(text)`** — parses the model response into `ExtractedResponse { Yaml?, Feedback? }` by extracting fenced code blocks tagged ` ```yaml ` and ` ```feedback `.

**`DescriptorFileWriter`** — writes YAML to disk.
- `Write(yaml, title)` — saves to `Application.persistentDataPath/GeneratedGameDescriptors/` with an auto-sanitised filename.
- `WriteTo(yaml, fullPath)` — writes to an explicit path.

## Dependencies & Gotchas

- Depends on `Assembler.Anthropic` for `AnthropicClient` and `FencedBlockExtractor`.
- The system prompt is loaded at runtime via `Resources.Load` — the three prompt files must exist under `Resources/GenerationPrompts/`. Regenerate `Behaviours.txt` via `Assembler > Generate Behaviour Docs` after adding new behaviours.
- The model must reply with exactly two fenced blocks (`yaml` then `feedback`). If `Yaml` is null after extraction, treat generation as failed and optionally retry via `RequestFixAsync`.
- Tests live in `Assets/Tests/Tests.Generation`.
