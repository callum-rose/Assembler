# Assembler.Generation

LLM-driven YAML game-descriptor generation. Wraps `AnthropicClient` with a structured system prompt and a multi-turn conversation loop that can request fixes when a generated descriptor fails to build.

## Public API

| Type / Member | Purpose |
|---|---|
| `GameDescriptorGenerator` | Stateful conversation wrapper. Create once per generation session. |
| `GameDescriptorGenerator.RequestInitialAsync(userPrompt, ct)` | Start a new generation from a text prompt. Returns `GeneratorResponse { RawText, Yaml?, Feedback? }`. |
| `GameDescriptorGenerator.RequestFixAsync(previousYaml, errors, ct)` | Send build errors back to the model and request a corrected descriptor. Maintains conversation history. |
| `SystemPromptBuilder.Build()` | Assembles the system prompt from three `Resources/GenerationPrompts/` text assets: `Skill.txt`, `Behaviours.txt`, `CompilerSyntax.txt`. |
| `ResponseExtractor.Extract(text)` | Parses the model response into `ExtractedResponse { Yaml?, Feedback? }` by extracting fenced ```` ```yaml ```` and ```` ```feedback ```` blocks. |
| `DescriptorFileWriter.Write(yaml, title)` | Saves to `Application.persistentDataPath/GeneratedGameDescriptors/` with an auto-sanitised filename. |
| `DescriptorFileWriter.WriteTo(yaml, fullPath)` | Writes to an explicit path. |

## Gotchas

- Depends on `Assembler.Anthropic` for `AnthropicClient` and `FencedBlockExtractor`.
- The system prompt is loaded at runtime via `Resources.Load` — the three prompt files must exist under `Resources/GenerationPrompts/`. Regenerate `Behaviours.txt` via `Assembler > Generate Behaviour Docs` after adding new behaviours; `SystemPromptBuilder.Build()` throws `FileNotFoundException` if any are missing.
- The model must reply with exactly two fenced blocks (`yaml` then `feedback`). If `Yaml` is null after extraction, treat generation as failed and optionally retry via `RequestFixAsync`.
- Tests live in `Assets/Tests/Tests.Generation`.
