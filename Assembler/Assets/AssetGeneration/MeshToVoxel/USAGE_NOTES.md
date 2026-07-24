# Mesh → Voxel — usage & tuning notes

Field notes from running real Meshy exports through the pipeline. Two halves: how to prompt the
**image/mesh generation** so the model voxelises well, and which **window knobs** to reach for per
model type. See the engine-free `Core/` assembly (`Assembler.AssetGeneration.MeshToVoxel`) for the
pipeline internals.

## Image-generation / prompting guidance

The voxeliser can only keep what the resolution can represent. Prompt the source image so the shape
survives at a low voxel budget:

- **Avoid thin, spindly appendages — especially in numbers.** On a small model (e.g. a crab at ~20
  voxels on the longest axis) fine legs are simply below the grid's Nyquist limit and get erased —
  the leg detail did not survive at all, which is expected. When prompting, **avoid thin legs, or
  reduce their count**, and make any legs that must exist chunky, so there's resolution to hold them.
  (Thin-feature keep helps, but it can't invent resolution that isn't there.)
- **When you know the model will be small, make the image itself very blocky with limited detail.**
  An apple targeted at a max voxel size of **4** did not resolve to the clean box you'd expect — it
  came out with jarring, arbitrary voxel placement, because the source mesh carried more shape
  variation than 4 voxels can express, and the occupancy vote had to make coarse all-or-nothing
  calls on it. If the intended output is tiny, prompt for a **blocky, low-detail, near-cuboid**
  subject up front rather than expecting the voxeliser to simplify a detailed mesh down to a box.

Rule of thumb: **detail budget must match the voxel budget.** Small target size → simple, blocky,
low-appendage subject. Save spindly / high-detail subjects for higher voxel counts.

## Per-model window tuning

Good general-purpose starting point (the current working set): World-size input, grid-placement
search + scale flex on, thin-feature keep on, coverage 1.0, cleanup strength 2, UV island dilation,
multi-sample colour, Taubin 5 / λ 0.5 / μ 0.53, SDF surface reprojection on.

- **Fine factor — raise it to preserve detail.** The abstract red mailbox needed a **higher fine
  factor** to hold its detail; the default under-resolved it. Cost grows as factor³ (watch the
  fine-grid-size warning), so raise it only when detail is being lost. *(Candidate for automation —
  see below.)*
- **Colour-edge align (`S_col`, advanced weights) — raise it to make detail stick.** The treasure
  chest needed the **colour-edge-align weight increased** so block boundaries snapped onto the real
  colour edges and the chest's detail held. It ships at 0 because it's costly (samples the whole
  fine surface's colours); turn it up during tuning when a model's colour detail is smearing.
- **Fill corners — great for blocky subjects, bad for organic ones.** It boxed out the corgi nicely
  (crisp, chunky, Crossy-Road read), but on an organic subject like a low-poly tree it fills notches
  that *should* stay irregular and makes it look wrong. **On for hard-surface / character / blocky
  models; off for organic / natural forms.**

### Colour selection

- **Colour mode `Raw` gives the best colouring**, but scatters a solid region across dozens of
  slightly different shades. To keep that fidelity while collapsing the variation down to the
  model's **fundamental colours**, use the new **`Consolidated`** mode (below).

## New: `Consolidated` colour mode

`Consolidated` samples colours exactly like `Raw` (so the read stays faithful), then **merges
perceptually near-identical shades into the model's fundamental colours**. Two controls, either or
both:

**Merge tolerance** (Oklab distance) — collapse shades within this radius:

- **0** = exact, no merging (identical to Raw).
- **~0.05–0.08** = removes texture/sampling noise without blurring genuinely distinct regions
  (default 0.06).
- **Too high** = distinct colours start merging.

**Max colours** — a hard cap that **locks the output to a known colour count** (0 = unlimited):

- Set it to the **source image's palette size** to reproduce that palette. E.g. the blocky giraffe
  reference (`Giraffe.jpg`) is clearly 5 colours — yellow, brown, orange, white, black — so **Max
  colours = 5** locks the voxel model to 5, even if the exact hues differ slightly.
- Fewer distinct colours than the cap yields fewer — it **never invents colours**.
- Pair it with a small/zero tolerance: tolerance 0 + Max colours 5 gives exactly the 5 dominant
  fundamental colours.

**Why this beats `Per-model palette` for matching a reference count:** Per-model palette's k-means
seeds on the *most chromatic* colours and farthest points, so it spends swatches on saturated
outliers (a stray highlight, an AO speckle) rather than the real fundamentals. Consolidated is
**frequency-weighted** — the most common shades anchor the clusters and the cap merges the *nearest*
colours first, so the surviving swatches are the dominant ones you actually see.

How it works: the distinct sampled colours are leader-clustered in Oklab within the tolerance,
most-frequent-first (so the dominant shades seed the clusters); if a Max-colours cap is set and
exceeded, the nearest clusters are then agglomeratively merged (frequency-weighted means) until the
cap is met. Each voxel is repainted with its cluster's mean. It produces a real palette + labels, so
**Potts smoothing still applies** on top (it's disabled only for plain `Raw`).

## `.vox` gotchas

### Palette byte 1 is reserved — `VoxWriter` starts real colours at byte 2

The Voxel Toolkit `.vox` importer maps voxel colour byte `b` → material index `b-1`, and material index **0** is treated as empty air by the mesher (no faces, voxel dropped entirely — not even rendered transparent) and is force-flagged Invalid/Transparent, never recomputed. So **every voxel written with colour byte 1 vanishes at import.** The symptom was "Snap to master palette decimating the mesh": snap collapses the model to a few swatches, so whatever colour lands in byte 1 is shared by a large voxel population and disappears (measured 97.9% loss on one starship).

The fix is writer-side (don't touch the third-party plugin): `Core/VoxWriter.cs` reserves palette entry 0 as an opaque dummy and starts real colours at byte 2 — `FirstColorIndex = 2`, `MaxColors = 254`, applied to both the exact-palette and median-cut paths. Verified 0 voxels land in byte 1 on either path. **If you touch the palette-writing code, preserve the byte-2 start.**

### `.vox` (and `.obj`/`.png`/`.fbx`) are untracked and NOT git-binary-safe

The `Assets/TestModels/**` source assets are **untracked** — they exist only in the working copy — and no `.gitattributes` marks `*.vox` as binary. So text-based patch tooling corrupts them: restoring a binary `.vox` from a **JetBrains "Shelf" or a `git apply` text patch** rewrites every byte `≥ 0x80` to the UTF-8 replacement char `EF BF BD` (lossy, irreversible — the RGBA palette is destroyed in every file; only the header + SIZE-chunk dims survive). Fingerprint: a valid file starts `56 4F 58 20 96 …`; a corrupt one starts `56 4F 58 20 EF BF BD …`. `git stash` (blob-based) is binary-safe; shelves and text patches are not.

**Recovery is re-voxelisation, not repair:** run **`Assembler ▸ Voxelisation ▸ Re-voxelise corrupt TestModels`** (finds `.vox` with a bad version tag, reads each one's recovered max-dim from its SIZE chunk, and re-runs the Mesh→Voxel pipeline from the untouched sibling `.obj`/`.fbx`). It must run from an **interactive editor** (OBJ/FBX load + texture decode need the main thread, and the sources are untracked so a headless worktree wouldn't contain them). Adding `*.vox binary` (etc.) to a `.gitattributes` would prevent recurrence.

## Possible automation (not yet built)

- **Auto fine-factor.** Raising the fine factor for detailed models is currently manual. It could be
  derived from a detail metric on the source mesh (e.g. surface-area-to-bbox ratio, or thin-feature
  fraction from the fine-grid analysis) so detailed inputs get a higher factor automatically, within
  the fine-grid-size budget. Recorded here as a follow-up.
