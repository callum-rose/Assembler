# Assembler.Voxels

LLM-driven voxel generation and I/O for the Assembler project. Handles the full lifecycle: prompt an LLM to produce Goxel plain-text, parse it into a model, convert coordinates, encode to MagicaVoxel `.vox` binary, and save assets.

## Key types

| Type | Purpose |
|---|---|
| `VoxelModel` | Core data: `Dictionary<Vector3Int, byte>` voxels + `Color32[]` palette + bounds |
| `GoxelTextParser.Parse(string)` | Parses Goxel plain-text (`x y z RRGGBB` per line) → `VoxelModel` |
| `GoxelTextWriter.Write(VoxelModel)` | Serializes `VoxelModel` back to Goxel plain-text |
| `VoxReader.Read(byte[])` | Parses MagicaVoxel `.vox` binary → `VoxelModel` |
| `VoxWriter.Write(VoxelModel)` | Encodes `VoxelModel` → `.vox` binary |
| `GoxelCoordinateConverter.SwapYAndZ(string)` | Swaps Y/Z axes in Goxel text (involutive; converts between Z-up and Y-up) |
| `VoxelGenerationPipeline` | Fluent async pipeline builder |
| `VoxelPipelineContext` | Immutable state record threaded through pipeline stages |
| `IVoxelStage` | Interface for custom pipeline stages |

## Pipeline usage

```csharp
var result = await VoxelGenerationPipeline
    .CreateNew()
    .WithAnthropic(client)
    .WithPrompt("a small red cube")
    .SwapYZAxes()       // LLM outputs Y-up; .vox is Z-up
    .DedupeVoxels()
    .EncodeVox()
    .SaveAsVoxFile("Assets/Resources/Voxels/cube.vox")
    .ExecuteAsync(ct);
```

`FromExisting(prior)` resumes from a previous result for refinement passes. `Refine(instruction)` appends a `RefineGoxelTextStage`.

## Gotchas

- **Coordinate convention**: The LLM is prompted to produce Y-up (Unity). `.vox` and Goxel are Z-up. Always call `SwapYZAxes()` when bridging between LLM output and file I/O.
- **Palette limit**: `GoxelTextParser` throws if a model has more than 255 distinct colours.
- **Thread safety**: Observer callbacks are marshaled to the Unity main thread via `IMainThreadDispatcher`. Stages that touch the Asset Database must use `ctx.MainThread.RunAsync(...)`.
- **Editor window**: `VoxelGeneratorWindow` (Editor-only assembly `Voxels.Editor`) exposes the full pipeline as a Unity Editor tool. Output defaults to `Assets/Resources/Voxels/`.
- **Dependencies**: `Assembler.Anthropic` (LLM client) is required for generation stages only; parse/encode/decode stages have no LLM dependency.
