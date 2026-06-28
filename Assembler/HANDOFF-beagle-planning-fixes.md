# Handoff — Beagle planning-stage fixes

**Branch:** `claude/beagle-planning-fixes` (stacked on `claude/recursing-chandrasekhar-37d718` / B1)
**PR:** [#350](https://github.com/callum-rose/Assembler/pull/350) (was open against the B1 branch; **closed when this work was paused** — branch + this doc preserve it)
**Status:** Implemented, `Tests.Voxelization` green. Planning-stage fixes **work**, but the target asset still fails downstream for non-planning reasons (see below). Paused over cost. Not merged.

## Origin

Diagnosing why the `standing-beagle` voxel asset kept failing. It died in the **planning** stage (never reached authoring/assembly). Three root causes were fixed, then a fourth structural fix was added after a second run showed the failure had shifted.

## Changes (all in `Assets/Voxelization/Pipeline/`)

1. **Accurate plan-rejection header** (`ModelPlanner`). Every geometry-check rejection was prefixed *"That skeleton can never assemble bilaterally symmetric…"* even on a `symmetry: none` asset whose real faults were **height** and **coverage**, pushing the planner to reason about mirrors instead of the actual problem. Header is now neutral/accurate.

2. **Derive `TargetLength`/`TargetWidth` from the silhouette when the manifest leaves them unconstrained** (`ModelPlanner.ResolveExtent`). The beagle manifest pinned only `height`, so the planner had no concrete length target for a 36-long dog and oscillated (span 27→41, coverage 45%→56%, never hitting the 80% gate). The authoritative silhouette is already height-scaled, so its horizontal extent is a voxel-unit span; pinning it (only when the axis is `0`) turns length into a normal constrained axis. Covered by `PipelineStageTests.Plan_DerivesUnconstrainedLengthFromTheSilhouette`.

3. **Collapse soft-shading duplicate palette shades** (`DeterministicBriefExtractor`, mergeDistance `0.06 → 0.10`). The 3D-lit render produced ~12 near-duplicate browns/tans where the manifest asked for 5. Merging only folds close shades into a kept one, so it can't drop a distinct colour like the black nose. **Partial:** it did *not* reduce the count to 5 (the shades sit right around the 0.10 boundary). Truly honoring an "N colours" budget needs a structured manifest field + real clustering — not done.

4. **Report all gates per round + raise `MaxAttempts` 5→8** (`ModelPlanner.TryParse`). `TryParse` returned on the *first* failing category (geometry → palette → coverage), so coverage was never shown until extents were already perfect — forcing sequential, one-gate-at-a-time convergence that exhausted the attempt budget. It now gathers geometry/bounding-box + palette + coverage into **one** feedback message (parse / inline-authored failures still short-circuit). *(This is the only change beyond the first three; it was pushed onto the same branch.)*

## Verification

- `Tools/run-tests.sh Tests.Voxelization` — green except the **two pre-existing** failures (`BriefExtractor_TrimsEmptyMarginFromTheSilhouette`, `SetOrchestrator_ExtractsTheBriefBeforePlanning_WhenAReferenceImageExists`), confirmed failing on the base branch too.
- **Not verified end-to-end** — no API key / reference PNGs locally. Verified by unit tests + run-log analysis only.

## Outcome on the beagle (why it still doesn't produce a model)

The planning fixes did what they should — in the later run, planning **converged** (length pinned, extents hit). But the asset still fails *downstream*:

- The right-view silhouette **IoU plateaus at ~0.50** (threshold 0.75): the planner's layouts (e.g. a raised head on a vertical neck) genuinely don't match the reference's standing-dog proportions/posture.
- B1's hull clip then turns that non-convergence into a long, expensive re-author/re-plan loop (one cancelled run ≈ **30 min, 51 LLM calls, ~98k output tokens**). See `HANDOFF-b1-hull-clip.md` (the B1 branch) for the cost detail and fix options.

## Remaining levers (not done)

- **Planner prompt guidance** for standing-quadruped posture (head forward at back height, not raised on a neck) — targets the IoU/posture mismatch. Unverified prompt change, held off.
- **Real palette clustering to N** (k-means or a structured manifest colour budget) — the merge-distance tweak is insufficient.
- **The asset/manifest itself** is likely the cheapest fix — a cleaner side-profile reference, a looser `tolerance`, or accepting a `NeedsReview` export — since none of those need pipeline changes or paid test runs.
- **Cost controls in B1** (see its handoff) must land before this whole path is viable for hard assets.

## Pointers

- Diagnostic run logs: `Assets/Resources/Voxels/Sets/2026-06-15-*beagle*/session*.log` (concise) and `session.verbose.log` (full call timeline + per-call token usage).
- Base feature: B1 hull clip — `claude/recursing-chandrasekhar-37d718`, PR #344.
