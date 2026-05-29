# Assembler.Generation.Verification

Closes the loop on LLM-driven YAML generation. Sends a game-descriptor request to Claude, runs the response through the full parse/transform/build pipeline, and feeds any errors back to Claude for a fix-up — repeating until the build succeeds or the attempt budget is exhausted.

`GenerationOrchestrator` owns the request-write-build-retry loop and is the main entry point (`CreateDefault(apiKey)` + `GenerateAsync(prompt, maxAttempts, ct)`), returning a `GenerationResult` (`SuccessfulGeneration` or `FailedGeneration`) with a per-iteration `Attempt` history. `BuildHarness.TryBuild(yaml)` runs `GameFileParser → Transformer → Builder` and captures Unity error/exception logs in addition to thrown exceptions. The directory also contains the Unity Editor window (`Assembler > Generate Game Descriptor`), an Anthropic connectivity smoke test, and a player-build smoke MonoBehaviour.
