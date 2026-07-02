# Handoff — Mesh → Stylised Voxel Spike

Continuation notes for picking this up in a fresh (local) Claude Code chat. Everything below
reflects the state of branch **`claude/new-feature-impl-t6pw16`** (PR **#413**), through commit
**`cba9a5d`**.

> This document supersedes the original `HANDOFFmeshtovoxelspike.md`. It preserves all of that
> earlier content and folds in a second work session (SDF compute-mode fix, `.vox` export, and a
> boxiness/geometry investigation). Statements the second session made obsolete have been updated
> inline; brand-new material is called out in **Session 2** sections.

## What this is

A new, **Editor-only, isolated** mesh→voxel pipeline that turns a messy Meshy mesh into a
**Crossy-Road-style blocky voxel model** (chunky, flat-shaded, limited palette), plus a smooth
comparison mesh. The existing `Window > Voxels > Mesh to Voxels` path is untouched — this is a
side-by-side A/B experiment.

Everything lives under `Assembler/Assets/AssetGeneration/MeshToVoxelSpike/` in a new assembly
`Assembler.AssetGeneration.MeshToVoxelSpike` that references the existing
`Assembler.AssetGeneration.MeshToVoxels` assembly (for the OBJ/FBX importer, `TextureSnapshot`,
`OklabColor`, `DefaultMasterPalette`, `VoxMasterPalette`, and — new in Session 2 — the `.vox`
writer `VoxWriter` + `VoxResult`/`VoxCell`) and the auto-referenced **geometry3Sharp (`g3`)** DLL.

Menu to run it: **`Window > Voxels > Mesh to Voxel Spike`**.

## The core idea (plain terms)

1. **Shape** — sample a signed distance field / winding number onto a **coarse** grid → clean blocky
   silhouette that ignores interior junk. Coarse = the stylised chunky look.
2. **Colour** — for each voxel/vertex, find the **nearest point on the original mesh → its UV →
   texture colour**, then flatten to a small palette. (Not the old "average texels into a voxel"
   approach.)
3. Show **every intermediate stage** laid out in the scene for judgement.
4. **(Session 2) Export** — optionally write the blocky occupancy grid out as a MagicaVoxel `.vox`.

## Files (all under `Assembler/Assets/AssetGeneration/MeshToVoxelSpike/`)

Pipeline (UI-free):
- `VoxelGrid.cs` — dense boolean occupancy grid + world placement (origin, cell size). `Index`,
  `InBounds`, `Center(x,y,z)`, `OccupiedCount`.
- `SdfIsosurface.cs` — builds the g3 SDF + marching-cubes isosurface + occupancy grid. **Key details
  below (incl. the Session 2 compute-mode fix).**
- `FeatureAwareDownsample.cs` — optional: voxelise finer, collapse `factor³` blocks by coverage
  vote, force-keep thin features via a bounded-erosion thickness map (geometric only).
- `TaubinSmoother.cs` — volume-preserving λ/μ umbrella smoothing of the isosurface (returns a copy).
- `SurfaceReprojection.cs` — optional Newton reseat of smoothed verts onto iso=0 along the SDF
  gradient.
- `ColourReprojector.cs` — nearest-triangle → barycentric UV → `TextureSnapshot.SampleBilinear`,
  per smooth vertex and per blocky voxel; optional wrong-side reject (off by default).
- `ColourModes.cs` — Raw / PerModelPalette (deterministic k-means in Oklab) / MasterPalette snap
  (reuses `OklabColor` + `PaletteSnap`'s chroma-gain penalty).
- `BlockyVoxelMesher.cs` — face-culled flat-colour cube mesh (the primary output). Colours are
  indexed by `VoxelGrid.Index`.
- `G3MeshConversion.cs` — g3 `DMesh3` ↔ Unity `Mesh` (carries `Color32[]`), + original-mesh preview
  builder.
- `SpikeSettings.cs`, `SpikeStageResult.cs` — settings bundle + result bundle. **(Session 2)**
  `SpikeStageResult` now also carries `Occupancy` (`VoxelGrid`) and `VoxelColours` (`Color32[]`,
  indexed by `VoxelGrid.Index`) so the `.vox` exporter can write exactly what the preview shows.
- `SpikePipeline.cs` — orchestrates the whole thing (synchronous, main-thread; builds Unity meshes).
- `SpikeVoxExport.cs` — **(Session 2, new)** turns the blocky occupancy grid + per-voxel colours
  into a `MeshToVoxels.VoxResult` and writes it via the shared `MeshToVoxels.VoxWriter`.
- `IsExternalInit.cs` — shim so `init` setters compile in this Unity/netstandard target (one per
  assembly; the one in `MeshToVoxels` is internal to that assembly and not visible here). **Added by
  the user locally — keep it.**

Editor / presentation:
- `MeshToVoxelSpikeWindow.cs` — `EditorWindow`, file picker, `EditorPrefs` persistence, guarded
  `async void` convert. All shape/colour options as live toggles. **(Session 2)** `Convert` now
  takes an `export` bool; two buttons drive it — **Convert** (preview only) and **Convert & Save
  .vox…** (preview + write `.vox` to a user-picked path, then `AssetDatabase.Refresh` if it lands
  under `Assets/`).
- `SpikeStagePreviewer.cs` — instantiates each stage as a GameObject laid out along +X with labels
  under a parent `"MeshToVoxelSpike Preview"`; clears the prior preview each run.
- `VertexColorUnlit.shader` — tiny URP unlit, **`Cull Off`**, vertex-colour × `_BaseColor`. All
  previews use this one material (grey intermediates tint via `_BaseColor`), so winding/culling
  never matters.

Tests:
- `Tests/SpikePipelineTests.cs` (+ asmdef, csc.rsp) — light EditMode checks: unit-cube occupancy /
  non-empty isosurface / per-voxel reprojected colour, and colour-mode logic.

## IMPORTANT: geometry3Sharp API pitfalls already hit (and how)

The first session was written in a cloud environment **with no Unity and no .NET/mono**, so it could
not be compiled there. Several g3 API guesses were wrong and were fixed reactively from the user's
compile errors. **When diagnosing further g3 errors, do NOT trust the general/public g3 docs — this
exact DLL build differs. Verify against the actual DLL and the existing working usage in
`Assembler/Assets/AssetGeneration/MeshToVoxels/ObjToVoxConverter.cs`.**

The g3 DLL is at:
`Assembler/Packages/nuget-packages/InstalledPackages/geometry3Sharp.1.0.324/lib/netstandard2.0/geometry3Sharp.dll`
(You can't easily reflect over it without a .NET runtime; grepping strings is misleading because the
metadata #Strings heap shares string *suffixes* — e.g. `DenseGridTrilinearImplicit` doesn't appear
as its own token because it's a suffix of `CachingDenseGridTrilinearImplicit`. That said, a plain
`strings | grep` **is** enough to confirm a *field/enum name exists* — that's how the Session 2 fix
below was verified.)

Fixes made so far (all now on the branch):
1. **`MeshSignedDistanceGrid.InsideModes` in this build has only `CrossingCount` and `ParityCount`**
   — NO `WindingNumber` and NO `AnalyticWindingNumber`. (Earlier commits tried both and failed.)
   Current code uses `ParityCount` for the signed grid.
2. **Use `DenseGridTrilinearImplicit(DenseGrid3f, Vector3d, double)`**, NOT
   `CachingDenseGridTrilinearImplicit` (whose ctor is `(Vector3d, double, Vector3i)`).
3. Because there's no winding-number inside-mode, **occupancy is signed by
   `DMeshAABBTree3.FastWindingNumber(Vector3d)` at each SDF grid node** (`|wn| > 0.5`, matching
   `ObjToVoxConverter`), independent of the SDF's parity sign. `SdfIsosurface.Build(mesh, tree,
   maxDimVoxels)` now takes the built AABB tree. This keeps the primary blocky output robust to
   non-watertight / self-intersecting / inverted meshes; the parity SDF only feeds the smooth
   comparison mesh + reprojection gradient.
4. **(Session 2) `ComputeModes.NarrowBand_SpatialFloodFill` requires three fields set together, or
   `Compute()` throws at runtime** with:
   `MeshSignedDistanceGrid.Compute: must set Spatial data structure and band max distance, and
   UseParallel=true`. The first session set the compute mode + `UseParallel = true` but left
   `Spatial` and `NarrowBandMaxDistance` unset, so every conversion threw. Fix in `SdfIsosurface.cs`:
   pass the already-built AABB `tree` as `Spatial` and set `NarrowBandMaxDistance = cellSize *
   NarrowBandCells` (`NarrowBandCells = 3`). The mode computes exact distances only within that band
   and flood-fills the sign across the rest of the grid via the tree — which is all the downstream
   consumers need (marching cubes wants the iso-0 crossing; SDF reprojection only walks a vertex a
   cell or two). No extra cost: the tree was already built for the occupancy sign.

APIs confirmed working (Unity only ever flagged the issues above, so the rest compiled):
`MeshSignedDistanceGrid` fields `ComputeSigns / InsideMode / ComputeMode / UseParallel / Spatial /
NarrowBandMaxDistance`, method `Compute()`, props `Grid` (`DenseGrid3f` with public `ni/nj/nk` +
`[x,y,z]` indexer), `GridOrigin` (`Vector3f`), `CellSize`; `ComputeModes.NarrowBand_SpatialFloodFill`;
`MarchingCubes` fields `Implicit / Bounds / CubeSize`, `Generate()`, `Mesh`;
`DenseGridTrilinearImplicit.Value(ref)/Gradient(ref)/Bounds()`; `AxisAlignedBox3d.MaxDim/.Expand(double)`;
`DMesh3` iteration/accessors; `DMeshAABBTree3(mesh).Build()/.FindNearestTriangle(Vector3d)/
.FastWindingNumber(Vector3d)`; `DistPoint3Triangle3(p, Triangle3d).GetSquared()/.TriangleBaryCoords`;
`Vector3d.Zero`, `.LengthSquared`, operators (no `.Dot()/.Cross()` — those are done with explicit
components).

If a NEW g3 error appears: check whether the same call already works in `ObjToVoxConverter.cs`
(that file is the ground truth for what this DLL supports), and prefer those exact patterns.

## Session 2 — `.vox` export

Goal: take the blocky output "all the way" to a MagicaVoxel `.vox` file, not just Unity preview
meshes. (This reverses the original "No `.vox` export" non-goal.)

- The blocky mesh is built purely from the occupancy grid + a `Color32[]` of flat per-voxel colours
  (indexed by `VoxelGrid.Index`). Session 2 surfaces both of those on `SpikeStageResult`
  (`Occupancy`, `VoxelColours`) — they were already computed in `SpikePipeline.Run`, just not
  exposed.
- `SpikeVoxExport.Write(path, grid, colours)` walks the occupied cells into a `List<VoxCell>`,
  wraps them in a `VoxResult(NX, NY, NZ, cells)`, and hands it to the **existing**
  `Assembler.AssetGeneration.MeshToVoxels.VoxWriter.Write(path, result)`.
- **Deliberately reused `MeshToVoxels.VoxWriter` rather than writing new `.vox` code**, because it
  already handles: palette build (exact ≤254 distinct colours, else median-cut quantisation), the
  **reserved palette byte-1 slot** (the Voxel Toolkit importer renders palette index 0 invisible —
  real colours must start at index 2; see the memory note "Voxel Toolkit importer material 0 is
  transparent"), and the g3→MagicaVoxel axis remap. So the export matches the orientation and
  colour handling of the existing `Window > Voxels > Mesh to Voxels` path.
- Coordinate frame: the spike's occupancy grid axes (x, y=up, z) line up with what `VoxWriter`
  expects (mesh Y-up grid space), because both grids are built from the same g3 mesh's bounds. So
  occupancy `(x,y,z)` maps straight to `VoxCell(x,y,z)` and `VoxWriter` does its own up-axis remap.
- Size limits: grid dims are capped at Max-dimension ≤ 96 here, well under the 256-cell `.vox`
  coordinate limit and the 254-colour exact-palette limit — no overflow risk in practice.

There are **two** `VoxWriter` classes in the project — `Assembler.Voxels.VoxWriter` (takes a
`VoxelModel`, returns `byte[]`) and `Assembler.AssetGeneration.MeshToVoxels.VoxWriter` (takes a path
+ `VoxResult`, writes to disk). The spike uses the **MeshToVoxels** one. `SpikeVoxExport` only
`using`s the MeshToVoxels namespace, so there's no ambiguity.

## Session 2 — boxiness / Crossy-Road geometry (investigation only, NOT implemented)

The user observed the geometry is "a bit messy" — Crossy Road has a distinct boxy style with few
corners / little diagonal stair-stepping — and asked whether existing parameters can coerce boxier
shapes or whether it's a new feature. **Conclusion: mostly a new feature.** Findings:

- The blocky output's shape comes **purely from the occupancy grid**. Only two settings feed it:
  - **Max dimension (voxels)** — the primary lever. Fewer voxels ⇒ fewer faces/corners and less
    stair-stepping. Recommend trying **~10–16**, not the default 24.
  - **Feature-aware downsample + coverage threshold** — secondary. A *high* coverage threshold
    (~0.6–0.75) trims jagged one-voxel slivers on diagonal surfaces. Caveat: this stage hard-codes
    `forceThinFeatures: true` in `SpikePipeline.cs`, which deliberately *preserves* thin spikes
    (legs/antennae) — the opposite of boxy — so it fights you on spindly models.
- **Red herrings** (do NOT affect the blocky output): Taubin passes/λ/μ and SDF surface
  reprojection only affect the *smooth comparison* mesh; colour mode/palette/normal-consistency are
  colour-only.
- Stair-stepping on diagonals is **inherent** to voxelising a smooth surface at a given resolution;
  none of the current knobs remove it. Crossy Road avoids it by being low-res **and** hand-authored
  as axis-aligned boxes.
- **Proposed new feature (not built):** a **morphological close→open pass** on the occupancy grid
  before meshing (close = dilate-then-erode fills one-voxel notches → flatter faces; open =
  erode-then-dilate removes lone bumps → cleaner silhouette), paired with **floater removal** (drop
  disconnected specks). Expose as a "Cleanup / smoothing strength" slider (0 = off). The
  erosion/neighbour logic already exists in this spike (`FeatureAwareDownsample.ThicknessMap` /
  `IsInterior`), and `MeshToVoxels` has a `Morphology` stage to mirror — so it's a small,
  self-contained add. **Recommended next step if lowering resolution + raising coverage isn't
  enough.**

## Current status

- All known compile errors fixed. **Session 2 also fixed the first runtime blocker** (the
  `NarrowBand_SpatialFloodFill` exception), so conversion now runs end-to-end and the user has
  produced blocky output + `.vox` files.
- Last pushed commit: **`cba9a5d`** (SDF spatial-floodfill wiring + `.vox` export).
- Headless compile/test scripts were **not** run for the Session 2 changes because the branch is
  checked out in the user's open Unity editor (the `Tools/*.sh` scripts refuse a path an open editor
  holds — see memory "validate-when-editor-holds-branch"). The changes are small and type-checked by
  hand. The user's open editor is the compile authority.
- No `.meta` files are hand-authored (Unity generates them). Don't create `.meta` files. (Note: many
  spike `.meta` files show as untracked in `git status` — the first session committed the `.cs` files
  without their metas. This handoff doc lives at the **project root**, outside `Assets/`, so Unity
  does not generate a `.meta` for it.)

## How to continue / verify (locally, in Unity)

1. `git pull` on branch `claude/new-feature-impl-t6pw16`.
2. Let Unity import + compile. If errors, see the g3 pitfalls section — paste the `error CS####`
   lines with file:line.
3. Optional headless checks (each boots Unity in batch mode, slow; **close the editor on this branch
   first, or run from a detached worktree at a different path**): `Assembler/Tools/check-compile.sh`,
   `Assembler/Tools/run-tests.sh`.
4. Real test: `Window > Voxels > Mesh to Voxel Spike` → pick a textured Meshy `.obj`/`.fbx` →
   **Convert** (preview) or **Convert & Save .vox…** (preview + write file). Judge the blocky output
   on (a) does it capture the shape essence / read as clean Crossy-Road, (b) do the reprojected
   colours beat the old `Mesh to Voxels` output, (c) does the exported `.vox` open correctly (e.g.
   in MagicaVoxel / the Voxel Toolkit importer) with colours intact.

## Things to watch (runtime, not compile)

- **Occupancy sign**: uses `|wn| > 0.5`. If the blocky model comes out hollow or inside-out on some
  mesh, the winding-number threshold or the tree is the place to look.
- **SDF narrow band**: `NarrowBandMaxDistance = 3 cells`. If SDF surface reprojection ever nudges a
  vertex *past* the 3-cell exact band, the gradient there is flood-fill fill, not exact — widen
  `NarrowBandCells` if reprojection misbehaves. (Marching cubes and occupancy are unaffected.)
- **Colour orientation**: if reprojected colours look vertically mirrored, flip UV `v` → `1 - v` in
  `ColourReprojector` (same caveat as the existing converter). This would also affect the `.vox`
  colours, since export reuses the same per-voxel colour array.
- **`.vox` orientation / mirroring**: export goes through `MeshToVoxels.VoxWriter`'s documented
  g3→MagicaVoxel remap (a proper rotation, not a mirror). If an asymmetric model reads mirrored in a
  `.vox` viewer, suspect the source FBX's own handedness, per that writer's comments — not the spike.
- **Coordinate frame**: g3 is right-handed; meshes go into Unity as-is (no Z-flip) and previews
  render `Cull Off`, so a global Z-mirror vs "reality" is possible but consistent across all stages
  and irrelevant for the A/B preview.

## Non-goals (deferred)

Surface Nets / dual contouring, xatlas atlas baking, and batch/CLI mode remain deferred until the
approach is validated. (`.vox` export is **no longer** a non-goal — done in Session 2. The
morphology/boxiness cleanup pass is designed but **not yet implemented** — see the boxiness section.)

---

## Session 3 — consistently-good Crossy-Road output (the big drop)

Session 3 implements the full "consistently good output" plan agreed in a /grill-me interview:
scored grid-placement search, connectivity-gated thin-feature keep, floater removal, protected
morphology, UV island dilation, multi-sample medoid colour, Potts label smoothing, objective
metrics, and a test-set batch runner. Everything is individually toggleable in the window.
Statements above that this section supersedes: the boxiness cleanup is now **implemented** (as
rank morphology, see below), `FeatureAwareDownsample.cs` is **deleted**, and batch mode exists
(in-editor "Run test set…", still no CLI).

### New pipeline order (SpikePipeline.Run)

load → **UV island dilation** (toggle) → AABB tree → resolve resolution (slider | worldSize÷voxelSize,
clamped 4–96) → SDF at `maxDim × f` (f = FineFactor 2–4 when search/thin-keep on, else 1) →
**FineGridAnalysis** (once: thickness map, components + main mask, air-gap mask, 4 IntegralVolumes) →
**GridPlacementSearch** (toggle; off = identity candidate through the same voting code) →
**floater removal** (toggle) → **protected close→open** (strength 0–2) → **multi-sample medoid
colour** (toggle; off = single centre sample) → `ColourModes.AssignPalette` (palette + labels) →
**PottsLabelSmoother** (strength knob, 0 = off) → blocky mesher → smooth comparison path
(unchanged) → **SpikeMetrics** on the final grid.

### New files (all pure/UI-free except the last two)

- `IntegralVolume.cs` — 3D summed-volume table, `BoxCount` = 8 lookups, bounds-clamped.
- `FineGridAnalysis.cs` — thickness map (moved verbatim from FeatureAwareDownsample), 6-connected
  components + main mask, air-gap mask (empty cell with occupied cells ≤2 away on BOTH sides along
  some axis), occupied bbox, and integrals over occupancy/thick/gap/main.
- `GridPlacementSearch.cs` — Candidate = per-axis phase ∈ [0,f) × per-axis floor/ceil voxel-count
  scale over the occupied bbox (deduped, stretch clamped ±10%; ≤(2f)³ candidates). One voting
  implementation (`Materialise`): per block via integrals, `thinKeep = occ>0 ∧ thick==0 ∧ main>0`
  (the main>0 connectivity gate is NEW vs the old downsample), `filled = thinKeep ∨ occ/vol ≥
  coverage`; block boundaries round to whole fine cells (≤½ fine cell error under flex).
  Score = wFace·S_face + wIou·S_iou + wGap·S_gap (defaults 1/1/2) + optional wCol·S_col (plumbing
  shipped, weight 0, costly). Ties: score → smallest phase sum → scale nearest 1 → first enumerated.
  Phase-2 fallback (SDF sample-position warp) is a comment only.
- `OccupancyCleanup.cs` — shared component labelling, floater removal (drop coarse components whose
  fine support never touches the fine main component; keeps the largest if nothing does), and
  close→open morphology. **Deliberate deviation from the plan's wording**: implemented as RANK
  morphology (close = fill empty cells with ≥5 occupied face-neighbours, ≥4 on pass 2; open = shave
  occupied cells with ≤1, ≤2 on pass 2) rather than structuring-element close→open, because SE
  open chamfers every box edge/corner (erode a cube by the 6-cross and dilate back — the 12 edges
  never return), which would wreck exactly the axis-aligned models (chair/house) the test set uses
  as "must be perfect" rows. Same knob (strength 0–2), same guards: close never fills cells with
  fine gap fraction > ¼, open never shaves the protected mask (thin-kept blocks dilated by 1), and
  a reconnect net BFS-bridges any split through the union of pre/post cells (restores a minimal
  path; pieces that were already separate are left alone).
- `UvIslandDilation.cs` — texel-centre-in-UV-triangle coverage rasterisation (GetTriangle/
  GetVertexUV only) + iterative 8-neighbour toroidal flood (default 8 passes) → new TextureSnapshot
  via its public ctor; rebuilt LoadedModel. No-op without texture/UVs.
- `MultiSampleColour.cs` — centre + 4 deterministic jittered samples per exposed face (±0.3·cell in
  the face plane), all through the shared `ColourReprojector.SamplePoint`; Oklab-medoid aggregate
  (`OklabMedoid`, a separate pure function). Interior voxels: single centre sample.
- `PottsLabelSmoother.cs` — ICM on the 6-adjacency, labels only; unary = Oklab dist² from the
  voxel's SAMPLED (pre-palette) colour to each palette entry; pairwise = β·exp(−(d_uv/σ)²) with
  σ=0.08 over SOURCE-colour difference (strong real edges attenuate to ~0 → pinned);
  β = strength · mean(secondBestU − bestU) so the knob is model-independent. Sweeps in index order,
  ≤10 or until stable; strength 0 short-circuits.
- `SpikeMetrics.cs` — voxel/face/floater/colour counts on the FINAL grid + chosen phase/scale/
  candidate count + score terms; `ToLogString`, `CsvHeader`/`ToCsvRow`.
- `SpikeTestSetRunner.cs` (editor) — batch a folder (.obj/.fbx, non-recursive, name-sorted), one
  preview row per mesh stacked along +Z, metrics table log, CSV returned for the window's
  Copy-CSV button. Per-mesh failures are logged and skipped.
- Tests: `GridPlacementSearchTests.cs`, `OccupancyCleanupTests.cs`, `SpikeColourPassTests.cs`
  (the last includes a full-pipeline smoke test that writes a temp cube .obj).

### Changed files

- `ObjToVoxConverter.cs` — additive only: `TextureSnapshot.Width/Height/CopyLinearPixels()`.
- `ColourReprojector.cs` — private `Sample` → internal `SamplePoint` (shared with MultiSampleColour).
- `ColourModes.cs` — refactored to `AssignPalette(...)` returning `PaletteAssignment`
  (Colours + Palette + per-entry Labels, −1 invalid; Palette/Labels null for Raw); `Apply` now
  delegates so the old tests stay green.
- `SpikeSettings.cs` — rewritten: ResolutionInput (slider|world-size) + `ResolveMaxDimVoxels()`
  (clamped 4–96), GridSearch/ScaleFlex/ThinFeatureKeep (replaces the hard-coded
  `forceThinFeatures`)/FineFactor/`ResolveFineFactor()`/Coverage/RemoveFloaters/CleanupStrength,
  score weights, UvDilate(+passes)/MultiSampleColour/PottsStrength, plus `Defaults`.
- `SpikeStageResult.cs` — added `Metrics`.
- `SpikePipeline.cs` — reorchestrated per the order above.
- `SpikeStagePreviewer.cs` — `Show` extracted into public `BuildRow` for the batch runner.
- `MeshToVoxelSpikeWindow.cs` — all new toggles/sliders (advanced weights in a foldout), metrics
  panel with Copy CSV / Log buttons, "Run test set…", EditorPrefs for every knob, fine-grid-size
  warning above ~120 nodes on the longest axis.
- `FeatureAwareDownsample.cs` — **deleted** (thickness map lives in FineGridAnalysis; the vote is
  GridPlacementSearch's identity path).

### Gotchas found in Session 3

- **C# version**: these asmdefs compile at C# 9 (`csc.rsp` is just `-nullable:enable`) — `init`
  works via the `IsExternalInit` shim, but `with`-expressions on structs (C# 10) do NOT compile.
  No records (per the existing style note).
- **`Mathf.RoundToInt` banker's-rounds** (.5 → nearest even) — block boundaries use
  `FloorToInt(v + 0.5f)` instead so ½-offsets tile deterministically.
- Candidate widths are floor/ceil ONLY (not ∪ {unstretched}): a third per-axis width option cubes
  the candidate count (1728 at f=4) and floor/ceil brackets the unstretched width anyway.

### Status / verification

- All EditMode tests written; the user's open editor is the compile/test authority (Tools/*.sh
  can't run against a checkout an open editor holds — use a detached temp worktree if headless
  checks are needed).
- Acceptance = the user's "Run test set…" over the locked Meshy test-set folder (12-model spread
  from the plan; minimum viable subset: dog, crab, rocket, oak tree), tuning geometry toggles
  first, then Potts strength, with the metrics table alongside.
