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

## Roadmap & known issues

All accuracy findings, the failure taxonomy, per-model human ground truth, and the ordered roadmap live in **GitHub umbrella issue [#483](https://github.com/callum-rose/Assembler/issues/483)** — read it before starting any new eye-placement work rather than re-deriving from old temp handoffs. Sub-issues: **#479** 3D eval harness (landed, PR #485), **#480** reprojection rework (the bottleneck — next task), **#481** head zoom (shelved), **#482** transparent render fix. The module is merged into `master` (PRs #438 + #458), so branch new work from `master`.

### Evaluate placement in 3D, never by a 2D hit-rate

**Do not judge placement quality with a 2D pick-in-region hit-rate, and don't trust a visualisation that reprojects the 3D anchor back into the single placement view.** An eye seated on the ears/top/back/wrong-side still reprojects *near the correct 2D spot* under the pitched camera, so 2D scoring reads ~87% while true 3D placement is ~0%. The head-zoom spike concluded "it works" from 2D metrics; inspecting the actual anchors in 3D found ~0/14 correct (eyes on ears, on the back, both on one side, on the snout).

Judge on the resolved 3D `EyeAnchor`s rendered on the model **from several angles** (or against 3D-annotated eye voxels). The #479 harness does exactly this: `EyePlacementScorer` (within-N of ground truth + on a surface voxel + normal-not-up), `EyeGroundTruth` (`<name>.eyes.json` sidecar alongside the untracked `.vox`), `EyeMontage` (yaw-ring contact sheet), and `EyePlacementEvalBatch.Evaluate` (run **without** `-nographics` for the GPU render).

The real bottleneck is **`EyeReprojection.BuildAnchors`**, not the 2D pick: it snaps to top/upward faces under the pitched-down view, mirrors across the *view* axis instead of the model's body axis (both eyes on one side on 3/4 views), and can't place side-eyed creatures from a front pick. Head-zoom improves the 2D pick, which is *not* the limiting stage — hence shelved (#481).

### Render substrate: crisp isometric GPU, not a rough CPU splat

The "paint eyes with an image model → diff → centroids" spike (2026-07-04) compared render configs. The verdict reversed an earlier "never use isometric": on a **crisp GPU/URP render, isometric is the strongest substrate** — it matches flat views elsewhere *and* uniquely cracks the hardest small-head case (a 3/4 view makes a tiny head an unambiguous protruding block, so the model places eyes on it instead of redrawing the head). The earlier "isometric floods" result was a **rough-CPU-render artifact** (the image model re-renders an aliased splat wholesale), not the view. So **feed the editor the crisp isometric render, never a rough CPU splat.** The remaining gap there is a *detector* fix (a shape-based "keep the 2 most eye-like clusters" selector to reject secondary muzzle/beak edits), which runs on already-captured images — not a placement problem.
