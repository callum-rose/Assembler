# Behaviours

Concrete MonoBehaviour implementations — the runtime Unity components for entities defined in YAML game descriptors. Each behaviour is described declaratively at parse time, has its values resolved at build time, and runs here as a MonoBehaviour attached to its entity.

Subfolders group behaviours by purpose: movement, rotation, physics, spawners, list operations, variable updaters, animations, audio, camera, sprites, visuals, and the various trigger families (input, timing, physical contact, conditionals). Also contains the abstract base types for behaviours and triggers, and the listener types used to wire triggers to the downstream behaviours they fire.

## Authoring gotchas

### Namespace shadowing — fully-qualify `Debug` and `Physics`

Inside any file under `Assembler.Behaviours.*` (e.g. `Assembler.Behaviours.Camera`, `.AI`), an unqualified type name that collides with a sibling `Assembler.Behaviours.*` **namespace** binds to the namespace, not the `UnityEngine` type:

- `Debug.Log/LogWarning/LogError` → the `Assembler.Behaviours.Debug` namespace (gizmo/debug behaviours), not `UnityEngine.Debug`.
- `Physics.SyncTransforms()` / `Physics.Raycast()` → the `Assembler.Behaviours.Physics` namespace, not `UnityEngine.Physics`.

Both produce `CS0234: '<member>' does not exist in the namespace 'Assembler.Behaviours.<X>'`. **Fully-qualify:** `UnityEngine.Debug.LogWarning(...)`, `UnityEngine.Physics.SyncTransforms()`. This error does *not* surface in `validate-game`/EditMode until the assembly recompiles; `check-compile.sh` catches it directly. (Related: reading `Collider.bounds` at build time needs a prior `UnityEngine.Physics.SyncTransforms()` — play mode leaves `autoSyncTransforms` off, so build-time bounds are otherwise stale → misregistered nav cells / invisible walls.)

### Object pooling — the reuse contract

`Spawner` reuses despawned entity shells and `Destroy` returns them to an `EntityPool` keyed by `templateId` (issue #102 / PR #330). Reuse keeps the GameObject and every component alive; the factory re-runs `OnInitialise` in place (no duplicate `AddComponent`) and then calls `GameBehaviour.OnReuse()`. Every spawnable behaviour must honour:

- **Guard sub-component creation in `OnInitialise`** (`if (_x == null) …`) — re-init must reuse, not stack a second Rigidbody/renderer/collider. Use `OnInitialise`, not `Awake` (which is skipped in edit mode — see CLAUDE.md → Environment & tooling gotchas).
- **Override `OnReuse()`** (runs after re-init, sees this spawn's `Data`): reset private transient state (counters, debounce, gesture flags, cached velocity) *and* re-arm one-shot `Start` logic (auto-start timers, scan coroutines, state-machine `OnEnter`), since `Start` doesn't re-fire on a reused component. Teardown of anything `OnInitialise` re-creates goes in an idempotent `OnInitialise`.

Only entities spawned via `Spawn` get a `GameEntity.TemplateId`; a null id means a real `Destroy`. Known gap: camera/UI behaviours create components unguarded and are **not** pool-safe, but they're never in spawned templates. The `add-behaviour` skill documents this in full.

### Camera Tag targets must carry a behaviour

`camera follow` / `camera orbit` / `camera confiner` resolve `{ Tag: … }` targets through `BehaviourRegistry.GetByEntityTag`, which indexes *behaviours* by their entity's tags. An entity declared with `Tags:` but **no `Behaviours:`** never registers, so the tag resolves to nothing, the vcam gets no Follow/LookAt, and the brain parks the camera at the origin — a blank game view **with no error** (`validate-game` can't catch it; the rig builds fine). Any entity used as a camera Tag target must carry at least one behaviour — prefer tagging an existing visual entity (e.g. a ground slab) rather than a bare pivot. The same caveat applies to anything else resolved via `GetByEntityTag` at query time.

### Particle/trail materials are found at runtime — pin the shader for device builds

`particle burst` (`Visual/ParticleBurst.cs`) builds its material at runtime with `Shader.Find("Universal Render Pipeline/Particles/Unlit")` (falling back to `Sprites/Default`, then the `Materials/Primitive` resource). This is fine in the editor, but a **device/player build (esp. iOS AOT) can strip an only-found-at-runtime shader**, rendering particles magenta, because no `.mat` asset pins it. When adding particle/trail behaviours (or doing the iOS-AOT validation), add the URP particle shaders to **Always Included Shaders** (Project Settings → Graphics) or ship a real `.mat` under `Resources/Materials/` and `Resources.Load` it. Don't assume editor-green means device-green for particles.

### Input: `Controls` + `input action` is the only path

The legacy raw-input triggers (`key hold/down/up`, `mouse button`, `axis`, `gamepad button`, `mouse position`, `scroll wheel`) were **removed**. The only input path is the `Controls:` section (abstract actions + per-platform bindings) read via the `input action` behaviour. Touch gesture recognizers stay — they're not `Controls` bindings.

A `Controls` value action (`Type: value, ValueType: vector2`) already accepts *any* Vector2 Input System control path — `<Mouse>/position`, `<Mouse>/delta`, `<Mouse>/scroll`, `<Gamepad>/leftStick` — and emits them as `axis`/`x`/`y` outputs every frame. So "adding mouse/scroll to Controls" needs **no new C#** — only bindings. There is no semantic `mouse_position`/`scroll_delta` output; read `axis`/`x`/`y`. (The trigger docs in `Assets/docs/Behaviours.md` are generated — run `Tools/generate-docs.sh` after changing behaviours, and re-sync the `Generation/Resources/GenerationPrompts/*.txt` copies.)

#### Mobile on-screen controls recipe

On-screen controls (joystick/d-pad/buttons) work only through `input action` (see `MiniRacer3D.yaml`):

1. Declare abstract actions under `Controls.Actions` — `{ Type: value, ValueType: vector2 }` for sticks/d-pads; `{ Type: button, Phase: down|hold|up }` for buttons.
2. Add a `mobile:` group under `Controls.Bindings` giving each action a **single simple control path** (the validator rejects composites/multi-path here): joystick → `<Gamepad>/leftStick`, d-pad → `<Gamepad>/dpad`, button → a face button like `<Gamepad>/buttonSouth`.
3. Add `Controls.OnScreen` widgets: `Type: joystick|dpad|button`, `Action:`, `Anchor:`, `Offset`/`Size` in px of a 1920×1080 design space. `OnScreenControlsValidator` (runs in `Builder.Resolve`, so `validate-game` catches it) requires the action declared, a single simple mobile path, and widget/action kind match.

Value actions fire every frame — read components with `!output x`/`!output y`/`!output axis`. For discrete grid games, convert the stick vector to a cardinal step with an expression that returns the current heading on neutral input (the `snap dir` pattern in Snake 2/Pacman). `input action` works on **runtime-spawned templates** (`GameEntityFactory` threads the `ControlsAsset` into every spawned entity — used by Tetris's spawned piece).

### Touch gestures — pointer source, timing, and output chaining

Verified in code (2026-07) while authoring `Riftwell.yaml`:

- `tap`/`double tap`/`long press`/`swipe`/`drag` read the last-used pointer (`InputSystem.Pointer.current`), so **mouse drives them on desktop for free**. `pinch and rotate` reads `Touchscreen.current` directly — it **never fires with a mouse**; give it a desktop parallel path (e.g. a scroll-wheel value action writing the same variable).
- `long press` fires **once at the `Duration` threshold**, not on release; there is **no pointer-release trigger**, so hold-to-charge-then-release isn't directly expressible (work around it with two long-press triggers at different Durations + a `deferred trigger` fuse + condition gate).
- Overlapping gestures all fire on one pointer (the first tap of a double-tap also fires `tap`; a fast drag-release also fires `swipe`). There's no consume/priority — design around it.
- **`TriggerContext` (bound `!output`s) forwards untouched through `condition gate`, `inverse condition gate`, `deferred trigger`, `debounced trigger`, and `throttled trigger`** — outputs bound on the first listener edge remain readable after multi-hop chains, and a gate's `Condition` is evaluated against the incoming context (so impact-scaled `camera shake` Force works).
