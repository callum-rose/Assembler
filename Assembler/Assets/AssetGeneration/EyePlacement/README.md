# Eye Placement

Places eyes on an arbitrary voxel model and returns their **3D grid coordinates**
(position + outward surface normal) in the model's own space — the same integer
space as `Assembler.Voxels.VoxelModel`, i.e. a `.vox` read through `VoxReader` is
Z-up.

## Why it works the way it does

A picture only tells you *where* on the screen an eye goes (2D). To get a real 3D
coordinate you need the model's occupancy, so the **`.vox` is the source of truth**
and the image is only the vision cue. The flow:

```
.vox ─► VoxReader ─► VoxelModel ─┬─► render isometric view (PNG) ─► Claude vision ─► 2D picks (u,v)
                                 └────────────► ray-march the same view ─► Vector3 position + normal
```

An **isometric ¾ view** (not a flat front view) is the default so eyes can land on
the **side** of a head as easily as the front — it exposes a front, a side and the
top at once. Each 2D pick becomes a ray along the view direction; the raycaster
returns the first surface voxel it enters and the face it entered through (the
outward normal). Use `IsometricLeft` (or the bilateral symmetry of most creatures)
to reach the far side.

## Pieces

| Type | Role |
|------|------|
| `OrthographicView` / `VoxelViewProjection` | Camera basis + invertible 2D↔ray mapping (Z-up presets: `Isometric`, `IsometricLeft`, `Front`, `Top`) |
| `VoxelIsometricRenderer` | CPU render of a `VoxelModel` to a PNG (deterministic, no scene) |
| `ImageEyePlacer` | Claude-vision core: image → normalised 2D eye picks |
| `VoxelRaycaster` | DDA march → first surface voxel + entry-face normal |
| `EyePlacer` | Orchestrator: `PlaceAsync` (auto-render), `PlaceFromImageAsync` (your own image), `PlaceGeometric` (offline, no key) |
| `GeometricEyePlacer` | Deterministic fallback: head-band + bilateral symmetry, no AI |

## Usage

```csharp
var model = VoxReader.Read(File.ReadAllBytes(voxPath));
var options = new EyePlacementOptions { EyeCount = 2, View = OrthographicView.Isometric };

// Vision path (needs an Anthropic API key):
EyePlacementResult result = await EyePlacer.PlaceAsync(apiKey, model, options);
foreach (var eye in result.Eyes)
    Debug.Log($"{eye.Position} facing {eye.Normal}");

// Offline path (no key, naive on unusual shapes):
EyePlacementResult geo = EyePlacer.PlaceGeometric(model, options);
```

Or drive it from the editor: **Assembler ▸ Eye Placement**.
