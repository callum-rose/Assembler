# Voxels

LLM-driven voxel generation and I/O. Covers the full lifecycle: prompt the model to produce voxel data as plain text, parse it into an in-memory model, convert between coordinate conventions, encode to a binary voxel format, and save the result as a Unity asset.

The in-memory model is the central type, with reader/writer pairs for each supported file format and a coordinate-swap utility for bridging between the Y-up convention used at authoring time and the Z-up convention used by the binary format. A fluent async pipeline composes the generation, refinement, axis-swapping, dedupe, encoding, and persistence steps; observer callbacks are marshalled back to the Unity main thread. A Unity Editor window exposes the pipeline as an interactive tool.
