# Voxel Post-Processing Pipeline — Design & Handoff

> Status: **design agreed, not yet implemented.** This document is a self-contained
> spec for an implementer (human or AI) picking this up cold. Read it top to bottom
> before writing code.

## 1. Context

This lives in the `Assembler.AssetGeneration.VoxelPipeline` editor-only module, which converts a textured
mesh (`.obj` / `.fbx`, from **Meshy**) into a coloured MagicaVoxel `.vox`. The module
already does:

- **Voxelization** — `ObjToVoxConverter.Convert(meshPath, maxDimVoxels, progress)` solid-fills
  the mesh into a grid using a fast-winding-number occupancy test and samples per-voxel
  colour from the texture (bilinear, nearest-triangle). Output is `VoxResult`.
- **Colour quantisation** — `ColorQuantizer.Quantise(VoxResult, Options)` extracts a small
  palette (coarse-binning + popularity + min-region gate) and snaps each voxel to it.
- **Writing** — `VoxWriter.Write(path, VoxResult)` emits a single-model `.vox`. It already
  writes an **exact palette verbatim when ≤255 distinct colours** (falls back to 3-3-2
  otherwise), so a small curated palette survives unaltered.

Key existing types (`ObjToVoxConverter.cs`):

```csharp
public readonly struct VoxCell { public readonly int X, Y, Z; public readonly Color32 Color; }
public sealed class VoxResult {
    public int GridX, GridY, GridZ { get; }
    public IReadOnlyList<VoxCell> Cells { get; }   // sparse: only filled cells
}
```

**Coordinate space:** `VoxCell` is in the **mesh's Y-up grid space** (X right, **Y up**, Z depth).
`VoxWriter` remaps to MagicaVoxel's Z-up at write time. **All pipeline steps operate in the
Y-up `VoxResult` space.** When this doc says "axis", it means an axis of that space.

Compile setup: this is a standalone editor asmdef (`Assembler.AssetGeneration.VoxelPipeline`) with
`-nullable:enable` (`csc.rsp`) and a local `IsExternalInit.cs` shim, so **records and
`init`-only setters work** here. Respect nullable annotations (house rule). No `.meta`
files by hand — Unity generates them.

This work is for a **generalised generative game engine** (Assembler). The voxel models are
static game assets in a **Crossy Road–style** aesthetic.

## 2. Goal & philosophy

**v1 is conservative cleanup. Fully local, synchronous, no AI, no network, no
human-in-the-loop, no segmentation.** One voxel model in → one cleaned `.vox` out.

Aesthetic target: **Crossy Road** — flat albedo (no baked lighting), a small clean palette,
hard colour steps, bright and cheerful.

Two governing principles that were settled deliberately (do not relitigate without reason):

1. **Preserve Meshy fidelity.** Meshy output matches the concept image closely; that is the
   asset's value. Steps must clean, not re-sculpt. Anything that idealises shape (forcing
   primitives, forcing symmetry) is **opt-in** and off by default, because it destroys
   intended detail/asymmetry.
2. **Keep v1 dumb and synchronous.** Every hard/fragile/expensive idea (segmentation,
   VLM, face features, articulation) was pushed out of this pipeline precisely to keep it
   shippable. Don't pull them back in.

## 3. Scope

### In scope (v1)
- Remove floating voxels
- Colour correction: **de-light** + **palette-snap to a hand-authored master palette** (anchored hybrid)
- Morphological despeckle/fill ("fix anomalous voxels", reduced to geometry — see §6.5)
- Optional symmetry: **mirror** about a plane, and **revolve** (axial symmetry) — both opt-in
- A composable step pipeline with toggles/params and per-category presets

### Explicitly OUT (cut, with rationale — do not implement in v1)
- **Part segmentation / "split into animatable parts"** — *cut entirely.* Too complex for the
  payoff. Animation is cartoony whole-model position/scale/rotate. Anything needing
  articulation is authored as **separate Meshy assets** ("Rayman hands") run through this
  same pipeline independently.
- **Eyes/faces as voxels** — *out.* Faces become floating **sprites** anchored to the model,
  handled **Assembler-side**, not here. The voxel pipeline emits nothing for them in v1.
- **VLM / any AI** — *out.* It only ever existed to serve parts/eyes; both are gone.

### Deferred (later, opt-in; design later)
- **Primitive regularisation** (snap a region to an exact sphere/cylinder) — only helps the
  hard-surface Meshy subset, is individually hard, and is destructive on organic models.
- **Duplicate-shape unification** — same reasoning.
- Vehicle-wheel handling beyond the standalone-revolve case (needed segmentation, now cut).

## 4. Architecture

### 4.1 Working model (`VoxModel`)
Introduce a richer mutable working context that steps operate on; keep `VoxResult` as the
thin export/serialisation DTO consumed by `VoxWriter`.

`VoxModel` should hold:
- A **dense occupancy grid** (`bool[x,y,z]` or a flat array with `[x + X*(y + Y*z)]` indexing)
  plus a parallel **colour grid** (`Color32[...]`). The existing sparse `List<VoxCell>` is the
  wrong shape for neighbour-heavy steps (floaters, morphology, revolve) — convert once on
  entry, convert back on export.
- Grid dims `X, Y, Z`.
- Scratch label buffers as needed by steps (e.g. an `int[]` component-id buffer for floaters,
  an `int[]` region-id buffer for de-light). These are transient per-step; don't over-design a
  universal metadata bag — add fields as steps need them.

Provide `VoxModel.FromResult(VoxResult)` and `VoxModel.ToResult()`.

### 4.2 Step interface
A step is a pure-ish transform over the working model:

```csharp
public interface IVoxStep
{
    string Name { get; }
    bool Enabled { get; }                 // from config
    void Apply(VoxModel model);           // mutates in place (or returns a new one — pick one and be consistent)
}
```

Keep steps individually testable (EditMode tests, no Unity scene needed — see §8).

### 4.3 Composition
**Fixed canonical order, each step toggleable with a few params, bundled into per-category
presets.** *Not* a reorderable DAG — the valid orderings are essentially unique (e.g. de-light
must precede palette-snap; floaters precede colour; morphology is last), so a free graph editor
is effort no one will use.

- **Presets** are named bundles of (which steps on, with what params): e.g. `creature`,
  `prop` / hard-surface, `raw-voxel-cleanup`. Start with sensible defaults; presets are just
  data.
- **Per-asset overrides**: allow an optional per-model override on top of the chosen preset
  (a small settings sidecar) so one stubborn asset can be hand-tuned. Category preset is the
  default; override is opt-in.

### 4.4 Canonical pipeline order
1. **Voxelize** — mesh only (existing `ObjToVoxConverter`); **skipped for voxel-in** (load a
   `.vox`/voxel source straight into `VoxModel`).
2. **Remove floaters** (§6.1)
3. **Mirror / revolve** (§6.4) — opt-in, while colour is still raw
4. **De-light** (§6.2)
5. **Palette-snap** (§6.3)
6. **Morphological despeckle/fill** (§6.5)
7. **Export** one `.vox` via `VoxWriter`

Ordering rationale: floaters before colour so specks don't pollute colour statistics;
morphology last so it tidies the final solid.

**Note:** because every step except voxelization is grid-level, "run cleanup on a pure voxel
model with no source mesh" works for free — just enter at step 2.

## 5. Colour — the meaty part

Meshy bakes **lighting/shading/AO** into the texture, so a surface meant to be one flat colour
arrives as a gradient (lit top, shadowed underside, dark crevices). The current quantiser
clusters *shaded* colours, which fractures one material into several palette entries and muddies
boundaries. The fix is two coordinated steps:

### 5.1 De-light (step 4)
Flatten baked shading **without knowing the lighting**:
- Segment **material regions** = spatially-connected runs of similar colour (region-grow over
  the occupancy grid with a colour-similarity threshold; work in a **perceptual space**, see below).
- Collapse each region to a single **representative** colour (median or dominant — *not* mean;
  mean drags toward shaded samples). Assign the representative to every voxel in the region.

This removes intra-material gradients as a side effect of region-collapse.

### 5.2 Palette-snap (step 5) — anchored hybrid
- Maintain a **hand-authored master palette** (~32–64 flat, cheerful Crossy-Road swatches). This
  is *the* art-direction knob; authoring it well is most of the quality. It is shared across all
  assets → cross-asset cohesion (a grab-bag of generated models reads as one game). `VoxWriter`
  already writes ≤255 exact colours verbatim, so a small master palette maps cleanly to `.vox`
  indices.
- For each model, snap each region's representative to the **nearest master swatch in a
  perceptual colour space** (Oklab or CIELAB — *not* raw RGB Euclidean; RGB distance mismatches
  perceived similarity and is why representatives currently come out muddy).
- Result is **anchored hybrid**: each model uses only the few swatches it needs (per-model
  economy) but every colour is drawn from the shared master set (global cohesion).

Failure mode to guard: shading dark enough that a representative crosses into a neighbouring
swatch (dark-red → brown). Computing the representative *before* snapping (per §5.1) prevents it,
because the representative is the un-shaded material colour, not a shaded sample.

> The original "advanced colour correction" / "LUT" idea reduces to: **author a good master
> palette, snap in perceptual space.** No separate LUT machinery needed.

## 6. Step specifications

### 6.1 Remove floating voxels
- Connected-component analysis over the occupancy grid (6- or 26-connectivity — pick 6 and make
  it a param if needed).
- Delete any component **below a size threshold** (absolute voxel count and/or % of total — one
  slider). Keep all larger components.
- **Not** largest-component-only: that nukes intended detached pieces (antenna ball, ear tip).
  The threshold spares substantial detached parts while killing voxelization specks.

### 6.2 De-light
See §5.1.

### 6.3 Palette-snap
See §5.2. Params: master-palette asset reference; perceptual space choice (default Oklab);
optional per-model max-colours cap.

### 6.4 Mirror / revolve (opt-in symmetry)
**Off by default.** Forcing symmetry erases intentional asymmetry (eyepatch, raised paw, logo on
one door). Two operations:

- **Mirror about a plane.** Find the plane by **known axis + offset-solve + confidence gate**:
  the bilateral plane is almost always left/right (a known axis in Y-up space); fix the axis
  (default left/right), search the *offset* along it for best mirror overlap (occupancy + colour
  agreement), and **gate on a confidence/overlap score** — below threshold, treat the model as
  *not* symmetric and skip. "Force symmetric" (overwrite one half with the mirror of the other)
  is the opt-in action.
- **Revolve (axial / rotational symmetry → solid of revolution).** For standalone wheel/cylinder
  assets: take the radial profile (occupancy as a function of axial position × radius from the
  axis) and revolve it so every ring at a given radius is identical → perfectly round *and*
  mid-plane symmetric in one shot. Axis = the model's principal axis (or a specified axis).
  This is the one "primitive regularisation" we keep in v1, scoped to the standalone case only
  (no segmentation, so no extracting a wheel from a whole vehicle).

> Why two operations: mirror enforces 2-/4-fold symmetry but a mirrored lumpy circle is still a
> lumpy octagon. Roundness needs revolve, not reflection.

### 6.5 Morphological despeckle/fill ("fix anomalous voxels")
The original "fix anomalous voxels/colours" goal splits: **colour** anomalies are already handled
by de-light + palette-snap (a stray off-colour voxel snaps to its region's swatch). What remains
is **geometric** noise:
- Remove isolated protruding bumps; fill single-voxel pinholes (a majority/closing filter on
  occupancy).
- Keep it **mild and opt-in** — aggressive morphology erodes intended thin features (legs,
  antennae). Run it **last** so it tidies the final solid.

## 7. Suggested build order (milestones)

1. **`VoxModel` refactor** — dense grid working model + `FromResult`/`ToResult`; wire the existing
   quantiser path through it without behaviour change (safety net / proves the seam).
2. **Conservative trio** — floaters (§6.1) + de-light & palette-snap (§5) + morphology (§6.5).
   These improve *every* Meshy model regardless of category and are the real first deliverable.
   Replace/refactor the current `ColorQuantizer` into the de-light + palette-snap steps.
3. **Pipeline + presets** — `IVoxStep`, fixed-order runner, category presets, per-asset override.
   Update `MeshToVoxelsWindow` to expose preset selection + per-step toggles.
4. **Symmetry (opt-in)** — mirror, then revolve (§6.4).

Author the **master palette** asset alongside milestone 2 — it gates colour quality.

## 8. Testing & verification
- Steps are pure grid transforms → cover with **EditMode tests** (`Tests.Voxels` area;
  `Tools/run-tests.sh Tests.Voxels`). Test on small hand-built grids: a grid with a detached
  speck (floaters), a two-region shaded block (de-light collapses to 2 colours), an off-centre
  near-symmetric shape (mirror confidence/offset), a lumpy disc (revolve → round).
- Watch the `VoxWriter` coordinate note: it flips an axis to preserve handedness; verify against
  an **asymmetric** test mesh (an "L") that output isn't mirrored after the round-trip — symmetry
  steps make mirroring bugs easy to mask.
- Compile check if unsure: `Tools/check-compile.sh` (boots Unity batch mode; slow — use sparingly).

## 9. Open/assumed defaults (flagged, not blocking)
- Master palette is **hand-authored** (assumed). If a derived-from-corpus palette is wanted
  later, the snap step is unchanged — only the palette source differs.
- Connectivity for floaters: assume **6-connected**; promote to a param if a model needs it.
- Perceptual space: assume **Oklab**; CIELAB is an acceptable substitute.
```
