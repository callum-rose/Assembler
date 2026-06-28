# Handoff — B1: Reference Hull Clip

**Branch:** `claude/recursing-chandrasekhar-37d718`
**PR:** [#344](https://github.com/callum-rose/Assembler/pull/344) (was open against `master`; **closed when this work was paused** — branch + this doc preserve it)
**Status:** Implemented and unit-tested, **not merged**. Paused over a cost concern found while testing (see below).

## What it does

When an asset has reference silhouettes, deterministically clip the **resolved part list** to the silhouette envelope **before composition**, so authored geometry can no longer overhang the reference shape and the existing silhouette-IoU gate stays honest. Gated on a `ReferenceBrief` with non-empty silhouettes; no brief ⇒ no-op. No-reference assets (cactus/tree/rock) are byte-for-byte unchanged by construction.

Design was settled in a "grill-me" session; the full build spec is the B1 plan that kicked off this branch.

## Key pieces

- `Assets/Voxelization/Assembly/HullClip.cs` *(new)* — pure, deterministic `HullClip.Apply(parts, brief, settings)`: builds per-axis dilated silhouette masks, classifies each world voxel inside/outside against the same projection the IoU gate uses, and tiers each part:
  - **light** (`< moderateRatio`) — trim silently
  - **moderate** (`[moderate, severe)`) — trim + emit a reposition hint
  - **severe** (`≥ severeRatio`, full removal, or disconnection of a non-`Loose` part) — **refuse** (keep authored geometry, request re-plan)
  - **global floor** — if aggregate removed-mass fraction exceeds the floor, discard the whole hull (a bad reference never beats no reference)
- `VoxelProjector.cs` — exposed `MapToPlane` / size-based `Dimensions` so the clip reuses the exact projection/v-flip convention (the main correctness risk; pinned by tests).
- `ValidationIssue.cs` — new `PartClippedModerate` / `PartClippedSevere` issue codes.
- `ModelAssembler.cs` — `Compose` made `public static` for recompose.
- `SetOrchestrator.cs` — runs the clip after assembly, recomposes clipped parts; **moderate → per-part re-author**, **severe → review-round re-plan**, exhaustion ships authored geometry, discarded hull falls back to free authoring.
- `VoxelizationConfig` / `VoxelizationSettings` / `VoxelSetReviewWindow` — five tunable knobs (`EnableHullClip`, dilation, moderate/severe ratios, global floor) in the advanced foldout.
- `Assets/Tests/Voxelization/HullClipTests.cs` *(new)* — 15 EditMode cases (mask projection per face, v-flip, dilation, ratio accounting, every tier, loose exemption, global floor, front-only depth, no-brief no-op). All pass.

## Verification

- `Tools/run-tests.sh Tests.Voxelization` — all 15 HullClip tests pass; no regressions. (Two pre-existing `PipelineStageTests` failures — `BriefExtractor_TrimsEmptyMarginFromTheSilhouette`, `SetOrchestrator_ExtractsTheBriefBeforePlanning_WhenAReferenceImageExists` — are unrelated and already fail on `master`.)
- Not exercised end-to-end against a live generation here (needs an API key + reference images).

## ⚠️ Why it's paused — cost risk (read before merging)

The deterministic **trim** is fine. The problem is the **orchestration response** to the clip. On a hard asset (the `standing-beagle` test, whose right-view silhouette IoU plateaus at ~0.50 against a 0.75 threshold), B1 turned a "fails fast in planning" asset into a **~30-minute grind**:

- **moderate → re-author** fired on 5–6 parts *every* validation round (the parts keep overhanging their planned boxes, so re-authoring the same box rarely converges).
- **severe → re-plan** forced a *full* second planning + 10-part authoring pass.
- One cancelled run = **51 LLM calls, ~98k output tokens** (40 of those calls were authoring), no convergence.

**Before merging, add a cost control.** Options discussed:
1. **Defang the loops** — moderate trims silently (no re-author); severe keeps geometry + marks `NeedsReview` (no forced re-plan). Keeps the deterministic trim + honest IoU; removes most of the cost. (Recommended for cost.)
2. **Hard per-asset budget** — token / LLM-call / wall-clock cap that aborts cleanly and exports best-effort.
3. **Lighter caps** — re-author only the single worst-clipped part per round; lower the planning attempt cap.

## Related

- Stacked on top of this: `claude/beagle-planning-fixes` (PR #350) — planning-stage fixes found while diagnosing the same beagle. See `HANDOFF-beagle-planning-fixes.md` on that branch.
- Follow-up tickets noted in the original B1 plan: B2 forward per-part bound (#340), per-view colour projection (#341), 2D-segment→backproject rigging (#343).
- Diagnostic run logs: `Assets/Resources/Voxels/Sets/2026-06-15-*beagle*/session*.log`.
