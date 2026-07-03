# Handoff — Eye Placement

Branch: `claude/nice-lalande-a3cfbf` · PR: https://github.com/callum-rose/Assembler/pull/438

## What it is

A new editor module that works out **where eyes go on an arbitrary voxel model** and
returns their **3D grid coordinates** (position + outward surface normal), in the model's
own Z-up `.vox` space. Assembly: `Assembler.AssetGeneration.EyePlacement` under
`Assets/AssetGeneration/EyePlacement/`. Menu: **Assembler ▸ Eye Placement**.

## The core idea

An image only says *where on screen* an eye goes (2D). Turning that into a real 3D
coordinate needs the model's occupancy, so the **`.vox` is the geometric source of truth**
and the render is only the vision cue:

```
.vox ─► VoxReader ─► VoxelModel
   │
   ├─ [auto-orient] render a ring of isometric views → SelectViewAsync picks the front-facing one
   │
   ├─ build mesh ─► high-res orthographic camera screenshot (PNG) ─► Claude vision ─► 2D eye picks (u,v)
   │
   └─ ray-march the SAME view ─► first surface voxel + entry-face normal ─► Vector3 position + normal
```

Everything is driven from one `VoxelViewProjection`, so a 2D pick reprojects to the exact
voxel it was drawn over.

## Key files

| File | Role |
|------|------|
| `OrthographicView.cs` | Camera basis + invertible 2D↔ray mapping (`VoxelViewProjection`); also configures a real ortho camera. Z-up presets: `Isometric`, `IsometricLeft`, `Front`, `Top`. |
| `VoxelRaycaster.cs` | DDA march → first surface voxel + entry-face normal (pure). |
| `VoxelPreviewMesh.cs` | Culled-face cube mesh with directional shading baked into vertex colours (so an unlit shader still shows form). |
| `VoxelCameraRenderer.cs` | High-res orthographic screenshot via `Camera.SubmitRenderRequest` (URP) using `Assembler/VertexColorUnlit`; built-in-pipeline fallback; returns false with no GPU. |
| `VoxelIsometricRenderer.cs` | Deterministic CPU-splat fallback render (headless). |
| `VoxelRender.cs` | Facade: camera render, else CPU splat. |
| `ModelOrientation.cs` | Renders `OrientationViewCount` iso views and calls `ImageFacingDirection.SelectViewAsync` to pick the front. |
| `ImageEyePlacer.cs` | Claude-vision core: image → normalised 2D eye picks. |
| `EyeVisibility.cs` | Occlusion test: is an eye visible from a view, or masked by the model? |
| `GeometricEyePlacer.cs` | Offline, no-AI fallback (head-band + bilateral symmetry). |
| `EyePlacer.cs` | Orchestrator: `PlaceAsync` / `PlaceFromImageAsync` / `PlaceGeometric`. |
| `Editor/EyePlacementWindow.cs` | UI: pick `.vox`, run, see the candidate grid + picks, copy coordinates. |

Shared dependency: `Assembler.AssetGeneration.ImageOrientation` (front detection).

## Design decisions (and why)

- **Isometric ¾ views, not front-only.** A flat front view can't reach side-of-head eyes
  (fish, horses). Iso exposes front + a side + top at once.
- **Multi-view front detection, not top-down.** `FacingDirection`'s 8-way compass can't
  express toward/away, so a single non-top view is ambiguous. Instead a ring of iso views
  is rendered and the vision model picks the one that best shows the front — each candidate
  is a concrete yaw, so there's no ambiguity, and the winner is used directly for placement.
- **Real camera screenshot, not a splat.** The vision model reads a proper shaded mesh far
  better. Orthographic + projection-driven so reprojection stays exact.
- **`OrientationAnswer` discriminated union.** `ImageFacingDirection` returns a closed union:
  `Facing(FacingDirection)` (single-image compass, incl. T/A) · `ViewIndex(int)` (multi-image
  pick) · `Unsure` · `Unrecognised`. `OrientationResult` wraps it with `Direction`/`Index`/
  `Code` helpers.
- **Candidate grid with occlusion colouring.** The window shows every candidate view with the
  resolved eyes drawn on each — green = visible from that view, red = masked (occluded).
  `EyeVisibility` casts the render's own ray through each eye's pixel and checks whether the
  eye's voxel is the frontmost hit.

## Merge reconciliation (2026-07-03)

master had independently reworked the same `ImageFacingDirection`: it added `Towards`/`Away`
(T/A) codes and an `UNSURE` outcome, modelling the result as
`(FacingDirection? Direction, OrientationOutcome enum, RawResponse)` via `Classify`. This
branch had turned the same type into the `OrientationAnswer` DU with a `ViewIndex` case.

Resolved by **unifying on the DU** (the shape it was meant to be): folded master's work in —
`OrientationAnswer` gained `Unsure`, `Facing` now covers T/A, `Classify` returns the union,
and the redundant `OrientationOutcome` enum was removed (`ImageOrientationWindow` switches on
the union). master's T/A/UNSURE system prompt is kept alongside `SelectViewAsync`/`ParseIndex`.

## Usage

```csharp
var model = VoxReader.Read(File.ReadAllBytes(voxPath));
var options = new EyePlacementOptions { EyeCount = 2 }; // AutoOrient on by default
EyePlacementResult result = await EyePlacer.PlaceAsync(apiKey, model, options);
foreach (var eye in result.Eyes)
    Debug.Log($"{eye.Position} facing {eye.Normal}");

// No key / deterministic fallback:
EyePlacementResult geo = EyePlacer.PlaceGeometric(model, options);
```

Vision path needs an Anthropic API key (shared `EditorPrefs` key with the other generation
windows). `AutoOrient = false` uses a fixed `View` instead of detecting the front.

## Verification

- `Tools/check-compile.sh` — clean (0 errors/warnings in this code).
- `Tools/run-tests.sh Assembler.AssetGeneration.EyePlacement.Tests` — **22/22** (raycaster,
  projection round-trip, geometric symmetry, candidate-yaw/index resolution, the DU accessors +
  `ParseIndex`, mesh face-culling, and occlusion visible/masked).
- The camera render can't run under headless tests (no GPU), so it's covered by the CPU
  fallback plus the pure-logic tests. **Eyeball the actual render quality in the editor.**

## Known limitations / next steps

- **Candidate thumbnails are the 512px detection renders** (what the model saw), lower-res than
  the final placement render. Fine for judging position/occlusion; re-render at `ImageSize` if
  you want full-res thumbnails (costs 8 more high-res renders/run).
- **Orientation resolution is 360/`OrientationViewCount`** (default 8 → 45°, so the chosen front
  can be up to ~22° off-axis). A cheap refinement is a second, narrower ring around the winner.
- **Auto-orient adds one vision call** (the multi-image selection sends `OrientationViewCount`
  images). Drop the count to 4 for 90° resolution / fewer tokens.
- **Camera render is URP-specific** (`SubmitRenderRequest` + `Assembler/VertexColorUnlit`); the
  CPU fallback covers non-URP/headless but at lower fidelity.
- The **geometric fallback is naive** on unusual shapes (multiple heads, a creature lying down).

## For the maintainer

The worktree for this branch was removed after the PR; the branch was updated on the remote
(merge + this doc). In your main checkout, `git pull` to fast-forward before running in Unity.
