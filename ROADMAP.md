# Roadmap & Product Direction

Where Assembler is heading and why. This is strategic/decision state — *what* we're
building toward and the design directions already agreed — as distinct from
`Assembler/CLAUDE.md`, which covers *how* to work in the code. When a direction here
turns into shipped code, document the mechanics in the relevant README/CLAUDE.md and
trim this file to the still-forward-looking parts.

## Product vision

The product being built toward (decided 2026-06-11) is a **mobile (iOS/Android) player
app / mini-arcade**: players browse a shelf of small games (the YAML descriptors), tap
one, it downloads from a remote CDN and runs via the Assembler engine with touch
controls. Ambition = **public MVP / portfolio** — lean, no monetization, no accounts.
AI generation stays a **server-side authoring tool**, not an in-app feature.

The engine core is mature; the missing work is the **player-facing shell**. Without this
framing the engine looks like the product, and it's easy to keep adding engine features
instead of shipping — so **bias toward player-facing shell work and cutting scope**.

**Hard "not in v1" list:** accounts, monetization, in-app creation, multiplayer,
social/leaderboards, cloud save, campaign/meta-progression, deterministic replay.

**Phased roadmap:** P0 de-risk → P1 touch + player → P2 remote loading → P3 UI/UX shell
→ P4 art identity (parallel) → P5 beta → P6 store launch.

### Phase 0 — iOS AOT de-risk (result: GO, with caveats)

The runtime expression compiler emits executable delegates, and iOS is AOT-only (IL2CPP,
no JIT) — so the go/no-go risk was whether descriptors run on-device at all.

**Result (2026-06-11): PASS on iOS.** MiniRacer3D built into an iOS player (iPhone, A16,
IL2CPP) via a `GameBootstrap` MonoBehaviour that loads a descriptor from StreamingAssets
through `Builder.Build`. `ExpressionMethodCompiler` uses `System.Linq.Expressions`
`lambda.Compile()` (**not** `Reflection.Emit`); `Builder.Resolve` (which runs
`CompileAndRegisterAll`) succeeded on-device — IL2CPP falls back to its interpreter. iOS
bootstrap work is PR #272 (branch `claude/laughing-swirles-63b731`).

**Reaffirmed 2026-07-24 — still true, but not fully closed. Two open items:**
1. **Android IL2CPP has never been verified.**
2. The on-device proof used only MiniRacer3D. A **worst-case expression stress test** is
   wanted on all target platforms — a single descriptor / harness exercising the gnarly
   compiler paths (LINQ, value-type generics, indexers/collections, numeric promotion,
   nested control flow) — so a green result isn't just proving the easy path.

Until then, AOT-safety is proven for one simple game, not the general case. (The first
on-device run black-screened on a *separate* bug, not AOT — entities omitting
Position/Rotation hitting `None<Vector3>` → `NullValueProvider.Get`; fixed in
`Transformer.cs` by using `CreateValueSource<Vector3>` for entity+child Position/Rotation.)

## Phase 2 — remote loading (designed + first cut landed)

Designed and a first implementation landed on branch `claude/gracious-villani-c33a68`
(2026-06-15). See also `RemoteTooling/README.md` and `Tools/remote/README.md`.

**Architecture:**

- **Store:** a separate **public GitHub repo `assembler-games`** holding `manifest.json`
  + `games/<id>/descriptor.yaml`, served free over **raw.githubusercontent.com** (not
  jsDelivr — its `@latest` caches ~12h and fights the refine loop). The engine repo holds
  only the *tooling* (`Tools/remote/`), not the game data.
- **App side:** runtime assembly `Assembler.Remote` (`Assets/Remote/`) — `GameShelf`
  (programmatic uGUI, replaces `GameBootstrap` in the Bootstrap scene) fetches the
  manifest, downloads + version-caches descriptors, runs them via
  `Builder.BuildFromYaml(string)`, and returns to the shelf when the `!gameover` path
  destroys the game.
- **Generation:** `Tools/remote/` scripts — `publish-game.sh` (generate via the
  `generate-game-descriptor` skill, `claude -p` → `validate-game.sh` → push),
  `refine-game.sh`, and a `generation-daemon.sh` (+ launchd LaunchAgent) that fulfils
  briefs queued as GitHub issues labelled `generate`.

**Two decisions that resolve earlier tensions:**
1. The vision's "**no in-app creation in v1**" line is preserved by keeping remote
   generation a **private dev tool only** — the phone queues briefs via GitHub issues,
   never an in-app feature.
2. The generation runner lives on a **Mac kept always-on**, so the daemon polls
   continuously rather than draining on demand.

**v1 scope:** primitive-asset games only — `RemoteGameGuard` rejects descriptors with a
top-level `Assets:` block (custom voxel/sprite/audio aren't shipped in the app).
Voxel-asset remote loading is a later phase.

## Phase system — direction for deep multi-mode games

Agreed 2026-07-08 (grill-me interview). Goal is systems **depth** (multi-mode, parallel
systems), explicitly **not** content-hours or open worlds. Not yet built — this is the
agreed shape.

- **Phase stack + always-on groups.** Modes push/pop (combat, shop, dialogue, pause);
  parallel systems (hunger, economy) are always-on groups *outside* the stack. Not an
  exclusive FSM, not free layers.
- **Membership is entity-side** (`Phase:` field next to `Tags:`, list allowed, OR
  semantics; absent = always-on so old descriptors stay valid; spawned entities inherit
  the spawner's phase). A top-level `Phases:` section holds policy only (freeze/simulate,
  controls, OnEnter/OnExit, camera).
- **Frozen = per-phase `IGameClock` stop** (+ rigidbody stash + tween pause), never
  `SetActive`. Hidden is separate, set by push kind: `exclusive` vs `overlay`. Pause =
  `PausesWorld: true` push freezing everything except `IgnoresPause: true` groups; normal
  pushes never touch always-on groups.
- **Push takes `Parameters`** (template idiom), **pop returns `Result`** as trigger
  outputs on `OnResume`; one instance per phase on the stack (re-push = error).
- **Nested records yes, dictionaries no** (record / record-list fields inside records +
  keyed-lookup library helpers).
- **`ui list` behaviour**: record list + row template, engine reconciles rows, row
  triggers emit record/index outputs; no selection model in v1.
- **Persistence = `Persist:` list of global variable ids** saved at
  gameover/phase-transitions/app-background; world rebuilds from data; no mid-combat
  snapshot resume.
- **Dialogue is a pattern, not a feature** (overlay phase + nested records + ui list);
  enablers: `!text` resolution for record string fields + a reference example descriptor.
- **Includes: merge-only.** Duplicate id across files = hard error, NO auto-namespacing
  (it would break string cross-refs); publish flattens to a single file for remote
  distribution; folder-per-game so launcher/validators don't treat modules as games.
- **Character controller deferred** but wanted eventually — orthogonal single-behaviour
  work.

**Build order:** 1) phase core (+ nested records & includes in parallel), 2) push
params / pop results + `ui list`, 3) persistence + a flagship RPG-loop example game.
`validate-game` must learn to exercise phases (the sandbox only checks start state today).
