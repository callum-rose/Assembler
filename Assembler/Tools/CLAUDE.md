# Checks — gotchas

Diagnostics for when a check gives a confusing result. The command list and how to run them
live in [`../CLAUDE.md`](../CLAUDE.md) › Build & Test; this file covers the surprises.

Checks run against a **live editor** over `com.unity.pipeline` (`unity command …`). Two
scripts survive in this directory and are documented at the bottom.

## The editor is unreachable

- **Compile errors take the pipeline down with them.** C# errors make Unity boot into Safe
  Mode, where packages — `com.unity.pipeline` included — do not load, so *every*
  `unity command` fails to connect. A batch-mode editor doesn't even stay up: it logs
  `"Scripts have compiler errors."` and exits. This is a deadlock if you were hoping to fix
  the errors *through* the editor, and there is no CLI-side workaround — packages not
  loading in Safe Mode is by design.

  `unity pipeline list` reports Safe Mode explicitly. Fix the errors at the source and
  restart the editor. **Do not read "can't connect" as "no editor, so hand-edit blindly."**

- **`unity status` shows an editor but commands 503.** The editor is still settling
  (importing/compiling) after startup. Main-thread commands are refused until it is ready;
  `editor_status` stays servable throughout so you can poll it. Wait and retry.

- **`unity run --command` is broken (issue #588).** It boots an editor and dispatches
  before that settle gate opens, so it fails with a spurious `503 Server Busy` after paying
  the full boot cost. Don't reach for it as a "no editor running" fallback — boot one and
  poll `unity status` instead.

- **Pass `--project-path` when more than one editor is running.** The user's main checkout
  and your worktree are different projects; without it the CLI may answer from the wrong one,
  which looks exactly like your changes having no effect.

## Confusing compile errors

- **A fresh worktree's first cold import emits spurious `PackageCache` errors — re-run.**
  The initial import can report e.g. `CS0234: 'GUID' does not exist in the namespace
  'UnityEditor'` inside `Library/PackageCache/com.unity.serialization` /
  `com.unity.shadergraph` — an assembly load-ordering artifact, not your code, often
  accompanied by a `[Licensing::Module]` handshake failure (an interactive editor on a
  *different* path holds the single floating license seat). The immediate second
  incremental run is clean. When a cold import shows only `PackageCache` errors and none
  under `Assets/`, just run the check again. Adding a package to `manifest.json` triggers
  the same cold-import pass.

  Seeding `Library/` from the main checkout (`cp -Rc` — an APFS clone, instant and costing
  no extra disk) skips the cold import entirely.

- **`com.unity.ai.assistant` duplicate-DLL conflict (Unity < 6.5).** The package bundles
  BCL DLLs (`System.Collections.Immutable`, `System.Text.Json`,
  `Microsoft.Bcl.AsyncInterfaces`, `System.Text.Encodings.Web`) that collide with the
  project's NuGetForUnity copies, breaking the auto-referenced NuGet copy → `CS0234:
  namespace 'Immutable' does not exist` in `TriggerContext.cs`. Fix (durable, official):
  add the four `EXCLUDE_COLLECTIONS_IMMUTABLE;EXCLUDE_TEXT_JSON;EXCLUDE_BCL_ASYNCINTERFACES;EXCLUDE_TEXT_ENCODINGS_WEB`
  scripting defines (active build target — `Standalone:` line in `ProjectSettings.asset`)
  so the NuGet copies win. Unity 6.5+ auto-excludes them.

- **The project carries ~50 pre-existing nullable warnings** (mostly intentional `CS8618` on
  serialized fields). `unity command console --level Error` filters to errors; widen it only
  when you want the full audit, and compare against `master` before assuming a warning is yours.

## Staleness — the failure mode a resident editor added

A batch process booted fresh and read the truth off disk. A live editor holds imported state,
so a check run straight after a `git checkout` or an external edit can answer about a project
that no longer exists — **confidently, and without erroring**.

Every check calls `AssetDatabase.Refresh()` first and refuses to run while the editor is
importing or compiling (see `Assets/Editor/EditorPipelineCli.cs`). That covers assets, but
**not** C#: after changing a `.cs`, run `unity command recompile` and poll
`recompile_status` until `completed` before trusting any other check. A command that runs
against the previous assemblies is wrong in the most expensive way.

## `validate_game` / `check_expression`

- **Baseline.** As of 2026-08-27 the corpus is clean except **`VoxelDemo.yaml`**, which fails
  both: `validate_game` at `deserialise`, and `check_expression` as an unreadable descriptor.
  It uses the `!asset { Id: … }` mapping form, which `AssetTypeConverter` doesn't accept.
  Compare any FAIL against `master` before assuming it's your regression, and scope the run
  with `--targets`.
- **Piping through `| tail -N` makes the exit code that of `tail` (always 0).** Read the
  printed "N of M file(s) failed" line, don't trust the exit code through a pipe, and don't
  let failures scroll off.
- `validate_game` builds entities *without* entering play mode, so `Awake`/`Start` never run —
  see `Assets/Behaviours/README.md` → object pooling for the `OnInitialise` contract that makes
  a behaviour sandbox-safe.
- **`--expr` takes one expression and is not comma-split**, because expression bodies contain
  commas — `Clamp(x, 0f, 1f)` would otherwise become three fragments that each fail to compile.
  `--targets` and `--args` *are* comma-separated lists.

## `ExampleGameDescriptors/` scanning scope

The validators recurse it (`SearchOption.AllDirectories`), but the Game Launcher window is
**top-level only** (`Directory.GetFiles`). So:

- Any non-game / auxiliary `.yaml` — or a descriptor depending on an unbaked asset —
  placed *anywhere* under that tree gets picked up by the validators and fails. Keep those
  elsewhere (e.g. under `Assets/Voxels/…/Examples/`).
- A descriptor must be **top-level** in `ExampleGameDescriptors/` to appear in the Launcher.

## The two surviving scripts

- **`check-format.sh`** — not a Unity check. The slow part is the MSBuild workspace load
  inside `dotnet format`, outside the editor entirely; Unity is involved only to regenerate
  the gitignored `.csproj`/`.sln` via `SyncVS.SyncSolution`. It does that through the
  pipeline when an editor is resident (~1s) and falls back to a batch boot otherwise.
  `SyncVS` is `internal`, so `eval` reaches it by reflection — calling it by name fails with
  *"'SyncVS' is inaccessible due to its protection level"*.

- **`validate-game.sh`** — kept solely for `RemoteTooling`'s unattended daemon, which cannot
  use the pipeline while issue #588 stands. It is not a second implementation: the script's
  `-executeMethod` entry point and the `validate_game` command are two wrappers over the same
  `GameSandboxValidatorBatch.Run`. **Prefer `unity command validate_game`** — this script
  boots Unity per call and refuses to run if an editor already holds this path.
