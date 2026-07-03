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
.vox ─► VoxReader ─► VoxelModel ─► [detect front, top-down] ─┐
                                                             ▼
        build mesh ─► high-res camera screenshot (PNG) ─► Claude vision ─► 2D picks (u,v)
                                 └──────────────► ray-march the same view ─► Vector3 position + normal
```

**Orientation first.** The model's front (its face) might point anywhere. Before
placing eyes, a top-down render is run through `ImageFacingDirection` — looking
straight down the up axis removes the toward/away ambiguity a side view has, so the
eight-way compass code fully determines the front's yaw. The eye-placement camera is
then turned to look at that front three-quarter, so eyes land on the face. Toggle off
with `AutoOrient = false` to use a fixed `View`.

**Real screenshot.** The render is a proper high-resolution camera shot of the shaded
voxel mesh (`Camera.SubmitRenderRequest`, URP), not a flat splat — far easier for the
vision model to read. It's orthographic and driven straight from the projection, so a
2D pick still reprojects to the exact voxel. Each pick becomes a ray along the view
direction; the raycaster returns the first surface voxel and its entry-face normal, so
eyes land on the side of a head as readily as the front. No GPU (batch/headless) falls
back to a deterministic CPU render.

## Pieces

| Type | Role |
|------|------|
| `OrthographicView` / `VoxelViewProjection` | Camera basis + invertible 2D↔ray mapping; also configures a real ortho camera |
| `ModelOrientation` | Top-down `ImageFacingDirection` pass → a front-facing view |
| `VoxelPreviewMesh` | Culled-face cube mesh with shading baked into vertex colours |
| `VoxelCameraRenderer` | High-res camera screenshot (URP `SubmitRenderRequest`, built-in fallback) |
| `VoxelIsometricRenderer` / `VoxelRender` | CPU splat fallback + the facade that picks camera-else-CPU |
| `ImageEyePlacer` | Claude-vision core: image → normalised 2D eye picks |
| `VoxelRaycaster` | DDA march → first surface voxel + entry-face normal |
| `EyePlacer` | Orchestrator: `PlaceAsync` (orient + render), `PlaceFromImageAsync`, `PlaceGeometric` |
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
