# Generation.Verification

Closes the loop on LLM-driven YAML generation: sends a game descriptor request to Claude, runs the response through the full parse/transform/build pipeline, and feeds any errors back to Claude for a fix-up — repeating until the build succeeds or the attempt budget is exhausted.

## Purpose

`Assembler.Generation` produces YAML; this assembly verifies it actually builds. The orchestrator owns the request-write-build-retry loop and writes the descriptor to disk on the first attempt, overwriting it on subsequent fix-up rounds.

## Key public API

| Type | Role |
|---|---|
| `GenerationOrchestrator` | Main entry point. Call `CreateDefault(apiKey)` then `GenerateAsync(prompt, maxAttempts, ct)`. |
| `GenerationResult` | Abstract result — either `SuccessfulGeneration(YamlPath, Attempts)` or `FailedGeneration(YamlPath?, Attempts)`. |
| `Attempt` | Per-iteration record — `RequestFailedAttempt`, `InvalidResponseAttempt`, or `BuildAttempt` (includes `BuildResult`). |
| `BuildHarness.TryBuild(yaml)` | Runs `GameFileParser` → `Transformer` → `Builder`, captures Unity error/exception logs, returns `BuildResult`. |
| `IGeneratorLogger` | Optional logging sink injected into `GenerationOrchestrator`. |

## Editor / Runtime entry points

- `GameDescriptorGeneratorWindow` — Unity Editor window at **Assembler > Generate Game Descriptor**. API key is stored in `EditorPrefs` under `Assembler.Generation.ApiKey` (shared with `AnthropicSmokeTest`).
- `AnthropicSmokeTest` — Editor menu **Assembler > Smoke Test Anthropic Client**; sends a single "say hi" message to verify connectivity.
- `Runtime/PlayerBuildSmoke` — MonoBehaviour for player-build smoke tests; do not ship with the API key field populated.

## Gotchas

- `BuildHarness.TryBuild` hooks `Application.logMessageReceivedThreaded` to intercept Unity errors — it catches exceptions thrown by the pipeline *and* errors logged without throwing (e.g. from within MonoBehaviour constructors).
- The YAML file is written to disk after the first successful response (even before the build check). Fix-up attempts overwrite the same path.
- The assembly depends on `Assembler.Generation`, `Assembler.Anthropic`, `Assembler.Building`, `Assembler.Parsing`, and `Assembler.Deserialisation`. It must not be referenced by those assemblies (no circular deps).
- `IsExternalInit.cs` and `csc.rsp` are Unity compatibility shims for `record` types and nullable reference types.
