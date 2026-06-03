---
name: generate-game-descriptor
description: >
  Use this skill whenever the user asks to author, generate, edit, or review a game descriptor YAML
  file for the Assembler project. Trigger on requests like "make me a Tetris game", "write a yaml for
  a top-down shooter", "add a power-up to this descriptor", "review my descriptor", or any task where
  the deliverable is a `.yaml` game definition under `Assets/ExampleGameDescriptors/`. Also trigger when the user wants feedback on whether existing
  behaviours are sufficient, well-designed, or missing functionality for a game idea — this skill is
  expected to push back on the behaviour catalogue when something is awkward, faulty, or missing.
---

# Generate Game Descriptor

You are authoring a declarative game definition as a YAML file. Each file describes one complete game.
The game is built by composing **entities** out of **behaviours** drawn from a fixed catalogue.

> **Two hard requirements before you write anything.**
>
> 1. **Read the behaviour catalogue.** The full list of available behaviours, their properties, and
>    their trigger outputs lives in [`Assets/docs/Behaviours.md`](../../../Assets/docs/Behaviours.md).
>    Always read it before writing a descriptor. The catalogue is the source of truth — if a behaviour
>    isn't there, it doesn't exist. Do not invent behaviour types, property names, or output names.
> 2. **Use the expression compiler skill for any code.** Any value inside an `Expression:` field is
>    code, not YAML. It must be authored via the [`unity-expression-compiler`](../unity-expression-compiler/SKILL.md)
>    skill — that compiler is strict and the wrong syntax will fail at runtime. Always invoke that
>    skill when writing or editing expression bodies. That skill also documents the **library
>    helpers** (see [`Assets/docs/Libraries.md`](../../../Assets/docs/Libraries.md)): reusable
>    functions like `CellToWorld`, `Rotate2D`, `Clamp`, `RandomFloat`, `LerpColor`, callable by bare
>    name from any expression with no `RegisterTypes` / `RegisterTypeStatics`. Prefer them over
>    hand-rolling vector/scalar/random/colour math.
> 3. **Register a `Test/Build <GameName>` menu item in `Assets/Building/Builder.cs`** whenever you
>    create a new descriptor under `Assets/ExampleGameDescriptors/`. The
>    Unity Editor only exposes games that have a corresponding `[MenuItem("Test/Build …")]` method
>    inside the `#if UNITY_EDITOR` block — without one, the user has no way to launch the game.
>    Follow the existing pattern verbatim (read the file, parse, transform, `Build(gameInfo)`). Do
>    this in the same turn that you write the YAML, without being asked.

You do not need to understand or reference the C# implementation, the build pipeline, the parsing
layer, or the runtime. Treat the descriptor as a self-contained authoring format.

---

## Top-level structure

A descriptor is a single YAML document with these top-level keys. Order is conventional but not
significant. Only `Entities` is strictly required for a working game; everything else is optional.

```yaml
Game:                # metadata
World:               # rendering / dimensionality
Physics:             # global physics settings
Constants:           # compile-time named values
Variables:           # runtime mutable named values
Expressions:         # named code snippets, called from !expr
Templates:           # reusable entity blueprints
Entities:            # the actual entities in the scene
Localisation:        # per-locale string table; referenced via !text
GameOverCondition:   # boolean !expr; game ends when true
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
sizes, key bindings, colours, starting positions).

Names may contain spaces — the **YAML mapping key is the identifier**.

```yaml
Constants:
  paddle up speed: 5
  left paddle up key: "w"
  upper wall position: !vec { X: 0, Y: 3 }
```

### `Variables`
A map of `id → initial value`. Variables are mutable at runtime; behaviours like `*_variable_setter`
write to them, and `!var <id>` reads them. The initial value can be a literal, another `!var`
reference, an `!expr`, or `!parameter` (inside a template).

```yaml
Variables:
  left score: !var initial score   # initialised from a constant
  ball velocity: !vec { X: 3, Y: 3 }
  is dead: false
```

### `Expressions`
Named code snippets that can be called via `!expr`. Each entry:

```yaml
expression name:
  ArgumentTypes:   [ int, int ]              # optional, omit if no args
  ArgumentNames:   [ a, b ]                  # optional, must match ArgumentTypes length
  ReturnType:      int                       # required: int | float | bool | string | vector | colour
  RegisterTypes:   [ UnityEngine.Vector3 ]   # optional; lets the body use the bare type name
  RegisterTypeStatics: [ UnityEngine.Random ]# optional; lets the body call statics without the type prefix
  Expression: "a + b;"                       # the method body
```

The `Expression:` field is code. **Always invoke the [`unity-expression-compiler`](../unity-expression-compiler/SKILL.md)
skill when authoring it.** It is a strict procedural subset of C#; ordinary C# will fail to parse.

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

### `Entities`
A map of `entity id → entity definition`. The entity id is the YAML key. See **Entity structure**
below.

### `GameOverCondition`
A boolean `!expr`. When it evaluates to true, the game ends.

```yaml
GameOverCondition: !expr
  ExpressionId: is game over
  Arguments:
    - !var left score
    - !var right score
    - !var score to win
```

---

## YAML tags (custom types)

These tags appear with a leading `!` and tell the loader how to interpret the value. Use them exactly
as shown.

| Tag | Form | Meaning |
|---|---|---|
| `!vec` | `!vec { X: 0, Y: 0, Z: 0 }` (Z optional) | Vector literal. Z defaults to 0 for 2D games. |
| `!colour` | `!colour red` or `!colour { R: 1, G: 0, B: 0 }` | Named colour (`red`, `blue`, `white`, `cyan`, `grey`, …) or RGB literal. |
| `!var` | `!var some name` | Reads a value by id. Resolves against per-entity variables first, then global Variables, then Constants. This is the only read tag — there is no separate `!const` form. |
| `!parameter` | `!parameter slot_name` | Inside a template, refers to a parameter slot supplied at instantiation. `!parameter self_id` is the special implicit slot for the entity's own id (use when a template behaviour needs to refer to "this entity"). |
| `!expr` | `!expr { ExpressionId: …, Arguments: [ … ] }` | Calls a named expression from the `Expressions:` section. |
| `!output` | `!output local_name` | Reads a trigger output that was bound by an upstream listener (see **Trigger outputs**). |
| `!clock` | `!clock deltaTime` | Reads a property of the game clock (`deltaTime`, `time`, `frameCount`, `unscaledDeltaTime`) as a number. Respects pause / slow-mo (`set timescale`): `deltaTime` is 0 while paused. Use it to pass the frame delta into per-frame `!expr` physics, e.g. `IntegratePosition(pos, vel, dt)` with `dt` supplied as `!clock deltaTime`. |
| `!text` | `!text menu.start` or `!text { Key: hud.score, Arguments: [ !var score ] }` | Resolves a localisation key to a string from the `Localisation:` table. Use the scalar form for static text and the mapping form for text with runtime values — the localised template owns the `{0}`/`{1}` placeholders that the arguments fill. **Always use `!text` for user-facing strings instead of inline literals** (see **Localisation**). |

Lists of values can use either flow `[ a, b ]` or block `- a` syntax — both work.

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
      up_key: !var left paddle up key
  Behaviours:                          # the entity's behaviours
    behaviour id:
      Type: <one of the behaviour types in Behaviours.md>
      Properties: { … }                # whatever properties that behaviour declares
      Listeners: [ … ]                 # optional; only meaningful on triggers
      Tags: [ scoreable ]              # optional; used by behaviour-tag listeners
```

- The **behaviour id** is the YAML key. It must be unique within the entity. Use spaces if you like.
- The **`Type:`** value must be exactly one of the names in
  [`Behaviours.md`](../../../Assets/docs/Behaviours.md). If you need something the catalogue doesn't
  offer, **stop and tell the user** — see **Offering feedback** below.
- **`Properties:`** are whatever that behaviour declares. Property names match the catalogue exactly
  (PascalCase). Types match too: pass a `Vector3` to a `Vector3` property, a `bool` to a `bool`, etc.
- Property values can be literals, `!var`/`!parameter`/`!expr`/`!output` — any expression the loader
  supports.

---

## Listeners — how behaviours are wired together

Triggers (`*_trigger`, `*_trigger trigger`, ui events, etc.) **fire** events. Other behaviours **execute**
when notified. The wiring is the `Listeners:` list on the trigger.

There are three listener forms:

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
2. **Read** the bound name with `!output` in the target behaviour's properties (often inside an
   `!expr` argument list):
   ```yaml
   paddle bounce velocity setter:
     Type: vector variable setter
     Properties:
       VariableId: !var ball velocity
       Value: !expr
         ExpressionId: paddle bounce
         Arguments:
           - !var ball velocity
           - !output hit_point
           - !output paddle_position
           - !var paddle bounce factor
   ```

Only triggers with declared **Outputs** in the catalogue produce values to bind. Don't make up output
names.

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
      up key trigger:
        Type: key hold trigger
        Properties: { Key: !parameter up_key }
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
        up_key: !var left paddle up key
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
| `ui canvas` | behaviour (UI root) | `MatchWidthOrHeight` (float 0..1; CanvasScaler match) |
| `ui container` | behaviour (auto-layout) | `Direction` ("vertical"/"horizontal"), `Spacing`, `Padding`, `ChildAlignment` (e.g. "middle-center","upper-left"), `FitContent` (bool) |
| `text label` | behaviour | `Text` (string; re-read each frame — bind via `!expr`/`!text`/`!var` for live values), `FontSize` (int), `PreferredWidth`, `PreferredHeight` |
| `ui button` | trigger | `Label` (string), `PreferredWidth`, `PreferredHeight` — fires its `Listeners` on click |
| `ui slider` | trigger | `InitialValue`, `MinValue`, `MaxValue`, `PreferredWidth`, `PreferredHeight` — emits output `value` [float] on change |

Layout model (replaces the old `Rect`): leaf blocks expose `PreferredWidth`/`PreferredHeight`
(`<= 0` = let the layout/content decide); the parent `ui container`'s `LayoutGroup` arranges its
child entities in declaration order, and the `CanvasScaler` makes it responsive across screen sizes.

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
  via **Assembler > UI > Generate UI Prefabs** (or `Tools/generate-ui-prefabs.sh`), after importing
  TMP Essentials (`Window > TextMeshPro > Import TMP Essential Resources`). See
  `Assets/ExampleGameDescriptors/UiShowcase.yaml` for a complete worked example.

---

## Lists of values

For `IList<T>`-typed properties (e.g. `TagsToDetect`, `Arguments`, list-variable behaviours), use
plain YAML sequences:

```yaml
TagsToDetect: [ left paddle, right paddle ]
```

To declare an empty list as a Variable's initial value, use the matching tag (e.g. `!vec []` for a
vector list, `[]` for an untyped list — match the surrounding examples in the catalogue's
`*_list_*` behaviours).

---

## Composition patterns that recur

These are conventions, not rules. Reach for them when they fit.

- **Trigger → setter** for keypress-driven movement. `key hold trigger` fires every frame the key is
  held; its listener targets a `velocity` behaviour to drive motion.
- **`key down trigger` for discrete actions** (jump, fire, change direction). Use `key hold trigger`
  for continuous actions.
- **`on start trigger`** to seed initial state (spawn first food, fire first asteroid, play a start
  sound).
- **`interval trigger`** for ticking gameplay (asteroid spawn, score-per-second, periodic checks).
- **`condition trigger`** as a gate: a periodic upstream trigger fires it, it forwards only when the
  expression is true. Use this for win/lose checks polled against variables.
- **Spawner + per-entity Variables** for objects with individual state (enemy health, bullet
  lifetime, bubble lifespan). Seed the per-entity variable from a `!parameter`.
- **Score / counter tracker entity** with one or more `*_variable_setter` behaviours, targeted by
  listeners on the triggers that should increment/decrement.
- **HUD** as a `ui canvas` entity with a `ui container` child that auto-lays-out its child UI
  entities (labels, buttons, sliders). Bind a `text label`'s `Text` to a variable/expression for
  live values. See **UI elements — composable uGUI blocks** above.
- **Tagged broadcast** (`EntityTag`, `BehaviourTag`) for "do this to everything matching" — e.g. one
  keypress destroys every enemy, or scores 1 point per alive enemy. Avoids hard-coding entity ids.
- **`Outputs:` binding + `!output` + `!expr`** for reactions that need data from the trigger event
  (collision contact point, other entity's velocity, slider value, submitted text).

---

## Authoring checklist

Run through this before handing a descriptor back:

- [ ] Every `Type:` value exists verbatim in [`Behaviours.md`](../../../Assets/docs/Behaviours.md).
- [ ] Every `Properties:` key matches the catalogue's property name exactly (PascalCase).
- [ ] Every property's value type matches the catalogue.
- [ ] Every `!var` / `!parameter` id resolves to something declared somewhere reachable.
- [ ] Every `EntityId` refers to an entity that exists (in `Entities:` or a `Template:` containing
      this behaviour).
- [ ] Every `BehaviourId` refers to a behaviour declared on the named entity (or named template).
- [ ] Every `!output` name matches a key on the right-hand side of some upstream listener's
      `Outputs:` map.
- [ ] All references inside a template that point to behaviours on the same instantiated entity use
      `EntityId: !parameter self_id`.
- [ ] Any colliders that need `collision_*` events have a `rigidbody` on at least one of the two
      entities involved (the catalogue notes this for `collision enter trigger`).
- [ ] A `camera` entity exists, with a `camera` behaviour, otherwise nothing renders.
- [ ] Every `Expression:` body has been authored via the `unity-expression-compiler` skill.
- [ ] Math-heavy expressions reuse the bare-name library helpers from
      [`Libraries.md`](../../../Assets/docs/Libraries.md) instead of hand-rolling them, and no
      `RegisterTypeStatics`/`RegisterTypes` entry remains that the helpers made unnecessary.
- [ ] `GameOverCondition` evaluates to `false` initially and there is at least one path to make it
      `true`, OR it is omitted intentionally for an endless game.

---

## Offering feedback on the behaviour catalogue

You are explicitly **encouraged** to push back on the catalogue when something is awkward, missing,
or wrong. The catalogue is a living artifact — flagging gaps is part of this skill's job.

Volunteer feedback when you notice any of the following while authoring or reviewing:

- **A missing behaviour.** The user's intent requires something the catalogue doesn't cover, and the
  workaround via expressions / chained behaviours is awkward or impossible. (Example: "this would be
  cleaner with a `mouse position` trigger output", or "there's no `lerp variable setter` for smooth
  interpolation".)
- **A faulty or surprising behaviour.** A property name, default, or behaviour description that
  doesn't match what the user reasonably expects. (Example: "`spawner` takes Euler `Rotation` in
  degrees but `Position` in world units — easy to confuse with the entity-level `Rotation` field".)
- **A naming inconsistency.** `colour list *` vs `color` elsewhere, `trigger enter trigger` vs `collision
  enter trigger` shape mismatch, or properties with the same role named differently across
  behaviours.
- **Coverage gaps in a family.** If `int`, `float`, `bool`, `string`, `vector` setters exist but
  there is no `colour variable setter` and the user needs one, say so.
- **Composition friction.** Patterns that require five chained behaviours to do something a single
  behaviour could express — surface this as a suggestion for a new behaviour.
- **Doc-gen warnings.** The bottom of `Behaviours.md` lists behaviours that the doc generator
  skipped (`move animation`, `scale animation`, `rotate animation`, `condition`, `trigger stay
  trigger`, `when all`, `when any`, …). If the user needs one, point out that it appears to exist
  but is undocumented, and offer to author around it carefully or ask for the missing doc.

When giving feedback, be concrete:

> "There's no `text variable setter` in the catalogue, so the HUD label has to be rebuilt every
> frame via a format expression. Consider adding `string variable setter` for direct text writes, or
> a `format and set` behaviour for the common case."

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
