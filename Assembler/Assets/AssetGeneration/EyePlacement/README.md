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
.vox ─► VoxReader ─► VoxelModel ─► [detect front: ring of iso views → pick best] ─┐
                                                                                  ▼
        build mesh ─► high-res camera screenshot (PNG) ─► Claude vision ─► 2D picks (u,v)
                                 └──────────────► ray-march the same view ─► Vector3 position + normal
```

**Orientation first.** The model's front (its face) might point anywhere. Before
placing eyes, a ring of isometric views is rendered around the up axis and
`ImageFacingDirection.SelectViewAsync` picks the one that best shows the front. Iso
candidates carry far more shape information than a flat view, and choosing from concrete
rendered angles sidesteps the toward/away ambiguity a single in-plane compass read has —
each candidate *is* a real yaw, and the winner is used directly as the eye-placement
render. Toggle off with `AutoOrient = false` to use a fixed `View`.

`ImageFacingDirection` returns an `OrientationAnswer` discriminated union — either a
`Facing(FacingDirection)` (single-image compass, the original mode) or a `ViewIndex(int)`
(which of several candidates shows the front).

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
| `ModelOrientation` | Renders a ring of iso views, picks the front via `ImageFacingDirection.SelectViewAsync` |
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

## Evaluation harness (issue #479)

Placement quality is judged **only** on the resolved 3D anchors — never a 2D pick-in-region
hit-rate, which read 87% when the true 3D placement was ~0%. The harness scores each anchor on
three things: it is **within tolerance** of a ground-truth eye, **on a real surface voxel**, and
its **normal does not point up** (eyes essentially never face +Z). A model passes only when every
ground-truth eye is reached.

### Ground truth

Each corpus model is a `<name>.vox` paired with a human-authored `<name>.eyes.json` sidecar giving
the acceptable eye regions in the model's own `.vox` grid coordinates (Z-up). Author them by
inspecting the orbit renders (`EyePlacementSpikeBatch.Render` emits the 8-yaw ring) and spot-check
by eye.

```json
{
  "name": "spotted_cow",
  "note": "authored from the 8-yaw ring",
  "eyes": [
    { "center": { "x": 12, "y": 5,  "z": 18 }, "radiusVoxels": 2 },
    { "center": { "x": 12, "y": 15, "z": 18 }, "radiusVoxels": 2 }
  ]
}
```

`radiusVoxels` is the per-eye acceptance radius; omit it (or use 0) to fall back to the scorer's
default tolerance. The `.vox` files are large and untracked — only the `.eyes.json` sidecars are
committed.

### Running it

One command turns the corpus into per-model PASS/FAIL + an orbit montage per model:

```
Unity -batchmode -quit -projectPath <project> \
  -executeMethod Assembler.AssetGeneration.EyePlacement.EyePlacementEvalBatch.Evaluate \
  -corpusDir <dir-of-vox-and-eyes.json> -outDir <dir> \
  [-apiKey sk-...] [-mode vision|geometric] [-gtDir <dir>] \
  [-tolerance 2.5] [-upDot 0.6] [-viewCount 8]
```

With an API key it runs the full vision pipeline (the "current pipeline" whose baseline is
expected ≈0/N) — run it **without** `-nographics` so the vision cue is the crisp GPU render. With
no key it scores the offline geometric fallback (no GPU/network). Output: `eval_summary.json`
(machine-readable per-model + totals) and `<name>_montage.png` — the human-review artifact that
draws ground-truth regions (cyan) and resolved anchors (green = reached its eye, red = didn't,
grey = extra; hollow when on the model's far side for that view) across the yaw ring.

| Type | Role |
|------|------|
| `EyeGroundTruth` | The `.eyes.json` ground-truth format + loader |
| `EyePlacementScorer` | Pure 3D scoring: match anchors → eyes, check within-N / on-surface / not-up |
| `EyeMontage` | Orbit contact sheet drawing ground truth + anchors across the yaw ring |
| `EyePlacementEvalBatch` | The one-command headless entry: corpus + GT → PASS/FAIL summary + montages |
