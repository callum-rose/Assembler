---
name: generate-game-descriptor
description: >
  ALWAYS use this skill whenever the user asks to create a game OR generate a descriptor of any kind
  for the Assembler project — these are non-negotiable triggers, not just suggestions. This includes
  authoring, generating, editing, or reviewing a game descriptor YAML file. Trigger on ANY request to
  "make a game", "create a game", "build me a game", "generate a descriptor", as well as more specific
  ones like "make me a Tetris game", "write a yaml for a top-down shooter", "add a power-up to this
  descriptor", "review my descriptor", or any task where the deliverable is — or could be — a `.yaml`
  game definition under `Assets/ExampleGameDescriptors/`. When in doubt about whether a request
  involves creating a game or a descriptor, use this skill. Also trigger when the user wants feedback
  on whether existing behaviours are sufficient, well-designed, or missing functionality for a game
  idea — this skill is expected to push back on the behaviour catalogue when something is awkward,
  faulty, or missing.
---

# Generate Game Descriptor

You are authoring a declarative game definition as a YAML file. Each file describes one complete game.
The game is built by composing **entities** out of **behaviours** drawn from a fixed catalogue.

> **Four hard requirements before you write anything.**
>
> 1. **Read the behaviour catalogue.** The full list of available behaviours, their properties, and
>    their trigger outputs lives in [`Assets/docs/Behaviours.md`](../../../Assets/docs/Behaviours.md).
>    Always read it before writing a descriptor. The catalogue is the source of truth — if a behaviour
>    isn't there, it doesn't exist. Do not invent behaviour types, property names, or output names.
>    (The bottom of that file has a **Parse-only behaviours (not yet runnable)** list and a
>    **Doc-gen warnings** list — treat anything mentioned there as unsupported.)
> 2. **Use the expression compiler skill for any code.** Any value inside an `!expr` `Do:` field (when
>    it is an inline body) or inside an `Expressions:` block `Expression:` field is code, not YAML. It
>    must be authored via the [`unity-expression-compiler`](../unity-expression-compiler/SKILL.md)
>    skill — that compiler is strict and the wrong syntax will fail at runtime. Always invoke that
>    skill when writing or editing expression bodies. That skill also documents the **library
>    helpers** (see [`Assets/docs/Libraries.md`](../../../Assets/docs/Libraries.md)): reusable
>    functions like `CellToWorld`, `Rotate2D`, `Clamp`, `RandomFloat`, `LerpColor`, callable by bare
>    name from any expression with no `RegisterTypes` / `RegisterTypeStatics`. Prefer them over
>    hand-rolling vector/scalar/random/colour math.
> 3. **Read a flagship descriptor and match its bar.** Before authoring anything more involved than a
>    single-mechanic demo, read **one of `Gridfall.yaml`, `HollowManorHeist.yaml`, `Riftwell.yaml`
>    end to end** and model your structure and ambition on it. These are the three most systems-dense
>    descriptors in the repo and they are the *default* reference — not the small single-feature
>    demos, which show one behaviour wired minimally and are a poor model for a whole game. See
>    **Reference descriptors** below for what to take from each, and for the complementary
>    descriptors to open when your game has a specific shape (grid board, vehicle physics, endless
>    content, …).
> 4. **Verify the descriptor builds before handing it back.** A new or edited descriptor saved under
>    `Assets/ExampleGameDescriptors/` is **automatically discovered** by the in-editor **`Assembler >
>    Game Launcher`** window (it lists every `*.yaml` in that folder) — there is **no** per-game
>    `MenuItem` to register any more, so do **not** edit `Builder.cs`. Instead, run the headless
>    validators (see **Verifying your work** below): at minimum `Tools/validate-game.sh <file>` to
>    confirm it actually builds through every pipeline stage, and `Tools/check-expression.sh <file>`
>    to confirm every embedded expression compiles. Do this in the same turn that you write the YAML,
>    without being asked.

You do not need to understand or reference the C# implementation, the build pipeline, the parsing
layer, or the runtime. Treat the descriptor as a self-contained authoring format.

> **Structural reference.** The authoritative description of the descriptor's *shape* — every
> top-level section, how entities/behaviours/listeners/templates nest, the value types, the scalar
> inference rules, and every custom YAML tag with its exact form — lives in
> [`Assets/docs/GameDescriptorSchema.md`](../../../Assets/docs/GameDescriptorSchema.md). It is
> generated from the deserialisation DTOs (the parser's source of truth), so consult it when you are
> unsure what keys a section takes or what form a tag accepts. The prose below is the authoring guide;
> the schema is the structural contract. (`Behaviours.md` still owns the behaviour `Type:`/`Properties:`
> catalogue, and `Libraries.md` the expression helpers.)

---

## Reference descriptors — start from the complex ones

`Assets/ExampleGameDescriptors/` holds ~50 files, and most of them are **single-feature demos**
(`TriggerStayDemo`, `LookAtDemo`, `CameraRigsDemo`, …). A demo shows one behaviour wired minimally;
it is the right thing to check when you need that one behaviour's exact property names, and the
wrong thing to imitate when you are building a game. **Default to the flagships below.**

### The three flagships — read one in full before authoring

| File | ~Lines | What to take from it |
|---|---|---|
| [`Gridfall.yaml`](../../../Assets/ExampleGameDescriptors/Gridfall.yaml) | 2250 | **Derived content and pointer-driven building.** A 3D tower defence whose board is *computed*, not hand-painted: an expression walks the authored lane waypoints cell-by-cell into a path-cell list, and that one list drives both the tile spawning (a `* list loop trigger`) and the "can't build on the lane" rule. Drag-to-place via `ui drag source` → `screen to world` → a cell-snap expression, with a live ghost plate (green/amber/red) and a screen-band guard so drops over the UI rail are ignored. Tap-to-upgrade as an entity-tag broadcast where each tower's own `condition gate` decides if it was the one tapped. One tower template × five weapons and one creep template × six kinds, all by `Parameters:`. A `Records:` wave table *is* the campaign. |
| [`HollowManorHeist.yaml`](../../../Assets/ExampleGameDescriptors/HollowManorHeist.yaml) | 1430 | **Agent AI and a global state variable everything reads.** Guards are one template: `perceive` (vision cone + line of sight) writes a per-instance blackboard, a `state machine` reads it, and *two* locomotion behaviours (`patrol` and `navigate`) both emit desired velocities into per-instance variables that a per-frame selector picks between, feeding a single `velocity` integrator. A global 0..3 alarm is read live by guard speed, torch colour, the manor light, and a vcam `Priority` expression. Noise propagation as a `(sequence, position)` pair each guard acknowledges. Record-list inventory folded by LINQ, with carry weight feeding straight into the movement expression. `Navigation:` + wall obstacles; tag-targeted `set behaviour enabled` to kill every camera at once. |
| [`Riftwell.yaml`](../../../Assets/ExampleGameDescriptors/Riftwell.yaml) | 2290 | **Trigger outputs as the data bus.** Five pointer gestures (tap / drag / fling / hold / pinch) where every verb's payload rides the listener edge as an `Outputs:` binding read back with `!output` — tap position into a spawner's velocity, swipe direction *and* distance into a projectile's heading and speed, collision `contact_point` / `other_velocity` / `other_position` into sparks, damage scaling and death-kick direction. `* variable changed trigger` with its `value`/`previous` outputs driving a "+N" readout and a damage-scaled camera shake. Record wave table, decaying combo multiplier, desktop/mobile parity on every gesture. |

### What makes them the right model

These three converge on the same architecture. Reproduce it:

- **Data tables over hand-written entities.** A `Records:` schema plus a `!record [ … ]` literal *is*
  the campaign/level/loot table; `Placements:` and expression-derived cell lists stamp out the board.
  Author the rule once and let expressions, list-loop triggers and placements generate the content —
  don't paste forty near-identical entity blocks.
- **One template, many variants.** Stats, colours, and even the id of a template to spawn are
  `!parameter` slots. Five weapon types and six enemy kinds come out of two templates.
- **Two tiers of state.** Per-entity `Variables:` inside a template are the entity's blackboard
  (mode, target, cooldown-until timestamps); global `Variables:` are the game's nervous system that
  many unrelated behaviours read live.
- **Gates are the logic.** `every frame trigger` → `condition gate` chains (and `state machine` for
  agents) express nearly all rules. Reach for a chain before concluding the catalogue is missing
  something.
- **Presentation is bound, not scripted.** Colours, light intensity, camera `Priority`, and speeds
  are `!expr` over state variables, so the game shows its own state without any update code.
- **Broadcast instead of hardcoding.** Entity-tag and behaviour-tag listeners, so runtime-spawned
  entities participate automatically.
- **Full presentation layer.** Every player-visible string through `!text` + `Localisation:`;
  `Controls` bound for both desktop and mobile, with `Controls.OnScreen` widgets for touch.
- **They explain themselves.** A long `Game.Description` block literal spells out each interlocking
  system, and every section carries comments saying *why*. Write that as you go, not afterwards.

### Complementary references — open the one matching your game's shape

Each of these is the cleanest worked example of one thing the flagships don't cover (or cover only
in passing). Read it *alongside* a flagship, not instead of one.

| Your game needs | Open | Why |
|---|---|---|
| A board held in list variables | [`Tetris.yaml`](../../../Assets/ExampleGameDescriptors/Tetris.yaml) | `GridMath` cell↔world math, move/rotate validated against an `occupied` cell list *before* committing, and a deferred three-phase broadcast (snapshot → apply → rebuild) for row clears. |
| Tile-to-tile movement and chase AI | [`Pacman.yaml`](../../../Assets/ExampleGameDescriptors/Pacman.yaml) | `grid mover` + four-connected `navigate` on a shared nav grid, a dot field generated by one `Placements:` entry whose `At` is an `!expr` returning a `PositionList`, and a global mode flip (frightened ghosts). |
| Terrain that changes at runtime | [`Bomberman.yaml`](../../../Assets/ExampleGameDescriptors/Bomberman.yaml) | `nav obstacle` as a *dynamic* grid obstacle — destroying a soft block frees its cell synchronously, so enemy A* immediately re-routes through the gap you blew open. |
| 3D vehicle physics with AI rivals | [`MiniRacer3D.yaml`](../../../Assets/ExampleGameDescriptors/MiniRacer3D.yaml) | A non-kinematic rigidbody car with grip/drift handling, rivals running the *same* model but steered by A*-to-corner headings through an `ai steer` expression, and checkpoint→lap gating. |
| A first-person camera | [`MazeShooterFps.yaml`](../../../Assets/ExampleGameDescriptors/MazeShooterFps.yaml) | The output `camera` living on the player entity, facing-relative movement (input rotated by yaw), and waves as `timer trigger` → `interval trigger` with a fixed `Count`. |
| Endless content from a fixed pool | [`HelixJump.yaml`](../../../Assets/ExampleGameDescriptors/HelixJump.yaml) | Recycling a fixed ring pool below the player forever, manual gravity via `fixed update trigger` + `add force`, and a contact-below-centre test so only real landings count. |
| A wave loop without a record table | [`WaveSurvival.yaml`](../../../Assets/ExampleGameDescriptors/WaveSurvival.yaml) | The minimal version: three counters (`wave`, `to spawn`, `alive`) polled each frame, with each round's difficulty derived from the wave number. |
| Inventory with per-instance state | [`InventoryDemo2.yaml`](../../../Assets/ExampleGameDescriptors/InventoryDemo2.yaml) | A `record list` variable as the whole inventory, LINQ folds for live HUD counts, and consuming a record *in place* (decrementing its own fields). |
| A real UI tree | [`UiShowcase.yaml`](../../../Assets/ExampleGameDescriptors/UiShowcase.yaml) | Canvas → container → label/button/slider nesting done correctly, with path-joined child entity ids. |

Steering, perception and pathfinding each also have a focused demo (`FlockingDemo`, `PerceptionDemo`,
`PatrolDemo`, `ClearancePathfinding`, `AiConcurrencyShowcase`) — useful for exact property names when
a flagship's usage isn't enough.

### Matching scale to the request

- **"Make me a game"**, or any brief without a scope qualifier → aim at the flagship end: several
  interlocking systems that read each other's state, not one mechanic in isolation.
- **A deliberately small or specific ask** (a clone of one classic, one new mechanic, a demo of one
  behaviour) → keep it small, but keep the flagship *structure*: tables over repetition,
  parameterised templates, gate chains, live-bound visuals, a localised HUD. Fewer systems, same
  discipline.
- Never pad a descriptor with systems the user didn't ask for just to look like a flagship.

### Before you copy a pattern from any example

Descriptors fall out of date as the engine evolves, and some in that folder no longer build.

- **Behaviour `Type:` names and property names come from `Behaviours.md`, never from an example** —
  an older descriptor may predate a rename.
- **Run `Tools/validate-game.sh <that-file>` before modelling your work on it.** Treat a clean run
  (not the file's mere presence) as the signal that its patterns are safe to copy. The flagships are
  the most recently exercised files in the corpus, but the check still applies.

---

## Top-level structure

A descriptor is a single YAML document with these top-level keys. Order is conventional but not
significant. Only `Entities` is strictly required for a working game; everything else is optional.

```yaml
Game:                # metadata
World:               # rendering / dimensionality
Physics:             # global physics settings
Assets:              # project assets (meshes, sprites, clips) referenced via !asset
Constants:           # compile-time named values
Variables:           # runtime mutable named values
Records:             # named record (typed field-bag) schemas, instanced via !record
Controls:            # abstract input actions + per-platform bindings
Expressions:         # named code snippets, called from !expr
Templates:           # reusable entity blueprints
Placements:          # stamp a template out at many positions
Entities:            # the actual entities in the scene
Localisation:        # per-locale string table; referenced via !text
Navigation:          # walkability grid config for pathfinding / perception
```

### `Game`
Free-form metadata. The title is shown to the user; the description is for documentation.

```yaml
Game:
  Title: Simple Pong Game
  Description: A basic implementation of the classic Pong game with two paddles and a ball.
```

### `World`
- `Dimensionality`: `2` or `3`. Almost all examples are 2D.
- `BackgroundColor`: a hex string like `"#000000"`.

### `Physics`
- `Gravity`: a `!vec`. Use `{ X: 0, Y: 0 }` to disable gravity (typical for top-down or arcade games).

### `Constants`
A map of `id → value`. Constants are compile-time named values referenced via `!var <id>` (the same
tag used for Variables — the resolver looks up Variables first, then falls back to Constants). Use
these for any literal that appears more than once, or that the player might want to tune (speeds,
sizes, colours, starting positions).

Names may contain spaces — the **YAML mapping key is the identifier**.

```yaml
Constants:
  paddle up speed: 5
  upper wall position: !vec { X: 0, Y: 3 }
  player colour: !colour { R: 0.3, G: 0.85, B: 0.4, A: 1 }
```

### `Variables`
A map of `id → initial value`. Variables are mutable at runtime; behaviours like `* variable setter`
write to them, and `!var <id>` reads them. The initial value can be a literal, another `!var`
reference, an `!expr`, or `!parameter` (inside a template).

```yaml
Variables:
  left score: !var initial score   # initialised from a constant
  ball velocity: !vec { X: 3, Y: 3 }
  is dead: false
```

### `Records`
Named **record schemas** — typed field bags, each a map of `fieldName → { Type, Default }` (`Type` is
`int` | `float` | `bool` | `string`). Instantiate one with `!record { Type: <schema>, field: value, … }`;
a `!record [ … ]` sequence is a record *list*, and `!record []` an empty one.

```yaml
Records:
  Wave:
    kind:   { Type: string, Default: "normal" }
    count:  { Type: int,    Default: 0 }
    speed:  { Type: float,  Default: 1.0 }
```

This is the flagships' main data-modelling tool: a `!record [ … ]` constant is a **campaign / loot /
spawn table**, and a `!record []` variable is an **inventory** you append to with `record list add`.
Read fields inside expressions with the `RecordHelper` bare-name functions (`GetInt`, `GetFloat`,
`GetString`, `GetBool`, `HasField`, and the `Set*` mutators), and fold a whole list with LINQ:
`bag.Sum(r => GetInt(r, "value"))`. See `Gridfall.yaml` (wave table) and `HollowManorHeist.yaml`
(swag bag).

### `Controls`
The semantic input layer (see **Input — Controls and actions** below). Declares abstract **actions**
and the **per-platform bindings** that feed them, so gameplay is wired to action names rather than
physical keys.

### `Expressions`
Named code snippets that can be called via `!expr { Do: <name>, … }`. Each entry:

```yaml
expressionName:                              # must be a valid identifier — see note below
  ArgumentTypes:   [ int, int ]              # optional, omit if no args
  ArgumentNames:   [ a, b ]                  # optional, must match ArgumentTypes length
  ReturnType:      int                       # required: int | float | bool | string | vector | colour
  RegisterTypes:   [ UnityEngine.Vector3 ]   # optional; lets the body use the bare type name
  RegisterTypeStatics: [ UnityEngine.Random ]# optional; lets the body call statics without the type prefix
  Expression: "a + b;"                       # the method body
```

Unlike entity/behaviour ids, an expression's name (its id, and any `CallableAs` alias) is the literal
name other expression bodies call it by, so it **must be a valid identifier**: letters, digits and
underscores only, not starting with a digit (e.g. `expressionName` or `expression_name`, never
`expression name`). A space or other illegal character is rejected at parse time.

The `Expression:` field is code. **Always invoke the [`unity-expression-compiler`](../unity-expression-compiler/SKILL.md)
skill when authoring it.** It is a strict procedural subset of C#; ordinary C# will fail to parse.

You often **don't need a named `Expressions:` entry at all** — a one-off body can be written inline
in the `!expr { Do: '<body>' }` call site (see **Expressions and `!expr`** below). Reserve the
`Expressions:` block for bodies reused across multiple call sites, or multi-statement bodies you want
to name.

**Prefer the library helpers over registering statics.** Functions documented in
[`Assets/docs/Libraries.md`](../../../Assets/docs/Libraries.md) (e.g. `ScaleVector`, `Rotate2D`,
`IntegratePosition`, `Clamp`, `Max`, `RandomFloat`, `RandomOnCircle`, `RandomColor`, `LerpColor`,
plus all of `GridMath`) are registered globally and callable by bare name — so you usually do **not**
need `RegisterTypeStatics: [ UnityEngine.Random / Mathf ]`, and often not `RegisterTypes` either.
Reach for these first; only register a `UnityEngine.*` type when no helper covers what you need
(`new Vector3(...)` still needs `RegisterTypes: [ UnityEngine.Vector3 ]`, though `new Color(...)` is
already available globally).

### `Templates`
Reusable entity blueprints. An entity that references a template inherits its `Tags`, `Variables`,
and `Behaviours`, with `!parameter` slots filled in at instantiation. See the **Templates** section
below.

### `Placements`
Stamps one template out at many absolute positions without writing an entity per instance. `At` is a
vector list — either a literal sequence of `!vec`, or an `!expr` returning one (build it with a
`PositionList`), which is how a whole dot field / tile grid / obstacle scatter gets authored as a
*rule*:

```yaml
Placements:
  dots:                                  # placement id
    Template: pill
    At: !expr { Do: pillField }          # or a literal list of !vec
    Rotation: !vec { X: 0, Y: 0, Z: 0 }  # optional; shared by every instance
    Parameters: { value: 10 }            # optional; forwarded to the template's slots
    Tags: [ collectable ]                # optional; layered on the template's tags
```

Reach for this before repeating near-identical entity blocks. `Pacman.yaml` and `Bomberman.yaml` are
the worked examples.

### `Entities`
A map of `entity id → entity definition`. The entity id is the YAML key. See **Entity structure**
below.

### `Navigation`
Configures the shared walkability grid that `navigate` (in either `astar` or `flowfield` mode) and
`grid mover` read. Entities tagged `ObstacleTag` are rasterized into it as blocked cells; a
`nav obstacle` behaviour makes an entity a *dynamic* one that frees its cell when destroyed.

```yaml
Navigation:
  CellSize: 0.5
  Bounds: { Min: !vec { X: -12.5, Y: 0, Z: -9.5 }, Max: !vec { X: 12.5, Y: 0, Z: 9.5 } }
  ObstacleTag: wall
  Plane: xz            # "xy" (default, 2D) or "xz" (3D ground plane)
  Diagonal: false      # four-connected; true (default) allows diagonal steps
  DefaultAgentRadius: 0.3   # optional obstacle inflation, overridable per agent
```

Required before any pathfinding behaviour will work. See `HollowManorHeist.yaml` (3D, `xz`) and
`Pacman.yaml` (four-connected maze).

### Ending the game
Every descriptor must declare at least one `!gameover` listener, or the build fails — this guarantees
a game can never get stuck unfinishable. The `!gameover` tag targets the framework's implicit end-game
behaviour, which unloads the whole game. Wire it onto the `Listeners` of any trigger that detects the
ending event (a collision, a key press, a UI button):

```yaml
Listeners:
  - !gameover
```

To end on a continuously-evaluated condition rather than a discrete event, poll it: an
`every frame trigger` feeds a `condition gate` whose `!gameover` listener fires only while the
condition holds.

```yaml
game over:
  Behaviours:
    tick:
      Type: every frame trigger
      Listeners:
        - EntityId: game over
          BehaviourId: gate
    gate:
      Type: condition gate
      Properties:
        Condition: !expr
          Do: is game over
          With:
            left: !var left score
            right: !var right score
            target: !var score to win
      Listeners:
        - !gameover
```

---

## YAML tags (custom types)

These tags appear with a leading `!` and tell the loader how to interpret the value. Use them exactly
as shown.

| Tag | Form | Meaning |
|---|---|---|
| `!vec` | `!vec { X: 0, Y: 0, Z: 0 }` (Z optional) | Vector literal (always a `Vector3`; Z defaults to 0 for 2D). There is no `Vector2` value type — even 2D quantities are `Vector3` with Z=0. |
| `!colour` | `!colour red`, `!colour "#FF8800"`, or `!colour { R: 1, G: 0, B: 0, A: 1 }` | Named colour (`red`, `blue`, `white`, `cyan`, `grey`, …), a hex string (`"#RGB"`, `"#RRGGBB"`, or `"#RRGGBBAA"` — quote it so YAML doesn't treat `#` as a comment), or an RGBA literal (A optional, defaults to 1). |
| `!var` | `!var some name` | Reads a value by id. Resolves against per-entity variables first, then global Variables, then Constants. This is the only read tag — there is no separate `!const` form. |
| `!parameter` | `!parameter slot_name` | Inside a template, refers to a parameter slot supplied at instantiation. `!parameter self_id` is the special implicit slot for the entity's own id (use when a template behaviour needs to refer to "this entity"). |
| `!expr` | `!expr { Do: …, With: { name: value, … } }` | Evaluates code. `Do` is either a **named** expression id (calls an `Expressions:` entry) **or** an inline C# body; `With` is a **map** of named operands. See **Expressions and `!expr`** below. |
| `!output` | `!output local_name` | Reads a trigger output that was bound by an upstream listener (see **Trigger outputs**). |
| `!entity` | `!entity { Id: other_entity_id, Property: Position }` | A **live** read of an entity's transform property (`Position`, `Rotation`, or `Scale`) as a `Vector3`, re-resolved each frame — use it to follow, aim at, or measure distance to another entity (e.g. as an `!expr` `With:` argument). Use the **mapping** form with `Id` and `Property` keys. **Omit `Id`** (`!entity { Property: Rotation }`) to read the **enclosing** entity's own transform — works in both direct and template behaviours, and is preferred over `Id: !parameter self_id`. |
| `!asset` | `!asset some_asset_id` | References a project asset by id for asset-typed properties (`sprite`'s `Sprite`, `voxel mesh`'s `Mesh`, `audio source`'s `Clip`). Use the **scalar** form (the asset id); the mapping form `!asset { Id: … }` fails to deserialise. |
| `!clock` | `!clock deltaTime` | Reads a property of the game clock (`deltaTime`, `time`, `frameCount`, `unscaledDeltaTime`) as a number. Respects pause / slow-mo (`set timescale`): `deltaTime` is 0 while paused. Use it to pass the frame delta into per-frame `!expr` physics, e.g. `IntegratePosition(pos, vel, dt)` with `dt` supplied as `!clock deltaTime`. |
| `!text` | `!text menu.start` or `!text { Key: hud.score, Arguments: [ !var score ] }` | Resolves a localisation key to a string from the `Localisation:` table. Use the scalar form for static text and the mapping form for text with runtime values. **Note the mapping form uses `Arguments:`, not `With:`.** Always use `!text` for user-facing strings instead of inline literals (see **Localisation**). |
| `!gameover` | `- !gameover` (as a listener) | Special listener that ends the game. Add it to any trigger's `Listeners:` list. |

Lists of values can use either flow `[ a, b ]` or block `- a` syntax — both work.

---

## Expressions and `!expr`

Every `!expr` call site uses **one uniform form**: `Do` plus optional `With`. `With` is a **map** of
`name: value` operands — there is no positional `arg0`/`arg1` form.

```yaml
!expr
  Do:   <name-or-inline-body>
  With:                          # optional; a map of named operands
    name1: <value>
    name2: <value>
```

`Do` dispatches **by name first**:

- **Named call** — if `Do` matches an id (or alias) declared in the top-level `Expressions:` block,
  it calls that expression; the `With` keys match the expression's declared `ArgumentNames` (order is
  irrelevant — operands bind by name):
  ```yaml
  Value: !expr
    Do: paddle bounce            # an entry under Expressions:
    With:
      velocity: !var ball velocity
      hit_point: !output hit_point
      factor: !var paddle bounce factor
  ```
- **Inline body** — otherwise `Do` is compiled as an anonymous C# body. Each `With` key is a
  parameter **referenced by name** inside the body:
  ```yaml
  Value: !expr
    Do: 'score + gain'
    With:
      score: !var score
      gain: !var points per pickup
  ```
  A zero-argument inline body needs no `With`:
  ```yaml
  Position: !expr { Do: 'new Vector3(0, RandomFloat(-2f, 2f), 0)', RegisterTypes: [ UnityEngine.Vector3 ] }
  ```

**Inline bodies are still code** — author them with the
[`unity-expression-compiler`](../unity-expression-compiler/SKILL.md) skill, exactly like a named
`Expression:`.

### Inline type hints (optional)

Operand types and the return type are usually **inferred** (literals by kind, `!var` by resolved
value, nested `!expr` by return type, use-site by the property type). When inference can't reach the
answer, an inline `!expr` accepts the same hints as the `Expressions:` block — they override
inference:

```yaml
Value: !expr
  Do: 'current + gain'
  ArgumentTypes: [ int, int ]          # explicit per-operand types (positional to With's declaration order)
  ReturnType: int                      # required where the use-site type is ambiguous (object)
  With:
    current: !parameter score_var      # entity-local / template operand — type not statically known
    gain: !var score per goal
RegisterTypes: [ UnityEngine.Vector3 ] # extra types the body may construct
RegisterTypeStatics: [ UnityEngine.Mathf ]
```

> **`!output` operands are NOT inferred — always give them an explicit `ArgumentTypes` entry.** A
> trigger output's type is only known to the trigger that emits it, so an inline `!expr` defaults an
> `!output` operand to `float`. If you read a non-float output (e.g. a `Vector3` like `mouse_delta`)
> and then access a member (`delta.x`), the body fails to compile (`'x' is not a member of type
> 'System.Single'`). Look up the output's type in the behaviour's **Outputs** table (`Behaviours.md`)
> and declare it:
>
> ```yaml
> Displacement: !expr
>   Do: 'new UnityEngine.Vector3(0f, delta.x * sensitivity, 0f)'
>   ArgumentTypes: [ vector, float ]   # mouse_delta is Vector3 — without this, delta.x won't compile
>   With:
>     delta: !output mouse_delta
>     sensitivity: !var mouse sensitivity
> ```

Reach for `ReturnType` especially in **object contexts** (spawner / template `Parameters:`, and
`!text` / condition arguments), where the use-site type can't be inferred. On a **named** `Do` call
these hints are ignored (the named expression declares its own).

> `!expr` uses `Do`/`With`. The older `ExpressionId`/`Arguments` form is gone — do not use it. (The
> only place `Arguments:` still appears is inside `!text { Key, Arguments }`, which is unrelated.)

---

## Localisation — user-facing text

**All user-facing strings must go through the localisation layer, never inline literals.** This is
cheap to do up front and miserable to retrofit. Whenever you author text a player will read (HUD
labels, instructions, button captions, titles), emit a `!text` key and add the string to the
`Localisation:` table — do **not** write the literal directly into a property or build it with a
string-concatenation `!expr`.

The `Localisation:` block is a per-locale string table:

```yaml
Localisation:
  DefaultLocale: en
  Locales:
    en:
      hud.score: "Score: {0}"
      hud.lives: "Lives: {0}"
      menu.start: "Press Space to start"
```

Reference keys with `!text`:

```yaml
# Static text — scalar form:
Text: !text menu.start

# Dynamic text — mapping form; arguments fill the template's {0}, {1}, … placeholders:
Text: !text { Key: hud.score, Arguments: [ !var score ] }
```

Notes:
- Placeholders use `string.Format` indices (`{0}`, `{1}`); escape literal braces as `{{`/`}}`.
- The template owns word order, so translators can reorder placeholders — prefer the mapping form
  over a format `!expr` for dynamic HUD text.
- A missing key renders as a visible `#key#` marker rather than crashing, so gaps are obvious.
- Only `en` need be authored for now; the layer falls back to the default locale.

---

## Entity structure

```yaml
entity id:
  Tags: [ ball, dynamic ]              # optional; used by tagged listeners and TagsToDetect
  Position: !vec { X: 0, Y: 0 }        # optional
  Rotation: !vec { X: 0, Y: 0, Z: 0 }  # optional
  Template:                            # optional; mutually compatible with inline Behaviours
    Id: paddle_template
    Parameters:
      up_action: move-left-up
  Behaviours:                          # the entity's behaviours
    behaviour id:
      Type: <one of the behaviour types in Behaviours.md>
      Properties: { … }                # whatever properties that behaviour declares
      Listeners: [ … ]                 # optional; only meaningful on triggers
      Tags: [ scoreable ]              # optional; used by behaviour-tag listeners
  Children:                            # optional; nested child entities (same entity shape)
    child id:
      Behaviours: { … }
```

- The **behaviour id** is the YAML key. It must be unique within the entity. Use spaces if you like.
- The **`Type:`** value must be exactly one of the names in
  [`Behaviours.md`](../../../Assets/docs/Behaviours.md). If you need something the catalogue doesn't
  offer, **stop and tell the user** — see **Offering feedback** below.
- **`Properties:`** are whatever that behaviour declares. Property names match the catalogue exactly
  (PascalCase). Types match too: pass a `Vector3` to a `Vector3` property, a `bool` to a `bool`, etc.
- Property values can be literals, `!var`/`!parameter`/`!expr`/`!output` — any expression the loader
  supports.
- **`Children:`** nests entities under this one (used heavily by UI). Child entity ids are
  path-joined onto the parent (see **UI elements**).

---

## Input — Controls and actions

All input is read through the **action layer**: declare abstract **actions** and bind physical inputs
to them **per platform** in the top-level `Controls:` block, then listen to an action with the
`input action` behaviour. This keeps gameplay independent of physical bindings and supports multiple
platforms. There are no raw `key`/`mouse`/`axis`/`gamepad` triggers — a key, mouse button, mouse
position, scroll wheel, or gamepad control is just a binding on an action.

```yaml
Controls:
  Actions:
    move: { Type: value, ValueType: vector2 }   # value action — emits axis/x/y every frame
    jump: { Type: button, Phase: down }          # button action — Phase: down | up | hold
    quit: { Type: button, Phase: down }

  Bindings:
    desktop:                                      # platform/scheme key (e.g. desktop, gamepad)
      jump: [ "<Keyboard>/space" ]                # a scalar = a single control path
      quit: [ "<Keyboard>/escape", "<Keyboard>/q" ]
      move:                                       # a mapping with Composite = a composite binding
        - Composite: 2DVector
          Up: "<Keyboard>/w"
          Down: "<Keyboard>/s"
          Left: "<Keyboard>/a"
          Right: "<Keyboard>/d"
    gamepad:
      jump: [ "<Gamepad>/buttonSouth" ]
      quit: [ "<Gamepad>/start" ]
      move: [ "<Gamepad>/leftStick" ]
```

- **Action `Type: button`** with `Phase: down | up | hold` fires once on press (`down`), once on
  release (`up`), or every frame held (`hold`).
- **Action `Type: value`** (e.g. `ValueType: vector2`) emits the outputs `axis` (`Vector3`),
  `x` (`float`), `y` (`float`) every frame.

The `input action` behaviour is a trigger — wire its `Listeners:` like any other trigger:

```yaml
move action:
  Type: input action
  Properties: { Action: move }          # must match a key under Controls.Actions
  Listeners:
    - EntityId: player
      BehaviourId: apply move
apply move:
  Type: translate
  Properties:
    Displacement: !expr
      Do: 'new UnityEngine.Vector3(x * step, y * step, 0f)'
      With:
        x: !output x
        y: !output y
        step: !var move step
```

`InputActionDemo.yaml` is the canonical worked example; `GameOverDemo.yaml` shows the simplest
button-action wiring.

### Mouse, scroll and gamepad

These are bindings too — no special trigger types:

- **Mouse buttons** → button actions bound to `<Mouse>/leftButton` / `rightButton` / `middleButton`.
- **Mouse position / movement** → a `value` action (`ValueType: vector2`) bound to `<Mouse>/position`
  (absolute, screen space) or `<Mouse>/delta` (per-frame movement). It emits `axis`/`x`/`y` every
  frame. `FlockingDemo.yaml` reads the cursor this way.
- **Scroll wheel** → a `value` action bound to `<Mouse>/scroll` (the `y` output is vertical scroll).
- **Gamepad** → bind controls like `<Gamepad>/buttonSouth`, `<Gamepad>/leftStick` under a `gamepad`
  scheme, as in the example above.

### Touch gestures

Higher-level gesture **recognizers** (`tap trigger`, `swipe trigger`, `drag trigger`,
`pinch and rotate trigger`, `long press trigger`, `double tap trigger`) are separate trigger
behaviours, not `Controls` bindings — see `Behaviours.md`. On-screen touch widgets (joystick / dpad /
button) are declared under `Controls.OnScreen` and drive existing actions.

---

## Behaviour families (orientation only — `Behaviours.md` is authoritative)

A quick map of what exists so you reach for the right behaviour. **Names and properties must be taken
verbatim from `Behaviours.md`** — this list is not exhaustive and omits properties.

- **Visuals (no asset needed):** `cube gizmo`, `sphere gizmo`, `line gizmo` (debug shapes with
  `Size`/`Radius`, `IsWire`, `Colour`); `primitive` (`Shape: cube|sphere|capsule|cylinder|plane|quad`
  + `Colour`/`Size`). These are how the example games draw players, balls, walls, etc.
  **Caveat:** the gizmo behaviours draw via `OnDrawGizmos`, so they only render in the editor Scene
  view (or the Game view with Gizmos toggled on) — **never in a built player or the default Game
  view**. Prefer `primitive` for geometry that must render in-game; reach for gizmos only as
  editor-time debug overlays.
- **Visuals (asset-backed):** `sprite` (a `Sprite` asset), `voxel mesh` (a `Mesh`), `audio source`
  (an `AudioClip`).
- **Physics bodies & colliders:** `rigidbody`, `box collider`, `sphere collider`, `capsule collider`,
  `mesh collider` (set `IsTrigger: true` for overlap-only). A `collision_*` / `trigger_*` event needs
  a `rigidbody` on at least one of the two entities.
- **Forces & motion:** `velocity`, `acceleration`, `translate`, `add force`, `add impulse`,
  `add torque`, `set velocity`, `set angular velocity`, `drag`, `speed limit`, `move towards`,
  `smooth move`, `clamp position`, `wrap position`, `angular velocity`, `rotate`, `rotation setter`,
  `position setter`.
- **Triggers (events):** input triggers (above); time/lifecycle (`on start trigger`,
  `every frame trigger`, `interval trigger`, `timer trigger`, `deferred trigger`, `debounced trigger`,
  `throttled trigger`); collision/overlap (`collision enter/exit/stay trigger`, `trigger enter/exit
  trigger`); list iteration (`* list loop trigger`).
- **Control flow:** `condition gate` (forwards an upstream trigger only when its `Condition` is true),
  `inverse condition gate` (only when false), `exclusive trigger` (only the first in a `Group` to fire
  this frame), `state machine` (FSM with states/transitions for AI).
- **State (variable setters):** `vector / int / float / bool / string / colour variable setter`, and a
  full family of typed list ops (`* list add / insert / remove / remove at / set / set at / clear /
  add range`).
- **Camera:** `camera` (the output camera + Cinemachine brain; `View: orthographic`/perspective,
  `Size`), `camera follow` (a follow/look-at vcam; `Target`/`LookAt` as `{ Tag: … }` or `{ Id: … }`,
  `Mode: 2d|3d`). **Every game needs a `camera` entity or nothing renders.**
- **Spawning & lifecycle:** `spawner` (instantiates a template at runtime), `destroy`, `set active`,
  `toggle active`, `set timescale`, `active poll`.
- **UI:** `ui canvas`, `ui container`, `text label`, `ui button`, `ui slider` — see **UI elements**.

---

## Listeners — how behaviours are wired together

Triggers (`*_trigger`, `input action`, ui events, etc.) **fire** events. Other behaviours **execute**
when notified. The wiring is the `Listeners:` list on the trigger.

There are three listener forms, plus the `!gameover` shorthand:

### Direct listener — target a named behaviour on a named entity

```yaml
Listeners:
  - EntityId: ball spawner
    BehaviourId: spawn ball
```

If both `EntityId` and `BehaviourId` are inside a template and refer to the template's own behaviours,
use `EntityId: !parameter self_id` so the reference resolves to the instantiated entity, not the
template literal.

### Entity-tag listener — target a behaviour on every entity carrying a tag

```yaml
Listeners:
  - EntityTag: !var target tag
    BehaviourId: self destruct
```

The set of matching entities is resolved **at notify time**, not at build time, so entities spawned
later are still picked up. `EntityTag` is a full `ValueSource<string>` — it can be a literal, a
`!var`, or an `!expr`.

### Behaviour-tag listener — target every behaviour carrying a tag

```yaml
Listeners:
  - BehaviourTag: !var scoreable tag
```

No entity is mentioned. Every behaviour anywhere that has `Tags: [ scoreable ]` will execute. This is
the broadest dispatch — useful for "score all", "freeze all", "reset all" patterns.

### `!gameover` — end the game

```yaml
Listeners:
  - !gameover
```

Ends and unloads the game. At least one reachable `- !gameover` listener is required — it's the only
way to end a game, and the build fails without one. See **Ending the game** for the per-frame-condition
pattern.

### Trigger outputs

Some triggers emit named outputs (see the **Outputs** tables in `Behaviours.md`). To use an output
downstream:

1. **Bind** the output to a local name in the listener:
   ```yaml
   - EntityId: !parameter self_id
     BehaviourId: paddle bounce velocity setter
     Outputs:
       contact_point: hit_point
       other_position: paddle_position
   ```
2. **Read** the bound name with `!output` in the target behaviour's properties (often as a value
   inside an `!expr` `With:` map):
   ```yaml
   paddle bounce velocity setter:
     Type: vector variable setter
     Properties:
       VariableId: !var ball velocity
       Value: !expr
         Do: paddle bounce
         With:
           velocity: !var ball velocity
           hit_point: !output hit_point
           paddle_position: !output paddle_position
           factor: !var paddle bounce factor
   ```

Only triggers with declared **Outputs** in the catalogue produce values to bind. Don't make up output
names.

When you feed an `!output` into an **inline** `!expr` (a `Do:` body, not a named expression), declare
its type with `ArgumentTypes` — the type is read from the trigger's **Outputs** table, not inferred,
and defaults to `float` otherwise. See **Inline type hints** above.

---

## Templates

Templates are reusable entity definitions. Anywhere an entity could appear, an entity can instead
say `Template: { Id: my_template, Parameters: { … } }` and inherit its `Tags`, `Variables`, and
`Behaviours`.

```yaml
Templates:
  paddle_template:
    Tags: [ paddle ]
    Variables:                # per-entity variables; each instance owns its own copy
      health: !parameter initial_health
    Behaviours:
      up action:
        Type: input action
        Properties: { Action: !parameter up_action }
        Listeners:
          - EntityId: !parameter self_id
            BehaviourId: move up
      move up:
        Type: velocity
        Properties:
          Velocity: !vec { X: 0, Y: !var paddle up speed }
```

Then the entity:

```yaml
Entities:
  left paddle:
    Template:
      Id: paddle_template
      Parameters:
        up_action: move-left-up
        initial_health: 3
    Position: !var left paddle initial position
    Tags: [ left paddle ]      # additional tags layered on top of the template's tags
```

Key points:

- **`self_id`** is the implicit parameter every template has. Use `EntityId: !parameter self_id`
  wherever a behaviour wants to refer to its own entity.
- **Per-entity Variables** declared inside `Templates: <id>: Variables:` are scoped to each spawned
  instance. `!var lifetime` inside that template resolves to *that* instance's `lifetime`, not a
  global one. This is the standard pattern for per-entity health, lifetime, ammo, etc.
- **Spawners** (`Type: spawner`) instantiate templates at runtime, passing `Parameters:` for the
  template's slots, including parameters that seed per-entity Variables.
- An entity can both use a `Template:` *and* declare extra `Behaviours:` — they layer on top.

---

## UI elements — composable uGUI blocks

The UI system is **composable uGUI**, built from these blocks (NOT IMGUI, NOT a `Rect`/anchor
model — those were removed). A UI element is an entity (GameObject), so UI composes with the *same*
`Children` nesting as everything else: a `ui canvas` entity holds child entities; a `ui container`
entity's children are auto-arranged.

| Block | Kind | Key properties |
|---|---|---|
| `ui canvas` | behaviour (UI root) | `MatchWidthOrHeight` (float 0..1; CanvasScaler match), `ReferenceResolution` (`!vec`; design resolution, X=width Y=height) |
| `ui container` | behaviour (auto-layout) | `Direction` ("vertical"/"horizontal"/"none" — "none" = no layout group, manual placement), `Spacing`, `Padding`, `ChildAlignment` (e.g. "middle-center","upper-left"), `FitContent` (bool) |
| `text label` | behaviour | `Text` (string; re-read each frame — bind via `!expr`/`!text`/`!var` for live values), `FontSize` (int), `PreferredWidth`, `PreferredHeight` |
| `ui button` | trigger | `Label` (string), `PreferredWidth`, `PreferredHeight` — fires its `Listeners` on click |
| `ui slider` | trigger | `InitialValue`, `MinValue`, `MaxValue`, `PreferredWidth`, `PreferredHeight` — emits output `value` [float] on change |

Layout model (replaces the old `Rect`): leaf blocks expose `PreferredWidth`/`PreferredHeight`
(omit for a sensible default); the parent `ui container`'s layout group arranges its child entities in
declaration order, and the `CanvasScaler` makes it responsive across screen sizes.

> **Do not use the legacy `text label` form with `Rect:` / `Anchor` / `Label:` properties.** A few
> older descriptors still carry it; those extra properties are silently ignored under the current
> uGUI label, so the text may not lay out as intended. Always place a `text label` inside a
> `ui canvas` → `ui container` tree and size it with `PreferredWidth`/`PreferredHeight`, as in
> `UiShowcase.yaml`.

```yaml
ui:                                  # canvas root entity
  Behaviours:
    canvas: { Type: ui canvas, Properties: { MatchWidthOrHeight: 0.5 } }
  Children:
    hud:                             # a vertical auto-layout container
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
                Text: !text { Key: hud.score, Arguments: [ !var score ] }   # live, localised
                FontSize: 30
                PreferredHeight: 44
        volume:
          Behaviours:
            slider:
              Type: ui slider
              Properties: { InitialValue: 1, MinValue: 0, MaxValue: 10, PreferredWidth: 320, PreferredHeight: 30 }
              Listeners:
                - EntityId: settings           # slider's `value` output -> a variable setter
                  BehaviourId: set volume
        quit:
          Behaviours:
            button:
              Type: ui button
              Properties: { Label: !text btn.quit, PreferredWidth: 240, PreferredHeight: 56 }
              Listeners:
                - !gameover
```

Key points:
- **Nested child entity ids are path-joined** (entity `ui` with child `hud` → `ui/hud`; its child
  `score` → `ui/hud/score`). Use the full path in `EntityId:` when targeting a UI element, and use
  top-level entity ids (e.g. `settings`, `scorer`) for the behaviours UI buttons/sliders drive.
- **`ui slider` output**: bind/read `value` like any trigger output — e.g. a listener targeting a
  `float variable setter` whose `Value: !output value`.
- **`ui button`/`ui slider` are triggers** — wire `Listeners` exactly like any other trigger
  (direct `EntityId`/`BehaviourId`, tags, or `- !gameover`).
- **Prerequisite**: the leaf blocks (`text label`, `ui button`, `ui slider`) instantiate prefabs
  from a `UiPrefabLibrary` asset. It must exist before a UI descriptor will build — generate it once
  via the **Assembler > UI > Generate UI Prefabs** editor menu, after importing
  TMP Essentials (`Window > TextMeshPro > Import TMP Essential Resources`). See
  `Assets/ExampleGameDescriptors/UiShowcase.yaml` for a complete worked example.

---

## Lists of values

For `IList<T>`-typed properties (e.g. `TagsToDetect`, list-variable behaviours), use plain
YAML sequences:

```yaml
TagsToDetect: [ left paddle, right paddle ]
```

A list-typed Variable/Constant can be **seeded with initial elements** — it does not have to start
empty and be filled at runtime. Use the matching element tag and list the elements inline:

```yaml
Constants:
  route: !vec [ { X: -4, Y: 4 }, { X: 4, Y: 4 }, { X: 4, Y: -4 }, { X: -4, Y: -4 } ]   # four waypoints
  spawn weights: !int [ 5, 10, 15 ]
Variables:
  occupied: !vec []        # an empty list, to be filled at runtime by `* list add`
```

This is the clean home for static list data (waypoint routes, spawn tables, level layouts) — prefer
it over baking values into a `ternary` expression and indexing by int. Read a seeded list from an
`!expr` like any other list value (LINQ, or `route[i]` indexing). The tagged element kinds are
`!int`, `!float`, `!bool`, `!string`, `!vec`, `!colour`, `!record`; an untagged `[]` is an untyped
list. Match the surrounding examples in the catalogue's `* list *` behaviours.

---

## Composition patterns that recur

These are conventions, not rules. Reach for them when they fit.

- **Action → setter or motion** for input-driven movement. An `input action` fires; its listener
  targets a `velocity`/`translate` behaviour to drive motion.
- **`input action` button on `Phase: down`** for discrete actions (jump, fire, change direction).
  Use `hold` for continuous actions.
- **`on start trigger`** to seed initial state (spawn first food, fire first asteroid, play a start
  sound).
- **`interval trigger`** for ticking gameplay (asteroid spawn, score-per-second, periodic checks).
- **`condition gate` / `inverse condition gate`** as a gate: a periodic upstream trigger fires it, it
  forwards only when its `Condition` expression is true (resp. false). Use for win/lose checks polled
  against variables.
- **`state machine`** for entity AI: declare states with `OnEnter`/`OnExit` hooks and ordered
  `Transitions` whose `when` conditions drive the FSM.
- **Spawner + per-entity Variables** for objects with individual state (enemy health, bullet
  lifetime, bubble lifespan). Seed the per-entity variable from a `!parameter`.
- **Score / counter tracker entity** with one or more `* variable setter` behaviours, targeted by
  listeners on the triggers that should increment/decrement.
- **HUD** as a `ui canvas` entity with a `ui container` child that auto-lays-out its child UI
  entities (labels, buttons, sliders). Bind a `text label`'s `Text` to a variable/expression for
  live values. See **UI elements** above.
- **Tagged broadcast** (`EntityTag`, `BehaviourTag`) for "do this to everything matching" — e.g. one
  keypress destroys every enemy, or scores 1 point per alive enemy. Avoids hard-coding entity ids.
- **`Outputs:` binding + `!output` + `!expr`** for reactions that need data from the trigger event
  (collision contact point, other entity's velocity, slider value, axis x/y).
- **Camera that follows the player**: a `camera` entity plus a `camera follow` vcam targeting the
  player by tag (`Target: { Tag: player }`). Check `Behaviours.md` for the `Mode`/offset properties,
  and validate the result builds (see **Verifying your work**).

The flagship descriptors add a handful of larger-scale patterns worth reaching for on anything
beyond a single-mechanic game (see **Reference descriptors**):

- **Record table → driver.** A `!record [ … ]` constant holds the wave/level/loot table; an index
  variable plus expressions reading fields off `table[i]` drive the whole progression, so tuning the
  game is editing one table. (`Gridfall`, `Riftwell`.)
- **Derived board.** An `on start trigger` runs an expression that computes a cell/position list from
  a few authored waypoints or a rule, then a `* list loop trigger` (or a `Placements:` entry with an
  `!expr` `At`) stamps an entity per element — and the *same* list is reused as the gameplay rule
  (walkable, buildable, blocked). (`Gridfall`, `Pacman`.)
- **Global state variable as a bus.** One `int`/`bool` variable (alarm level, wave, combo, phase) that
  many unrelated behaviours read live — speeds, colours, light intensity, camera `Priority` — so a
  single write re-dresses the whole game. (`HollowManorHeist`.)
- **Dual locomotion + selector.** Two motion behaviours write desired velocities into per-entity
  variables; a per-frame `* variable setter` picks between them by FSM mode; one `velocity`
  integrator applies the result. Cleaner than enabling/disabling motion behaviours. (`HollowManorHeist`.)
- **Broadcast + self-filtering gate.** Instead of resolving which entity was hit/tapped/selected,
  broadcast to an `EntityTag` listener and let each instance's own `condition gate` decide whether it
  is the one. Works for entities spawned after build. (`Gridfall`.)
- **Pointer → world.** `tap`/`drag` triggers or a `ui drag source` feed a `screen to world` behaviour
  that unprojects through the live camera; a small expression snaps the result to a cell. Guard
  against drops over the UI with a screen-band condition. (`Gridfall`.)
- **Outputs as the payload.** Bind a trigger's `Outputs:` at the listener and read them with
  `!output` downstream, rather than stashing values in globals just to move them one hop.
  (`Riftwell`.)

---

## Verifying your work

The repo ships fast headless validators — **run them after writing or editing a descriptor** (and
fix what they report) before handing it back. They boot Unity in batch mode; the first run in a fresh
worktree does a one-time cold import.

| Script | Checks | When |
|---|---|---|
| `Tools/validate-yaml.sh <file>` | YAML well-formedness + duplicate keys (structure only) | quick syntax sanity |
| `Tools/check-expression.sh <file>` | every embedded `!expr` / `Expressions:` body compiles | after any expression work |
| `Tools/validate-game.sh <file>` | the descriptor builds through **structure → deserialise → parse → resolve → instantiate**, reporting the exact failing stage | always, before handing back |

`validate-game.sh` is the authoritative "does it actually build" check. Pass a single file to scope
it; with no argument it sweeps everything in `Assets/ExampleGameDescriptors/`.

### Learning from the example descriptors

See **Reference descriptors** near the top of this skill: default to the three flagships
(`Gridfall.yaml`, `HollowManorHeist.yaml`, `Riftwell.yaml`) for structure and ambition, open the
matching complementary descriptor for your game's shape, and validate any file with
`Tools/validate-game.sh` before copying its patterns.

---

## Authoring checklist

Run through this before handing a descriptor back:

- [ ] A flagship descriptor (`Gridfall` / `HollowManorHeist` / `Riftwell`) was actually read, and this
      descriptor follows its architecture — data tables and `Placements`/list-loops over repeated
      entity blocks, one parameterised template per family of variants, per-entity blackboard
      variables, gate chains for rules, presentation bound to state via `!expr` — at a scale that
      matches what the user asked for.
- [ ] Every `Type:` value exists verbatim in [`Behaviours.md`](../../../Assets/docs/Behaviours.md)
      (and is not in the **Parse-only (not yet runnable)** list).
- [ ] Every `Properties:` key matches the catalogue's property name exactly (PascalCase).
- [ ] Every property's value type matches the catalogue.
- [ ] Every `!var` / `!parameter` id resolves to something declared somewhere reachable.
- [ ] Every `EntityId` refers to an entity that exists (in `Entities:` or a `Template:` containing
      this behaviour); use the full path (`ui/hud/score`) for nested child entities.
- [ ] Every `BehaviourId` refers to a behaviour declared on the named entity (or named template).
- [ ] Every `!output` name matches a key on the right-hand side of some upstream listener's
      `Outputs:` map.
- [ ] Every `input action`'s `Action` matches a key declared under `Controls.Actions`, and each action
      has at least one binding per platform you target.
- [ ] All references inside a template that point to behaviours on the same instantiated entity use
      `EntityId: !parameter self_id`.
- [ ] Any colliders that need `collision_*` / `trigger_*` events have a `rigidbody` on at least one of
      the two entities involved.
- [ ] A `camera` entity exists, with a `camera` behaviour, otherwise nothing renders.
- [ ] Every `!expr` uses `{ Do, With }` with `With` as a **map** of named operands (never the
      positional `arg0`/`arg1` form, never `ExpressionId`/`Arguments`), and every inline `Do:` body
      references its operands by their `With` key names and has been authored via the
      `unity-expression-compiler` skill.
- [ ] Math-heavy expressions reuse the bare-name library helpers from
      [`Libraries.md`](../../../Assets/docs/Libraries.md) instead of hand-rolling them, and no
      `RegisterTypeStatics`/`RegisterTypes` entry remains that the helpers made unnecessary.
- [ ] At least one `!gameover` listener exists and is reachable (a discrete event, or a
      `condition gate` polled by an `every frame trigger`), so the game can actually end.
- [ ] User-facing strings go through `!text` + `Localisation:`, not inline literals.
- [ ] `Tools/validate-game.sh <file>` and `Tools/check-expression.sh <file>` both pass.

---

## Offering feedback on the behaviour catalogue

You are explicitly **encouraged** to push back on the catalogue when something is awkward, missing,
or wrong. The catalogue is a living artifact — flagging gaps is part of this skill's job.

Volunteer feedback when you notice any of the following while authoring or reviewing:

- **A missing behaviour.** The user's intent requires something the catalogue doesn't cover, and the
  workaround via expressions / chained behaviours is awkward or impossible.
- **A faulty or surprising behaviour.** A property name, default, or behaviour that doesn't match what
  the user reasonably expects, or one that fails to build for a particular property combination. (If a
  validator surfaces a stage failure that traces to a specific behaviour/property — e.g. a property
  whose type doesn't round-trip through the parser — report it concretely with the failing config.)
- **A naming inconsistency.** `colour list *` vs `color` elsewhere, `trigger enter trigger` vs
  `collision enter trigger` shape mismatch, or properties with the same role named differently across
  behaviours.
- **Coverage gaps in a family.** If a setter or list op is missing for a type the user needs, say so.
  (Note the variable-setter family is now complete — `vector`, `int`, `float`, `bool`, `string`, and
  `colour` setters all exist — so don't claim a missing one without checking the catalogue.)
- **Composition friction.** Patterns that require five chained behaviours to do something a single
  behaviour could express — surface this as a suggestion for a new behaviour.
- **Parse-only / undocumented behaviours.** The bottom of `Behaviours.md` lists behaviours that parse
  but have **no runtime implementation** (currently `condition`, `trigger stay trigger`, `when all`,
  `when any`) plus any **Doc-gen warnings**. If the user needs one, point out that it appears in the
  catalogue but won't execute, and offer to work around it or ask for it to be implemented first.

When giving feedback, be concrete:

> "There's no behaviour that smoothly interpolates a variable, so the HUD bar has to be re-derived
> every frame via an expression. Consider adding a `lerp variable setter` for the common case."

For HUD text with runtime values, prefer a `!text { Key, Arguments }` (the localised template owns the
placeholders) over a string-concatenation format `!expr` — see **Localisation**.

Don't gold-plate — only raise it if it actually bites the current task or would clearly bite the
next similar one.

---

## When you don't know something

If a behaviour the user wants isn't in the catalogue: **don't invent it**. Either find an equivalent
composition using behaviours that do exist, or tell the user it's missing and ask whether to (a)
work around it, (b) drop the feature, or (c) author a new behaviour first (which is a separate task,
not part of this skill).

If a property type or behaviour role is ambiguous in the catalogue, read the description and outputs
tables in `Behaviours.md` carefully; if still unclear, ask the user before guessing.
