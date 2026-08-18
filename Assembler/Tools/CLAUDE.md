# Tools — batch-mode script gotchas

Diagnostics for when a `Tools/*.sh` script gives a confusing result. Each script's own
header comment documents its modes, flags, and usage — read that first; this file covers
the surprises the headers don't.

**Shared rules for every script:** they boot Unity in batch mode and are slow (tens of
seconds to a couple of minutes). They run fine alongside an editor open on a *different*
path (e.g. the user's main checkout), but refuse if an editor already has *this* path
open. The first run in a fresh worktree does a one-time cold import (~3 min).

## Confusing compile errors

- **Batch mode aborts on compile errors.** Unity refuses to run *any* `-executeMethod` if
  the project has compiler errors at boot (`"Scripts have compiler errors. Aborting
  batchmode due to failure."`) and exits before invoking the method — regardless of which
  assembly the entry point lives in. Isolating it in a dependency-free `.asmdef` does
  **not** make it run on a broken project. Fix the compile error first; every other script
  is blocked until you do.
- **A fresh worktree's first cold import emits spurious `PackageCache` errors — re-run.**
  The initial import can report e.g. `CS0234: 'GUID' does not exist in the namespace
  'UnityEditor'` inside `Library/PackageCache/com.unity.serialization` /
  `com.unity.shadergraph` — an assembly load-ordering artifact, not your code, often
  accompanied by a `[Licensing::Module]` handshake failure (an interactive editor on a
  *different* path holds the single floating license seat). The immediate second
  incremental run is clean. When a cold import shows only `PackageCache` errors and none
  under `Assets/`, just run the check again. Adding a package to `manifest.json` triggers
  the same cold-import pass.
- **`com.unity.ai.assistant` duplicate-DLL conflict (Unity < 6.5).** The package bundles
  BCL DLLs (`System.Collections.Immutable`, `System.Text.Json`,
  `Microsoft.Bcl.AsyncInterfaces`, `System.Text.Encodings.Web`) that collide with the
  project's NuGetForUnity copies, breaking the auto-referenced NuGet copy → `CS0234:
  namespace 'Immutable' does not exist` in `TriggerContext.cs`. Fix (durable, official):
  add the four `EXCLUDE_COLLECTIONS_IMMUTABLE;EXCLUDE_TEXT_JSON;EXCLUDE_BCL_ASYNCINTERFACES;EXCLUDE_TEXT_ENCODINGS_WEB`
  scripting defines (active build target — `Standalone:` line in `ProjectSettings.asset`)
  so the NuGet copies win. Unity 6.5+ auto-excludes them.

## `check-compile.sh`

- **Parse the log, not the callbacks, for warnings.**
  `CompilationPipeline.assemblyCompilationFinished` / `compilationFinished` don't reliably
  deliver *warnings* in batch mode (they arrive empty even when `csc` logged them); `csc`
  always writes `error CS####` / `warning CS####` to `-logFile`, so the tooling greps those
  out instead. Preserve this if you change the script.
- The project carries ~50 pre-existing nullable warnings (mostly intentional `CS8618` on
  serialized fields), which is why the script **defaults to incremental** — that scopes the
  report to code you just touched. `--all` resurfaces every project warning; use it only
  for a full audit.

## `validate-game.sh`

- **Baseline.** As of 2026-06-24 the corpus is clean except **`VoxelDemo.yaml`** (fails at
  `deserialise` — it uses the `!asset { Id: … }` mapping form, which `AssetTypeConverter`
  doesn't accept). Compare any FAIL against `master` before assuming it's your regression,
  and scope the run to the files you touched.
- **Piping `validate-game.sh 2>&1 | tail -N` makes the exit code that of `tail` (always
  0).** Read the printed "N of M file(s) failed" line, don't trust the exit code through a
  pipe, and don't let failures scroll off.
- It builds entities *without* entering play mode, so `Awake`/`Start` never run — see
  `Assets/Behaviours/README.md` → object pooling for the `OnInitialise` contract that makes
  a behaviour sandbox-safe.

## `ExampleGameDescriptors/` scanning scope

The batch validators recurse it (`SearchOption.AllDirectories`), but the Game Launcher
window is **top-level only** (`Directory.GetFiles`). So:

- Any non-game / auxiliary `.yaml` — or a descriptor depending on an unbaked asset —
  placed *anywhere* under that tree gets picked up by the validators and fails. Keep those
  elsewhere (e.g. under `Assets/Voxels/…/Examples/`).
- A descriptor must be **top-level** in `ExampleGameDescriptors/` to appear in the Launcher.
