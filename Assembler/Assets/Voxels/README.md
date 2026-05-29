# Assembler.Voxels

LLM-driven voxel generation and I/O. Handles the full lifecycle: prompt an LLM to produce Goxel plain-text, parse it into a model, convert coordinates, encode to MagicaVoxel `.vox` binary, and save assets.

## Public API

| Type / Member | Purpose |
|---|---|
| `VoxelModel` | Core data: `Dictionary<Vector3Int, byte>` voxels + `Color32[]` palette + bounds. |
| `GoxelTextParser.Parse(string)` | Parses Goxel plain-text (`x y z RRGGBB` per line) → `VoxelModel`. |
| `GoxelTextWriter.Write(VoxelModel)` | Serialises `VoxelModel` back to Goxel plain-text. |
| `VoxReader.Read(byte[])` | Parses MagicaVoxel `.vox` binary → `VoxelModel`. |
| `VoxWriter.Write(VoxelModel)` | Encodes `VoxelModel` → `.vox` binary. |
| `GoxelCoordinateConverter.SwapYAndZ(string)` | Swaps Y/Z axes in Goxel text (involutive; converts between Z-up and Y-up). |
| `VoxelGenerationPipeline` | Fluent async pipeline builder (`CreateNew()`, `FromExisting(prior)`, `WithAnthropic`, `WithPrompt`, `Refine`, `SwapYZAxes`, `DedupeVoxels`, `EncodeVox`, `SaveAsVoxFile`, `ExecuteAsync`). |
| `VoxelPipelineContext` | Immutable state record threaded through pipeline stages. |
| `IVoxelStage` | Interface for custom pipeline stages. |

## Gotchas

- **Coordinate convention**: the LLM is prompted to produce Y-up (Unity), but `.vox` and Goxel are Z-up. Always call `SwapYZAxes()` when bridging between LLM output and file I/O.
- **Palette limit**: `GoxelTextParser` throws if a model has more than 255 distinct colours.
- **Thread safety**: observer callbacks are marshalled to the Unity main thread via `IMainThreadDispatcher`. Stages that touch the Asset Database must use `ctx.MainThread.RunAsync(...)`.
- **Editor window**: `VoxelGeneratorWindow` (Editor-only assembly `Voxels.Editor`) exposes the full pipeline as a Unity Editor tool. Output defaults to `Assets/Resources/Voxels/`.
- **Dependencies**: `Assembler.Anthropic` is required for generation stages only; parse/encode/decode stages have no LLM dependency.
