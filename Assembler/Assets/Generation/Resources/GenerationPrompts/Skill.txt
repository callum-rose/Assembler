---
name: generate-game-descriptor
description: >
  ALWAYS use this skill whenever the user asks to create a game OR generate, edit, or review a game
  descriptor for the Assembler project — non-negotiable triggers, not suggestions. Covers "make a
  game", "build me a game", "generate a descriptor", "make me a Tetris game", "write a yaml for a
  top-down shooter", "add a power-up to this descriptor", "review my descriptor", or any task whose
  deliverable is — or could be — a `.yaml` under `Assets/ExampleGameDescriptors/`. When in doubt, use
  it. Also trigger when the user wants feedback on whether existing behaviours are sufficient or
  missing functionality for a game idea — this skill is expected to push back on the catalogue.
---

# Generate Game Descriptor

You are authoring a declarative game definition as a YAML file. Each file describes one complete game,
built by composing **entities** out of **behaviours** from a fixed catalogue. You don't need to read
the C# — treat the descriptor as a self-contained authoring format.

## Four hard requirements

1. **Read the behaviour catalogue** — [`Assets/docs/Behaviours.md`](../../../Assets/docs/Behaviours.md)
   is the source of truth for every `Type:`, property, and trigger output. If it isn't there, it
   doesn't exist; never invent names. Anything in its **Parse-only (not yet runnable)** or **Doc-gen
   warnings** lists is unsupported.
2. **Author every expression via the [`unity-expression-compiler`](../unity-expression-compiler/SKILL.md)
   skill.** Anything in an `!expr` inline `Do:` body or an `Expressions:` `Expression:` field is code
   for a strict C# subset; ordinary C# fails at runtime. That skill also covers the bare-name library
   helpers in [`Libraries.md`](../../../Assets/docs/Libraries.md) — prefer them over hand-rolled math.
3. **Read one flagship end to end** (`Gridfall.yaml`, `HollowManorHeist.yaml`, `Riftwell.yaml`) before
   authoring anything beyond a single-mechanic demo, and model your structure on it.
4. **Verify before handing back**, in the same turn, unasked:
   `unity command validate_game --targets <file>` and `unity command check_expression --targets <file>`.
   Descriptors saved under `Assets/ExampleGameDescriptors/` are
   auto-discovered by `Assembler > Game Launcher` — never edit `Builder.cs` to register one.

**Structural reference.** [`GameDescriptorSchema.md`](../../../Assets/docs/GameDescriptorSchema.md) is
generated from the deserialisation DTOs and is the authoritative contract for every section key, value
type, scalar inference rule, and tag form. Consult it whenever you're unsure of a section's shape. This
skill is the authoring guide, not the schema.

**Model composition.** [`Models.md`](../../../Assets/docs/Models.md) covers the `model` behaviour and
the craft of making primitives look like something: normalised sizes, anchors, mirroring, the
composition rules (real-world scale, silhouette, grounding, a tight palette), and ten worked recipes
— tree, house, car, crate, barrel, rock, streetlight, coin, turret, humanoid. **Any entity needing
more than one shape is a `model`**, and you should start from the nearest recipe and adapt it rather
than composing geometry from scratch.

---

## Reference descriptors

`ExampleGameDescriptors/` holds ~50 files; most are **single-feature demos** — right for checking one
behaviour's exact property names, wrong as a model for a whole game. **Default to the flagships.**

| Flagship | What to take from it |
|---|---|
| [`Gridfall.yaml`](../../../Assets/ExampleGameDescriptors/Gridfall.yaml) | Derived content and pointer-driven building. The board is *computed* from authored waypoints into a cell list that drives both tile spawning and the build rule. Drag-to-place (`ui drag source` → `screen to world` → cell snap) with a live ghost and a screen-band guard. Tap-to-upgrade as a tag broadcast each tower's own `condition gate` filters. One tower template × 5 weapons, one creep template × 6 kinds. A `Records:` wave table *is* the campaign. |
| [`HollowManorHeist.yaml`](../../../Assets/ExampleGameDescriptors/HollowManorHeist.yaml) | Agent AI and a global state bus. Guards: `perceive` writes a per-instance blackboard, a `state machine` reads it, `patrol` and `navigate` both write desired velocities that a per-frame selector picks between, feeding one `velocity`. A global alarm level is read live by speed, torch colour, lights and vcam `Priority`. Record-list inventory folded by LINQ. `Navigation:` + tag-targeted `set behaviour enabled`. |
| [`Riftwell.yaml`](../../../Assets/ExampleGameDescriptors/Riftwell.yaml) | Trigger outputs as the data bus. Five pointer gestures whose payloads ride the listener edge as `Outputs:` and are read back with `!output` — tap position, swipe direction *and* distance, collision contact/velocity into sparks and damage. `* variable changed trigger` driving a "+N" readout and camera shake. Desktop/mobile parity. |

**The architecture they share — reproduce it:**

- **Data tables over hand-written entities.** A `Records:` schema + `!record [ … ]` literal *is* the
  campaign/loot table; `Placements:` and expression-derived cell lists stamp out the board. Author the
  rule once — don't paste forty near-identical entity blocks.
- **One template, many variants** via `!parameter` slots (including the id of a template to spawn).
- **Two tiers of state.** Per-entity `Variables:` in a template are the instance blackboard; global
  `Variables:` are the nervous system many unrelated behaviours read live.
- **Gates are the logic.** `every frame trigger` → `condition gate` chains (plus `state machine` for
  agents) express nearly all rules. Try a chain before concluding the catalogue is missing something.
- **Presentation is bound, not scripted** — colours, intensity, camera `Priority`, speeds are `!expr`
  over state, so the game shows its own state with no update code.
- **Broadcast, don't hardcode** — entity-/behaviour-tag listeners so runtime-spawned entities join in.
- **Full presentation layer** — every player-visible string via `!text`, `Controls` bound for desktop
  *and* mobile (`Controls.OnScreen` for touch).
- **They explain themselves** — a long `Game.Description` and per-section comments, written as you go.

**Complementary references** — open the one matching your game's shape, *alongside* a flagship:

| Your game needs | Open | Why |
|---|---|---|
| A board in list variables | [`Tetris.yaml`](../../../Assets/ExampleGameDescriptors/Tetris.yaml) | `GridMath` cell↔world, moves validated against an `occupied` list before committing, deferred snapshot→apply→rebuild broadcast |
| Tile-to-tile movement, chase AI | [`Pacman.yaml`](../../../Assets/ExampleGameDescriptors/Pacman.yaml) | `grid mover` + four-connected `navigate`, a dot field from one `Placements:` whose `At` is an `!expr` returning a `PositionList`, global mode flip |
| Terrain that changes at runtime | [`Bomberman.yaml`](../../../Assets/ExampleGameDescriptors/Bomberman.yaml) | `nav obstacle` as a dynamic obstacle — destroying a block frees its cell so A* re-routes immediately |
| 3D vehicle physics + AI rivals | [`MiniRacer3D.yaml`](../../../Assets/ExampleGameDescriptors/MiniRacer3D.yaml) | Non-kinematic rigidbody car, rivals on the same model steered by A*-to-corner via `ai steer`, checkpoint→lap gating |
| A first-person camera | [`MazeShooterFps.yaml`](../../../Assets/ExampleGameDescriptors/MazeShooterFps.yaml) | `camera` on the player entity, facing-relative movement, waves as `timer` → `interval` with a fixed `Count` |
| Endless content from a pool | [`HelixJump.yaml`](../../../Assets/ExampleGameDescriptors/HelixJump.yaml) | Recycling a fixed ring pool, manual gravity via `fixed update trigger` + `add force`, contact-below-centre landing test |
| A wave loop, no record table | [`WaveSurvival.yaml`](../../../Assets/ExampleGameDescriptors/WaveSurvival.yaml) | The minimal version: three counters polled each frame, difficulty derived from wave number |
| Per-instance inventory | [`InventoryDemo2.yaml`](../../../Assets/ExampleGameDescriptors/InventoryDemo2.yaml) | A `record list` variable as the inventory, LINQ folds for HUD counts, consuming a record in place |
| A real UI tree | [`UiShowcase.yaml`](../../../Assets/ExampleGameDescriptors/UiShowcase.yaml) | Canvas → container → label/button/slider nesting, path-joined child ids |

Steering/perception/pathfinding have focused demos (`FlockingDemo`, `PerceptionDemo`, `PatrolDemo`,
`ClearancePathfinding`, `AiConcurrencyShowcase`) for exact property names.

**Scale to the request.** A brief with no scope qualifier → aim flagship: several interlocking systems.
A deliberately small ask → keep it small but keep the flagship *structure* (tables, parameterised
templates, gate chains, bound visuals, localised HUD). Never pad with systems the user didn't ask for.

**Before copying from any example:** take `Type:`/property names from `Behaviours.md`, never from an
example (some predate renames), and run `unity command validate_game --targets <file>` on a file
before modelling on it — some descriptors in that folder no longer build.

---

## Top-level structure

One YAML document. Order is conventional; only `Entities` is strictly required. Full key-by-key detail
is in [`GameDescriptorSchema.md`](../../../Assets/docs/GameDescriptorSchema.md) — the notes below are
the parts that bite.

```yaml
Game:          # Title (shown to player) + Description (document each interlocking system here)
World:         # Dimensionality: 2|3, BackgroundColor: "#000000"
Physics:       # Gravity: !vec — { X: 0, Y: 0 } disables it (typical for top-down/arcade)
Assets:        # project assets referenced via !asset
Constants:     # id → compile-time value; read with !var
Variables:     # id → initial value; mutable at runtime, read with !var
Records:       # named record schemas, instanced via !record
Controls:      # abstract actions + per-platform bindings
Expressions:   # named code snippets, called from !expr
Templates:     # reusable entity blueprints
Placements:    # stamp one template at many positions
Entities:      # the scene
Localisation:  # per-locale string table, referenced via !text
Navigation:    # walkability grid for `navigate` / `grid mover`
```

The **YAML mapping key is the identifier** everywhere (entities, behaviours, variables, templates…) —
spaces allowed. **Exception:** an expression's id (and any `CallableAs` alias) is the literal name
other bodies call it by, so it must be a valid identifier — letters, digits, underscores, not starting
with a digit. A space there is rejected at parse time.

`Constants` and `Variables` share the `!var` read tag (Variables win, then Constants). Use Constants
for any literal appearing twice or that the user might tune.

### `Records`

Typed field bags: `fieldName → { Type: int|float|bool|string, Default: … }`. Instance with
`!record { Type: <schema>, field: value, … }`; `!record [ … ]` is a record list, `!record []` empty.

```yaml
Records:
  Wave:
    kind:  { Type: string, Default: "normal" }
    count: { Type: int,    Default: 0 }
    speed: { Type: float,  Default: 1.0 }
```

This is the flagships' main data-modelling tool: a `!record [ … ]` constant is a campaign/loot/spawn
table; a `!record []` variable is an inventory you append to with `record list add`. Read fields with
the `RecordHelper` bare names (`GetInt`, `GetFloat`, `GetString`, `GetBool`, `HasField`, `Set*`) and
fold with LINQ: `bag.Sum(r => GetInt(r, "value"))`.

### `Expressions`

```yaml
expressionName:
  ArgumentTypes: [ int, int ]                 # optional
  ArgumentNames: [ a, b ]                     # optional, same length as ArgumentTypes
  ReturnType: int                             # required: int|float|bool|string|vector|colour
  RegisterTypes: [ UnityEngine.Vector3 ]      # optional; lets the body use the bare type name
  RegisterTypeStatics: [ UnityEngine.Random ] # optional; statics without the type prefix
  Expression: "a + b;"
```

You often need no `Expressions:` entry at all — write one-off bodies inline at the `!expr` call site.
Reserve the block for bodies reused across call sites or multi-statement bodies worth naming.

**Prefer library helpers over registering statics.** Everything in
[`Libraries.md`](../../../Assets/docs/Libraries.md) (`ScaleVector`, `Rotate2D`, `IntegratePosition`,
`Clamp`, `Max`, `RandomFloat`, `RandomOnCircle`, `RandomColor`, `LerpColor`, all of `GridMath`, …) is
callable by bare name, so `RegisterTypeStatics: [ UnityEngine.Random / Mathf ]` is usually unnecessary.
`new Vector3(...)` still needs `RegisterTypes: [ UnityEngine.Vector3 ]`; `new Color(...)` is global.

### `Placements`

Stamps a template at many positions instead of repeating entity blocks. `At` is a vector list — a
literal sequence of `!vec`, or an `!expr` returning a `PositionList`, which is how a whole dot field /
tile grid / obstacle scatter is authored as a *rule*.

```yaml
Placements:
  dots:
    Template: pill
    At: !expr { Do: pillField }          # or a literal list of !vec
    Rotation: !vec { X: 0, Y: 0, Z: 0 }  # optional; shared by every instance
    Parameters: { value: 10 }            # optional; forwarded to the template's slots
    Tags: [ collectable ]                # optional; layered on the template's tags
```

### `Navigation`

The shared walkability grid `navigate` (`astar`/`flowfield`) and `grid mover` read. Entities tagged
`ObstacleTag` are rasterized in; a `nav obstacle` behaviour makes one dynamic (frees its cell on
destroy). Required before any pathfinding behaviour works.

```yaml
Navigation:
  CellSize: 0.5
  Bounds: { Min: !vec { X: -12.5, Y: 0, Z: -9.5 }, Max: !vec { X: 12.5, Y: 0, Z: 9.5 } }
  ObstacleTag: wall
  Plane: xz               # "xy" (default, 2D) or "xz" (3D ground plane)
  Diagonal: false         # true (default) allows diagonal steps
  DefaultAgentRadius: 0.3 # optional obstacle inflation, overridable per agent
```

### Ending the game

**At least one reachable `!gameover` listener is mandatory** — the build fails without one. For a
continuously-evaluated ending, poll it: `every frame trigger` → `condition gate` → `- !gameover`.

```yaml
game over:
  Behaviours:
    tick:
      Type: every frame trigger
      Listeners: [ { EntityId: game over, BehaviourId: gate } ]
    gate:
      Type: condition gate
      Properties:
        Condition: !expr { Do: is game over, With: { left: !var left score, right: !var right score, target: !var score to win } }
      Listeners: [ !gameover ]
```

---

## YAML tags

| Tag | Form | Meaning |
|---|---|---|
| `!vec` | `!vec { X: 0, Y: 0, Z: 0 }` (Z optional) | Always a `Vector3` — there is no `Vector2` value type; 2D quantities are Z=0 |
| `!colour` | `!colour red`, `!colour "#FF8800"`, `!colour { R: 1, G: 0, B: 0, A: 1 }` | Named colour, hex (`#RGB`/`#RRGGBB`/`#RRGGBBAA` — **quote it**), or RGBA (A defaults to 1) |
| `!var` | `!var some name` | Reads per-entity variables → global Variables → Constants. The only read tag; there is no `!const` |
| `!parameter` | `!parameter slot_name` | A template parameter slot. `!parameter self_id` is the implicit "this entity's id" slot |
| `!expr` | `!expr { Do: …, With: { … } }` | Evaluates code — see below |
| `!output` | `!output local_name` | Reads a trigger output bound by an upstream listener |
| `!entity` | `!entity { Id: other, Property: Position }` | **Live** per-frame read of an entity's `Position`/`Rotation`/`Scale`. **Omit `Id`** to read the enclosing entity's own transform — preferred over `Id: !parameter self_id` |
| `!asset` | `!asset some_asset_id` | Asset by id. **Scalar form only** — the mapping form `!asset { Id: … }` fails to deserialise |
| `!clock` | `!clock deltaTime` | `deltaTime`, `time`, `frameCount`, `unscaledDeltaTime`. Respects pause/slow-mo — feed it into per-frame `!expr` physics |
| `!text` | `!text menu.start` or `!text { Key: hud.score, Arguments: [ !var score ] }` | Localised string. Note the mapping form uses **`Arguments:`**, not `With:` |
| `!gameover` | `- !gameover` | Listener that ends the game |

Lists accept flow (`[ a, b ]`) or block (`- a`) syntax.

---

## Expressions and `!expr`

One uniform form: `Do` plus optional `With` — a **map** of named operands. There is no positional
`arg0`/`arg1` form, and `ExpressionId`/`Arguments` is gone.

`Do` dispatches **by name first**: if it matches an id or alias in `Expressions:`, it calls that (the
`With` keys match its `ArgumentNames`; order is irrelevant). Otherwise it is compiled as an anonymous
body, with each `With` key a parameter referenced by name inside it. A zero-arg body needs no `With`.

```yaml
Value: !expr { Do: 'score + gain', With: { score: !var score, gain: !var points per pickup } }
Position: !expr { Do: 'new Vector3(0, RandomFloat(-2f, 2f), 0)', RegisterTypes: [ UnityEngine.Vector3 ] }
```

**Inline bodies are still code** — author them via the `unity-expression-compiler` skill.

### Type hints

Types are usually inferred (literals by kind, `!var` by resolved value, nested `!expr` by return type,
use-site by property type). An inline `!expr` accepts the same hints as the `Expressions:` block to
override inference — `ArgumentTypes` (positional to `With`'s declaration order), `ReturnType`,
`RegisterTypes`, `RegisterTypeStatics`. On a **named** `Do` call these are ignored. Reach for
`ReturnType` in **object contexts**: spawner/template `Parameters:`, `!text` and condition arguments.

> **`!output` operands are NOT inferred — always give them an `ArgumentTypes` entry.** An output's type
> is known only to the emitting trigger, so an inline `!expr` defaults it to `float`; reading a
> `Vector3` output and touching `delta.x` then fails to compile. Look the type up in the behaviour's
> **Outputs** table and declare it:
>
> ```yaml
> Displacement: !expr
>   Do: 'new UnityEngine.Vector3(0f, delta.x * sensitivity, 0f)'
>   ArgumentTypes: [ vector, float ]   # mouse_delta is Vector3
>   With: { delta: !output mouse_delta, sensitivity: !var mouse sensitivity }
> ```

---

## Localisation

**All user-facing strings go through `!text` + `Localisation:` — never inline literals, never a
string-concatenation `!expr`.** Cheap up front, miserable to retrofit.

```yaml
Localisation:
  DefaultLocale: en
  Locales:
    en:
      hud.score: "Score: {0}"
      menu.start: "Press Space to start"
```

Placeholders are `string.Format` indices (`{0}`, `{1}`; escape braces as `{{`/`}}`). Prefer the mapping
form over a format `!expr` for dynamic HUD text — the template owns word order so translators can
reorder. A missing key renders as `#key#` rather than crashing. Only `en` need be authored.

---

## Entity structure

```yaml
entity id:
  Tags: [ ball, dynamic ]              # optional; used by tagged listeners and TagsToDetect
  Position: !vec { X: 0, Y: 0 }        # optional
  Rotation: !vec { X: 0, Y: 0, Z: 0 }  # optional
  Template:                            # optional; layers with inline Behaviours
    Id: paddle_template
    Parameters: { up_action: move-left-up }
  Behaviours:
    behaviour id:                      # unique within the entity; spaces allowed
      Type: <verbatim from Behaviours.md>
      Properties: { … }                # names PascalCase-exact, types matching the catalogue
      Listeners: [ … ]                 # only meaningful on triggers
      Tags: [ scoreable ]              # optional; for behaviour-tag listeners
  Children:                            # optional; nested entities, same shape (ids are path-joined)
    child id: { Behaviours: { … } }
```

Property values can be literals or any of `!var`/`!parameter`/`!expr`/`!output`. If the catalogue
doesn't offer what you need, **stop and tell the user** — see **Feedback** below.

### Behaviour gotchas

`Behaviours.md` is authoritative; these are the traps it won't shout at you about.

- **`cube gizmo` / `sphere gizmo` / `line gizmo` draw via `OnDrawGizmos`** — editor Scene view only,
  never a built player or the default Game view. Use `primitive` for geometry that must render
  in-game; gizmos are debug overlays only.
- **Every game needs a `camera` entity with a `camera` behaviour** or nothing renders.
- **One shape is `primitive`; two or more is `model`.** Never stack repeated `primitive` behaviours
  on one entity — they all render at the entity origin, axis-aligned, on top of each other. See
  [`Models.md`](../../../Assets/docs/Models.md).
- **`Size` means different things in the two.** `primitive.Size` is a raw `localScale`; a `model`
  part's `Size` is a true world bounding box (Unity's 2-tall cylinder/capsule and 10x10 plane are
  divided out). Copying numbers between them gives a half-height column.
- **Model/primitive parts carry no collider.** Declare collision separately, and **never hand-write a
  `Size`/`Radius` to match a visual** — use `box collider`/`sphere collider` with `Fit: bounds` (one
  fitted collider) or `part colliders` (one per part), listed **after** the visual behaviour.
- **A `collision_*` / `trigger_*` event needs a `rigidbody`** on at least one of the two entities.
  `IsTrigger: true` on a collider makes it overlap-only.
- **Parse-only behaviours** (bottom of `Behaviours.md`: currently `condition`, `trigger stay trigger`,
  `when all`, `when any`) parse but never execute. Don't use them.

---

## Input — Controls and actions

All input goes through the action layer: declare abstract **actions**, bind physical inputs **per
platform**, then listen with the `input action` behaviour. There are no raw key/mouse/axis/gamepad
triggers — a key, mouse button, mouse position, scroll wheel or gamepad control is just a binding.

```yaml
Controls:
  Actions:
    move: { Type: value, ValueType: vector2 }   # emits outputs axis (Vector3), x, y (float) every frame
    jump: { Type: button, Phase: down }         # down | up | hold
  Bindings:
    desktop:
      jump: [ "<Keyboard>/space" ]
      move:
        - Composite: 2DVector
          Up: "<Keyboard>/w"
          Down: "<Keyboard>/s"
          Left: "<Keyboard>/a"
          Right: "<Keyboard>/d"
    gamepad:
      jump: [ "<Gamepad>/buttonSouth" ]
      move: [ "<Gamepad>/leftStick" ]
```

`input action` is a trigger — wire `Listeners:` like any other, `Properties: { Action: move }` matching
a key under `Controls.Actions`.

- **Mouse buttons** → button actions on `<Mouse>/leftButton` etc.
- **Mouse position / movement** → a `value` action (`ValueType: vector2`) on `<Mouse>/position`
  (absolute screen space) or `<Mouse>/delta` (per-frame). `FlockingDemo.yaml` reads the cursor this way.
- **Scroll wheel** → a `value` action on `<Mouse>/scroll` (`y` is vertical scroll).
- **Touch gestures** are separate trigger behaviours (`tap trigger`, `swipe trigger`, `drag trigger`,
  `pinch and rotate trigger`, `long press trigger`, `double tap trigger`), *not* bindings. On-screen
  widgets (joystick/dpad/button) go under `Controls.OnScreen` and drive existing actions.

`InputActionDemo.yaml` is the canonical example; `GameOverDemo.yaml` the simplest button wiring.

---

## Listeners

Triggers fire; other behaviours execute when notified. The wiring is the trigger's `Listeners:` list.

```yaml
Listeners:
  - EntityId: ball spawner            # direct — a named behaviour on a named entity
    BehaviourId: spawn ball
  - EntityTag: !var target tag        # every entity carrying the tag (resolved at notify time, so
    BehaviourId: self destruct        #   runtime-spawned entities are picked up); BehaviourId optional
  - BehaviourTag: !var scoreable tag  # every behaviour anywhere with that tag — broadest dispatch
  - !gameover                         # ends and unloads the game
```

`EntityTag`/`BehaviourTag` are full `ValueSource<string>` — literal, `!var`, or `!expr`. Inside a
template, a reference to the template's own behaviours needs `EntityId: !parameter self_id`.

### Trigger outputs

Only triggers with declared **Outputs** in the catalogue produce values. Bind at the listener, read
with `!output` downstream — usually as an `!expr` `With:` operand (and see the `ArgumentTypes` warning
above).

```yaml
- EntityId: !parameter self_id
  BehaviourId: paddle bounce velocity setter
  Outputs:
    contact_point: hit_point          # output name: local name
    other_position: paddle_position
```

```yaml
paddle bounce velocity setter:
  Type: vector variable setter
  Properties:
    VariableId: !var ball velocity
    Value: !expr
      Do: paddle bounce
      With: { velocity: !var ball velocity, hit_point: !output hit_point, paddle_position: !output paddle_position }
```

---

## Templates

Anywhere an entity could appear it can instead say `Template: { Id: …, Parameters: { … } }` and inherit
the template's `Tags`, `Variables` and `Behaviours` — layering extra tags and behaviours on top.

```yaml
Templates:
  paddle_template:
    Tags: [ paddle ]
    Variables:                      # per-instance; each spawned entity owns its own copy
      health: !parameter initial_health
    Behaviours:
      up action:
        Type: input action
        Properties: { Action: !parameter up_action }
        Listeners: [ { EntityId: !parameter self_id, BehaviourId: move up } ]
      move up:
        Type: velocity
        Properties: { Velocity: !vec { X: 0, Y: !var paddle up speed } }
```

- **`self_id`** is implicit on every template — use it wherever a behaviour refers to its own entity.
- **Per-entity `Variables:`** are the standard pattern for per-instance health, lifetime, ammo. `!var
  lifetime` inside the template resolves to *that* instance's copy.
- **`spawner`** instantiates templates at runtime, passing `Parameters:` — including ones that seed
  per-entity Variables.

---

## UI — composable uGUI blocks

UI is entities, composed with the same `Children` nesting as everything else. **Not IMGUI, not a
`Rect`/anchor model** — a few old descriptors still carry `text label` with `Rect:`/`Anchor`/`Label:`;
those properties are silently ignored now. Always nest inside `ui canvas` → `ui container` and size
with `PreferredWidth`/`PreferredHeight`.

| Block | Kind | Key properties |
|---|---|---|
| `ui canvas` | UI root | `MatchWidthOrHeight` (0..1), `ReferenceResolution` (`!vec`, X=width Y=height) |
| `ui container` | auto-layout | `Direction` (vertical/horizontal/none), `Spacing`, `Padding`, `ChildAlignment`, `FitContent` |
| `text label` | behaviour | `Text` (re-read each frame — bind `!expr`/`!text`/`!var` for live values), `FontSize`, `PreferredWidth/Height` |
| `ui button` | trigger | `Label`, `PreferredWidth/Height` — fires `Listeners` on click |
| `ui slider` | trigger | `InitialValue`, `MinValue`, `MaxValue`, `PreferredWidth/Height` — emits output `value` [float] |

```yaml
ui:
  Behaviours:
    canvas: { Type: ui canvas, Properties: { MatchWidthOrHeight: 0.5 } }
  Children:
    hud:
      Behaviours:
        layout:
          Type: ui container
          Properties: { Direction: vertical, Spacing: 12, Padding: 24, ChildAlignment: upper-left }
      Children:
        score:
          Behaviours:
            label:
              Type: text label
              Properties:
                Text: !text { Key: hud.score, Arguments: [ !var score ] }
                FontSize: 30
                PreferredHeight: 44
        quit:
          Behaviours:
            button:
              Type: ui button
              Properties: { Label: !text btn.quit, PreferredWidth: 240, PreferredHeight: 56 }
              Listeners: [ !gameover ]
```

- **Child entity ids are path-joined** — `ui` → `ui/hud` → `ui/hud/score`. Use the full path in
  `EntityId:`; use top-level ids for the behaviours UI buttons/sliders drive.
- **Prerequisite:** leaf blocks instantiate prefabs from a `UiPrefabLibrary`, generated once via
  **Assembler > UI > Generate UI Prefabs** after importing TMP Essentials. Without it, UI won't build.

---

## Lists of values

`IList<T>` properties take plain YAML sequences: `TagsToDetect: [ left paddle, right paddle ]`.

List-typed Variables/Constants can be **seeded** — they don't have to start empty. This is the clean
home for static list data (waypoint routes, spawn tables, level layouts); prefer it over baking values
into a `ternary` and indexing by int. Read a seeded list from an `!expr` like any other (LINQ, or
`route[i]`). Element tags: `!int`, `!float`, `!bool`, `!string`, `!vec`, `!colour`, `!record`; untagged
`[]` is untyped.

```yaml
Constants:
  route: !vec [ { X: -4, Y: 4 }, { X: 4, Y: 4 }, { X: 4, Y: -4 } ]
  spawn weights: !int [ 5, 10, 15 ]
Variables:
  occupied: !vec []        # filled at runtime by `* list add`
```

---

## Recurring composition patterns

Conventions, not rules — reach for them when they fit.

- **Action → setter/motion** for input-driven movement; `Phase: down` for discrete actions, `hold` for
  continuous.
- **`on start trigger`** to seed initial state; **`interval trigger`** for ticking gameplay.
- **`condition gate` / `inverse condition gate`** fed by a periodic trigger for polled win/lose checks.
- **`state machine`** for entity AI — states with `OnEnter`/`OnExit` and ordered `Transitions`.
- **Spawner + per-entity Variables** for objects with individual state, seeded from a `!parameter`.
- **Counter entity** holding `* variable setter` behaviours, targeted by whatever should increment it.
- **Tagged broadcast** for "do this to everything matching", avoiding hard-coded ids.
- **`Outputs:` + `!output` + `!expr`** for reactions needing data from the event, rather than stashing
  values in globals just to move them one hop.
- **`camera` entity + `camera follow` vcam** targeting the player by tag (`Target: { Tag: player }`).

Larger-scale, for anything beyond a single mechanic:

- **Record table → driver.** A `!record [ … ]` constant plus an index variable drives the whole
  progression, so tuning the game is editing one table. (`Gridfall`, `Riftwell`.)
- **Derived board.** `on start trigger` → expression computing a cell list from a few waypoints or a
  rule → `* list loop trigger` (or `Placements:` with an `!expr` `At`) stamps an entity per element,
  and the *same* list is reused as the gameplay rule. (`Gridfall`, `Pacman`.)
- **Global state variable as a bus.** One int/bool (alarm, wave, combo, phase) read live by speeds,
  colours, lights, camera `Priority`, so one write re-dresses the game. (`HollowManorHeist`.)
- **Dual locomotion + selector.** Two motion behaviours write desired velocities to per-entity
  variables; a per-frame setter picks by FSM mode; one `velocity` applies it. Cleaner than
  enabling/disabling motion behaviours. (`HollowManorHeist`.)
- **Broadcast + self-filtering gate.** Rather than resolving which entity was hit/tapped, broadcast to
  an `EntityTag` listener and let each instance's `condition gate` decide. Works post-build. (`Gridfall`.)
- **Pointer → world.** `tap`/`drag`/`ui drag source` → `screen to world` → a cell-snap expression,
  guarded against drops over the UI by a screen-band condition. (`Gridfall`.)

---

## Verifying your work

Run these after writing or editing a descriptor and fix what they report. They boot Unity in batch
mode; the first run in a fresh worktree does a one-time cold import.

| Command | Checks | When |
|---|---|---|
| `unity command validate_yaml --targets <file>` | well-formedness + duplicate keys (structure only) | quick syntax sanity |
| `unity command check_expression --targets <file>` | every embedded `!expr` / `Expressions:` body compiles | after any expression work |
| `unity command validate_game --targets <file>` | builds through **structure → deserialise → parse → resolve → instantiate**, reporting the failing stage | **always**, before handing back |

These run against the running Unity editor and answer in about a second. Each exits non-zero and prints
its report when the check fails. See `Assembler/CLAUDE.md` › Build & Test if no editor is running.

---

## Authoring checklist

- [ ] A flagship was actually read, and this descriptor follows its architecture — data tables and
      `Placements`/list-loops over repeated entity blocks, one parameterised template per family,
      per-entity blackboards, gate chains for rules, presentation bound via `!expr` — at the scale the
      user asked for.
- [ ] Every `Type:` exists verbatim in `Behaviours.md` and isn't parse-only; every `Properties:` key
      and value type matches the catalogue exactly.
- [ ] Every `!var` / `!parameter` id resolves; every `EntityId` exists (full path for nested UI ids);
      every `BehaviourId` is declared on that entity/template; every `!output` matches an upstream
      `Outputs:` binding.
- [ ] Every `input action`'s `Action` is declared under `Controls.Actions` with a binding per platform.
- [ ] Template-internal references use `EntityId: !parameter self_id`.
- [ ] Colliders needing `collision_*` / `trigger_*` have a `rigidbody` on one of the two entities.
- [ ] A `camera` entity with a `camera` behaviour exists.
- [ ] Every entity needing more than one shape uses `model` (not stacked `primitive`s), sized in
      true world metres, grounded with `Anchor: bottom` unless floating on purpose.
- [ ] Every `!expr` uses `{ Do, With }` with `With` as a map, was authored via the
      `unity-expression-compiler` skill, and reuses `Libraries.md` bare-name helpers instead of
      hand-rolled math (with no now-unnecessary `RegisterTypes`/`RegisterTypeStatics` left behind).
- [ ] Every `!output` fed into an inline `!expr` has an explicit `ArgumentTypes` entry.
- [ ] At least one reachable `!gameover` listener exists.
- [ ] User-facing strings go through `!text` + `Localisation:`.
- [ ] `unity command validate_game --targets <file>` and `unity command check_expression --targets <file>` both pass.

---

## Feedback on the catalogue

You are **encouraged** to push back — the catalogue is a living artifact and flagging gaps is part of
this skill's job. Volunteer feedback when you hit:

- **A missing behaviour** whose workaround via chained behaviours/expressions is awkward or impossible.
- **A faulty or surprising behaviour** — a property name, default, or combination that fails to build
  or doesn't match reasonable expectation. Report it with the concrete failing config.
- **Naming inconsistency** — `colour list *` vs `color`, mismatched trigger shapes, same role named
  differently across behaviours.
- **Coverage gaps in a family.** (The variable-setter family is complete — `vector`, `int`, `float`,
  `bool`, `string`, `colour` — so don't claim a missing one without checking.)
- **Composition friction** — five chained behaviours doing what one could express.
- **Parse-only behaviours** the user needs: say they appear in the catalogue but won't execute, and
  offer to work around it or get it implemented first.

Be concrete: *"There's no behaviour that smoothly interpolates a variable, so the HUD bar has to be
re-derived every frame via an expression. Consider a `lerp variable setter`."* Don't gold-plate — only
raise it if it bites this task or would clearly bite the next similar one.

**If something the user wants isn't in the catalogue, don't invent it.** Either compose it from
behaviours that exist, or tell the user and ask whether to (a) work around it, (b) drop the feature, or
(c) author a new behaviour first — a separate task, not part of this skill. If the catalogue is
ambiguous about a property type or role, read its description and Outputs tables carefully; if still
unclear, ask rather than guess.
