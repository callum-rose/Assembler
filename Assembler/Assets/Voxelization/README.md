# Voxelization — image/brief → rigged voxel model pipeline

LLM-driven generation of part-based voxel models. Input: a one-line game brief (and optionally reference images); output: validated, rigged `.vmodel.yaml` models plus `.vox`/Goxel/PNG exports, generated as a consistent *set* (shared scale, shared style). Everything LLM-facing is deterministic-gated: a plan that cannot possibly produce a correct model is rejected by code before any authoring spend.

Run it from **Assembler > Voxel Set Review** (editor window). Tests: `Tools/run-tests.sh Tests.Voxelization`.

## Conventions (load-bearing — read first)

- **Y-up, z forward.** x = right, y = up, z = forward *towards the viewer*. The subject FACES +z (face, beak, headlights on the high-z side). Matches Unity's `Transform.forward`.
- **Bounding box**: `height` = y, `length` = z (nose-to-tail), `width` = x (left-right). A car is longer (z) than wide (x). Enforced per-asset within `tolerance` (± voxels, default 1).
- **`unit` is always 1** in generated manifests; all dimensions are voxel counts. (Legacy metres-style manifests still resolve via `SetManifest.HeightInVoxels`.)
- **Ground**: lowest geometry sits at y=0 (`origin: feet_center`).
- **Bilateral symmetry** mirrors across the x=0 plane: centre parts have odd `size.x` straddling x=0; side parts are authored once and twinned with `mirror:` parts.
- **Z-up storage boundary**: `.vox`/Goxel exports swap y/z in `ModelExporter` via `VoxelGridConvert.SwapYZ` — nowhere else. All prompts and in-memory data are Y-up.

## Pipeline

```
game brief ──0──▶ manifest ──1a─▶ reference brief ──1b─▶ plan ──2──▶ authored parts
                                  (only with image)        │            │
                                                     plan gates    ┌────▼────┐
                                                     (reject+retry)│ assemble │──▶ validate ──▶ re-author loop
                                                                   └─────────┘         │
                                              re-plan ◀── corrections ◀── 3: review ◀──┘ (clean or exhausted)
                                                                           │ OK
                                                                           ▼
                                                                        export
```

| Stage | Class (`Pipeline/`) | What it does |
|---|---|---|
| 0 manifest | `ManifestGenerator` | Brief → set manifest: one asset per distinct *thing*, full bounding box + binding `description` per asset. Normalizes `unit` to 1. |
| 1a brief | `BriefExtractor` | Vision transcription of a reference image into a locked palette + front silhouette (`ReferenceBrief`). Deliberately separate from planning so the transcription can't be bent to fit a design. Trims margins, symmetrizes bilateral silhouettes. Skipped when the asset has no `reference:`. |
| 1b plan | `ModelPlanner` | One call → part skeleton (`PlannedPartData` per part). Parsed plan is re-anchored to manifest-owned facts (id, scale, box, symmetry, description) then run through **plan gates**; failures are fed back, max 3 attempts. |
| 2 author | `PartAuthor` | One call per planned part: `layers` (ASCII fence), `primitives` (shape-line fence), or `script` (VoxelBuilder tool loop). Decoded immediately so format errors retry in-call. Mirrors/copies are free. |
| — assemble | `Assembly/ModelAssembler` | Pure code: decode each part to a grid, accumulate pivots down the tree, compose one volume. Problems become `ValidationIssue`s, not throws. |
| — validate | `Assembly/ModelValidator` | Pure code: bounding box vs targets, bilateral symmetry (whole-model IoU + per-centre-part cell-exact), connectivity (floating chunks; child touches parent), palette legality, declared-size overflow, brief palette/silhouette IoU. |
| re-author | `SetOrchestrator` | Failing parts re-authored with the issue text + colour-keyed ASCII views (front/side/top; +back if not bilateral) of what was actually built. `MaxValidationRounds` per plan. |
| 3 review | `SetOrchestrator.ReviewAsync` | One vision call: built views + measured-vs-target dimensions + reference image. "OK" or numbered corrections → full **re-plan** (shape problems live in plan-owned sizes/pivots). `MaxReviewRounds`. |
| export | `Assembly/ModelExporter` | `<id>.vmodel.yaml` (the full generation source — rebuildable), `reference_brief.yaml`, composed `.vox` + `.goxel.txt`, front/iso preview PNGs, per-part `.vox` for rigged models. |

`SetOrchestrator` drives all of it; assets in a batch run in parallel and auto-export the moment they finish into a per-run subfolder (`<timestamp>-<descriptive>/<asset>/` + `session.log`). The descriptive tail is one short LLM call (`RunFolderNamer`, stage `0-name`) that slugs the manifest into a folder name like `2026-06-12-143501-pirate-cove-props`; the date/timestamp is always kept in front and a naming failure falls back to `run-<timestamp>`.

## Plan gates (deterministic, pre-authoring)

`Pipeline/PlanGeometryChecks` rejects any plan that re-authoring could never fix:

- declaration order (parents, mirror/copy sources first)
- bounding-box spans (y always; z/x when targeted) within tolerance; lowest box at y=0
- bilateral geometry: odd-width centred centre parts, mirror twins at exactly reflected pivots, x-axis mirrors only
- palette ⊆ locked brief palette (in `ModelPlanner.TryParse`)
- silhouette feasibility: the union of part boxes must cover ≥80% of the reference silhouette and match its width ±1 (`SilhouetteFeasibilityError`)

## Formats (`Format/`)

- **Manifest** (`SetManifest`/`ManifestYaml`): `game`, `unit`, per asset `id`, `description` (binding theming distilled from the brief), `height`/`length`/`width`, `tolerance`, `symmetry`, `rig`, optional `reference` (image filename, resolved by `IReferenceImageSource` against the window's image folder; missing file silently degrades to no-image).
- **Model** (`VoxelRigModel`/`VModelYaml`, `*.vmodel.yaml`): palette (single-char keys, `_` empty), part tree, poses. Part pivots are parent-local; a part's grid cell (0,0,0) sits at its `offset` in pivot-local space.
- **Part encodings** (`PartData`):
  - `layers` — ASCII slices bottom-to-top (`LayersCodec`); layer = size.z rows of size.x palette keys.
  - `primitives` — shape lines (`PrimitivesCodec`): `box KEY min size [round R]`, `sphere KEY c r [half ±axis]`, `cylinder KEY axis base r h [half ±axis]`. Fractional centres/radii; later lines overwrite; clipped to the window.
  - `script` — restricted-C# VoxelBuilder body (`Assembler.Voxels.Scripting`); built grid is palette-remapped and snapped into the declared window (`FitToWindow`).
  - `mirror: {source, axis}` — reflected copy (p → −p on the axis); pivot derivable.
  - `copy: {source}` — verbatim prefab-like reuse at the copy's own pivot (four identical wheels = author one).
  - `planned` — stage-1 placeholder carrying encoding/size/offset/note.
- **Reference brief** (`ReferenceBrief`/`ReferenceBriefYaml`): palette, front silhouette rows (`#`/`.` — but any non-`.` char counts as solid, see `SilhouetteSpec.IsSolid`), proportions, signature features.

## Prompts & knobs

- All Claude-facing text lives in `Pipeline/VoxelizationPrompts` — one place; coordinate/facing doc is a shared constant.
- `Pipeline/VoxelizationConfig`: per-stage model ids, retry caps, thresholds, part voxel budget (oversized layers demote to script), and `StyleGuidance` — operator free-text injected into planning/authoring/review (not manifest gen, not the brief extractor, which must stay an honest scan).
- API plumbing: `IAnthropicGateway` (streaming activity + `TokenUsageTracker` per stage) wrapping `Assembler.Anthropic`.

## Editor & runtime

- `Editor/VoxelSetReviewWindow` — the operator console: brief → manifest → parallel batch, live per-asset gallery (dimensions/voxel/part counts, previews), refine/regenerate per asset, style guidance, per-stage model dropdowns, token spend, log.
- `Runtime/RigInstantiator` + `VoxelMeshBuilder` — build a GameObject tree (one child per part, pivots as localPosition) from an exported model at runtime.
- `Assembly/VoxelProjector` (ASCII/occupancy/colour projections), `VoxelPreviewRenderer` (front + top-down dimetric iso PNGs).

## Gotchas

- The whole left-hand path is *per asset*; one asset failing never aborts the batch — it lands in the gallery as Failed/NeedsReview.
- A run is **not resumable mid-asset** (orchestration state is in memory); finished assets survive via auto-export.
- Vision transcription is the weakest link: gates are robust to *sloppy* briefs (charset, margins, blobbing) but not *confidently wrong* ones (miscounted widths).
- `ScriptPartData` must be deterministic (no RNG) so re-runs reproduce the part.
- Tests live in `Assets/Tests/Voxelization` (`Tests.Voxelization` assembly), fixtures around `VillagerFixture`.
