# Handoff — Voxel Turnaround Spike (AI-in-the-loop → voxel pipeline)

> **Status: paused.** PR #353 was closed (not merged) on 2026-06-28 while the work
> is on hold. The branch `claude/thirsty-goldberg-3a8025` is preserved — reopen the
> PR or re-checkout the branch to resume. This doc is the resume point.

## What this is
A throwaway Unity **editor-window spike** that reconstructs an animatable voxel model
from AI-generated orthographic images, with an AI image-gen in the loop to supply the
missing top view. Single self-contained file; no dependency on the real Assembler
pipeline.

- **File:** `Assets/VoxelSpike/Editor/VoxelSpikeWindow.cs` (namespace `VoxelSpike.Editor`, menu **Assembler > Voxel Spike (Turnaround)**)
- **asmdef:** `Assets/VoxelSpike/Editor/VoxelSpike.Editor.asmdef` (editor-only, `references: []`)
- **Branch / PR:** `claude/thirsty-goldberg-3a8025` / #353 (closed, reopenable).
- **Never headless-compiled.** The editor holds the branch path, so `Tools/*.sh`
  refuse to run here; correctness was verified by reading + brace/paren balance only.
  Always ask the user to flag in-editor compile errors.

## The pipeline (2 stages, stateless — recomputes from inputs, survives domain reload)
**Stage 1 — `RunStage1`:** split a turnaround sheet (front+right, front on left) into
two objects via connected components on a shared vertical extent → carve a high-res
F+R visual hull (`Carve`, top=null) → export:
- `hull.txt` — carved hull as-is (Goxel text)
- `top_guess.png` — pristine top-down render (`RenderTop`; re-import target for stage 2)
- `reference_sheet.png` — third-angle projection drawing for the AI (the main artifact)

**Stage 2 — `RunStage2`:** re-carve F+R, AND-in the AI-refined top (`View.BuildLargest`,
drops stray marks) as the third silhouette (subtractive, down Y), recolour top-visible
voxels, down-res to editable resolution (modal block colour), write Goxel `output.txt`.
Runs F+R-only if no AI top loaded.

Conventions: world X=right, Y=up, Z=depth. Goxel(x,y,z)=world(X,Z,Y). Front sees XY,
right sees ZY, top sees XZ. Top render row 0 (bottom) = z=0 = front edge.

## Top-guess colouring certainty (the hard-won core logic)
The F+R hull **over-fills**: front-X × side-Z manufactures phantom voxels the (still
missing) top carve would delete. The top render colours the *topmost* voxel per (x,z)
column — which can itself be a phantom. Colouring phantoms misleads the AI, so uncertain
columns render as the flat grey `Unknown` placeholder for the AI to fill.

**Current rule (this session, commit `f8f50dc`)**, gating `RenderTop` / `RenderTopRaw`:
> A top voxel is coloured (certain) when **`edgeSeen && agreeDist <= tolerance`** —
> i.e. a colouring view saw it at its silhouette edge (real-surface proxy) AND the views
> that saw it agree on colour within `_colorAgreeTolerance`.

- `agreeDist` = max pairwise normalised-RGB distance among the axes (front / right / top)
  that coloured the voxel, computed at carve time (Pass 3) and stored on the `Hull`.
  Single-axis voxels = `0`, so they always pass the agreement half.
- `_colorAgreeTolerance` — new **0–1 slider** (default `0.10`), under the "Top guess
  colouring" header. At **`0`** it reduces to the old behaviour (single-view silhouette-edge
  only, plus *perfectly* agreeing corners). Higher admits agreeing two-view "corner"
  voxels — the over-fill voxels the old rule excluded — only when the views' colours
  corroborate them. Threshold is applied at render, so the slider can re-render without
  re-carving.

**Why this shape (decided in a design grill, see PR #353 discussion):**
- No rule can *guarantee* it never colours a cullable voxel — from two orthographic
  silhouettes you can't prove any individual hull voxel survives the unknown top carve.
  The lever is **false-positive rate**, not a guarantee.
- The voxels the agreement rule newly admits (two-view corners) are exactly the
  over-fill-prone ones, so the colour test is doing all the safety work. It works well
  on **multicolour** models (a phantom corner's two views disagree → rejected) and
  **over-paints on uniform / low-variety** regions (everything agrees → phantom caps get
  confident colour). If you push the slider up on a monochrome model and see colour
  bleeding into footprint that should be culled, back the tolerance down.
- The `edgeSeen` guard is the surface proxy kept from the old rule; it's what suppresses
  interior phantom corners. Don't drop it.

Earlier rules that FAILED and were replaced: nearest-neighbour fill (muddy); seen-by-≥1
view; single-view-only; `seenCount == 1 && edgeSeen` (this session's predecessor — too
strict, excluded all corners); 3D horizontal-edge test (`HorizEdge`, removed — phantoms
have empty hull neighbours too).

**Stronger variant not yet built (fallback if uniform over-paint bites):** *column
colour-consistency* — colour (x,z) iff *every* seen voxel in that column shares one
colour (still AND-ed with `edgeSeen`). Invariant to where the top carve cuts vertically,
so it can't suffer "phantom on top, different real colour below." Discussed but the
per-voxel agreement rule was chosen as the smaller delta that subsumes the old rule.

## The reference sheet (`BuildProjectionSheet`)
A real **third-angle engineering drawing**, all panels at one pixels-per-voxel scale:
- Front lower-left, side right (shares height), top above front (shares width).
- Equal gutters; all three meet at the front view's top-right corner; 45° **miter
  diagonal** through that corner reflects depth side→top.
- **Feature projectors** at colour-change boundaries, placed by `FeatureLandmarks` at
  every `VerticalEdgeStrength` local maximum above a `_guideSensitivity`-driven threshold
  (0–1 slider, default 0.8; 0 = box only, 1 = subtlest).
- **Depth transfer:** quarter arcs about the corner (`_depthArcs`) OR miter-reflected
  L-paths. **Landing ticks** (`_landingTicks`) mark where projectors land on the top.
- Toggles: `_sideGuidesOnly`, `_topGridOnly` (grid, no colour), `_guideColor`.
- The top panel here uses `RenderTopRaw`, which shares the exact certainty rule above
  (same `_colorAgreeTolerance`).

## The image-gen prompt (was chat-only; recorded here)
Hand the reference sheet to the image model with:

> You are given a third-angle orthographic reference sheet of one object: front view
> (lower-left), side view (right of front, same height), and a rough top view (above
> front, same width). Thin lines are construction guides; the 45° diagonal mirrors depth
> from the side view into the top.
>
> Produce the top-down view (looking straight down). Rules:
> - Keep exact registration: same width as the front view, same depth as the side view,
>   front edge at the bottom, aligned to the guide lines and the 45° miter.
> - Flat grey areas are unknown — fill them with the colour that view should be, inferred
>   from the front and side views. Keep areas that are already coloured.
> - Colours change only at the guide lines; keep each region flat and consistent with the
>   elevations.
> - Plain solid background, no shadows, no text, no extra marks, single object only.

Asks for **just the top view** (drops the manual-crop step). If the model reliably
returns a full sheet instead, change the first rule to "Return the full sheet, redrawing
only the top panel."

## Likely next steps / open threads
- **Confirm the agreement rule visually** — it has not been run in-editor since the change.
  Verify denser fill on a multicolour model; check a monochrome model for phantom over-paint
  and tune `_colorAgreeTolerance` (or implement the column-consistency fallback above).
- **Top-panel auto-extraction for stage 2:** stage 2 still expects a lone top image; the AI
  now returns a full sheet, cropped manually. Auto-extract the top panel (component above
  the front / upper-left quadrant) — likely the next real feature.
- Guide-density tuning: `W/100` min-spacing or the `25f` silhouette-flip weight in
  `View.VerticalEdgeStrength` if stray/gradient guide lines appear.
- Optional: full bilateral-symmetry colour inference (reflect left-half onto missing
  right-half) — only mirror toggles exist today.

## Working agreements / gotchas
- Editor asmdef has **no nullable context** → avoid `?`/records/`init`; use plain classes
  + `set`, return `null` freely. (`default`-literal for structs is fine.)
- Brace/paren balance check via python is the standing substitute for compiling.
- `.meta` files are Unity-generated — don't author them (this `HANDOFF.md` has none).
- Direct, no sycophancy; the user pushes back on geometry — reason it through.

## Key references
- Full file: `Assets/VoxelSpike/Editor/VoxelSpikeWindow.cs`
- Commit history on this branch is the feature arc (one logical change each); top of branch
  is `f8f50dc` (the agreement rule).
- Project guidance: `CLAUDE.md`.
