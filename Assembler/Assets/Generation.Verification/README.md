# Assembler.Generation.Verification

Closes the loop on LLM-driven YAML generation. Sends a game-descriptor request to Claude, runs the response through the full parse/transform/build pipeline, and feeds any errors back to Claude for a fix-up — repeating until the build succeeds or the attempt budget is exhausted.

## Public API

| Type / Member | Purpose |
|---|---|
| `GenerationOrchestrator.CreateDefault(apiKey)` | Construct with default dependencies. |
| `GenerationOrchestrator.GenerateAsync(prompt, maxAttempts, ct)` | Main entry point. Owns the request-write-build-retry loop. |
| `GenerationResult` | Abstract result — `SuccessfulGeneration(YamlPath, Attempts)` or `FailedGeneration(YamlPath?, Attempts)`. |
| `Attempt` | Per-iteration record: `RequestFailedAttempt`, `InvalidResponseAttempt`, or `BuildAttempt` (includes `BuildResult`). |
| `BuildHarness.TryBuild(yaml)` | Runs `GameFileParser → Transformer → Builder`, captures Unity error/exception logs, returns `BuildResult`. |
| `IGeneratorLogger` | Optional logging sink injected into `GenerationOrchestrator`. |
| `GameDescriptorGeneratorWindow` | Unity Editor window at **Assembler > Generate Game Descriptor**. API key stored in `EditorPrefs` under `Assembler.Generation.ApiKey`. |

## Gotchas

- `BuildHarness.TryBuild` hooks `Application.logMessageReceivedThreaded` to intercept Unity errors — catches exceptions thrown by the pipeline *and* errors logged without throwing (e.g. from MonoBehaviour constructors).
- The YAML file is written to disk after the first successful response (even before the build check). Fix-up attempts overwrite the same path.
- Depends on `Assembler.Generation`, `Assembler.Anthropic`, `Assembler.Building`, `Assembler.Parsing`, `Assembler.Deserialisation`. Must not be referenced by those assemblies (no circular deps).
- `Runtime/PlayerBuildSmoke` is a MonoBehaviour for player-build smoke tests; do not ship with the API key field populated.
