# Game Descriptor Schema

The structural contract for a game-descriptor YAML file. This document describes **how a descriptor
is constructed** — the top-level sections, every nested shape, the value types, and the custom YAML
tags — so the structure can be produced and validated without reading the C# implementation.

It is derived from the deserialisation DTOs in `Assets/Deserialisation/Dtos/` (the parser's
source-of-truth shapes) and the tag registry in `Assets/Deserialisation/GameFileParser.cs`. When this
doc and the DTOs disagree, **the DTOs win** — update this doc.

This schema covers **structure only**. It is one of three reference docs; each owns a different axis:

| Doc | Owns | Use it for |
|---|---|---|
| **`GameDescriptorSchema.md`** (this file) | The document shape: sections, nesting, value types, tags | "What keys exist and how do they nest?" |
| [`Behaviours.md`](Behaviours.md) | The behaviour catalogue: every `Type:`, its `Properties:`, its trigger `Outputs` | "What behaviour types and property names are legal?" |
| [`Libraries.md`](Libraries.md) | Global expression helpers callable by bare name from any `!expr` | "What functions can an expression body call?" |

> **Behaviour `Type:` and `Properties:` names are NOT defined here.** They live in
> [`Behaviours.md`](Behaviours.md) and are the source of truth — never invent a behaviour type,
> property name, or output name. This schema only describes the *slots* those values sit in.

---

## File location and discovery

- A descriptor is a **single YAML document** saved under `Assets/ExampleGameDescriptors/*.yaml`.
- Files in that folder are **auto-discovered** by the in-editor `Assembler > Game Launcher` window.
  There is no per-game registration step — do not edit `Builder.cs`.
- One file = one complete game.

---

## Top-level document structure

The document is a single YAML mapping. Its keys map **verbatim** (PascalCase, case-sensitive) onto the
`GameDto` record — the parser uses a null naming convention, so the YAML key *is* the property name.
Key order is conventional, not significant. Unknown top-level keys are ignored.

| Key | Shape | Required | Purpose |
|---|---|---|---|
| `Game` | `InfoDto` mapping | optional | Title / description metadata. |
| `World` | `WorldDto` mapping | optional | Dimensionality + background colour. |
| `Physics` | `PhysicsDto` mapping | optional | Global physics (gravity). |
| `Assets` | list of `AssetDto` | optional | Project assets (meshes, sprites, clips) referenced by `!asset`. |
| `Constants` | map `id → value` | optional | Compile-time named values; read with `!var`. |
| `Variables` | map `id → value` | optional | Runtime-mutable named values; read with `!var`, written by setter behaviours. |
| `Records` | map `schemaName → fieldMap` | optional | Named record (typed field-bag) schemas; instanced with `!record`. |
| `Expressions` | map `id → ExpressionDto` | optional | Named, reusable code snippets; called from `!expr`. |
| `Templates` | map `id → EntityDto` | optional | Reusable entity blueprints; instantiated by `Template:` refs and spawners. |
| `Entities` | map `id → EntityDto` | **the only effectively-required section** | The actual entities in the scene. |
| `Controls` | `ControlsDto` mapping | optional | Abstract input actions + per-platform bindings. |
| `Localisation` | `LocalisationDto` mapping | optional | Per-locale string table; read with `!text`. |
| `Navigation` | `NavigationDto` mapping | optional | Walkability grid config for pathfinding/perception. |

> **Hard build requirement (not structural but enforced):** at least one reachable `!gameover`
> listener must exist somewhere, or the build fails. See [Listeners](#listeners).

---

## Sections in detail

### `Game` — `InfoDto`

```yaml
Game:
  Title: Simple Pong Game            # string, optional — shown to the user
  Description: A basic Pong clone.    # string, optional — documentation only
```

### `World` — `WorldDto`

```yaml
World:
  Dimensionality: 2                  # int, optional — 2 or 3 (most games are 2)
  BackgroundColor: "#000000"         # colour, optional — see the !colour tag for accepted forms
```

`BackgroundColor` accepts the same value forms as a `!colour` (hex string, named colour, or RGBA
mapping) — the tag itself is not required here because the field is already colour-typed.

### `Physics` — `PhysicsDto`

```yaml
Physics:
  Gravity: !vec { X: 0, Y: 0 }       # !vec, optional — { X:0, Y:0 } disables gravity (top-down/arcade)
```

### `Assets` — list of `AssetDto`

A YAML **sequence**, not a map. Each entry declares one project asset; reference it elsewhere by `Id`
with the `!asset` tag (for `sprite.Sprite`, `voxel mesh.Mesh`, `audio source.Clip`, …).

```yaml
Assets:
  - Id: voxel mesh asset             # string — the id used by !asset
    Type: mesh                       # string — asset kind (mesh | sprite | audio | …)
    Source: resources                # string — where to load from (e.g. resources)
    Path: Voxels/voxel               # string — load path within that source
```

### `Constants` — map `id → value`

Compile-time named values. The **YAML mapping key is the identifier** (spaces allowed). Read every
constant with `!var <id>` (there is no separate `!const` tag — `!var` resolves Variables first, then
falls back to Constants). Values may be any scalar, a tag (`!vec`, `!colour`, `!record`, …), or a
nested `!expr`.

```yaml
Constants:
  paddle up speed: 5
  upper wall position: !vec { X: 0, Y: 3 }
  player colour: !colour { R: 0.3, G: 0.85, B: 0.4, A: 1 }
  coin item: !record { Type: Item, kind: coin }
```

### `Variables` — map `id → initial value`

Runtime-mutable named values. Same `id → value` shape as `Constants`; the value is the **initial**
state. Behaviours (`* variable setter`, `* list *`) mutate them; `!var <id>` reads them. Initial
values may be a literal, another `!var`, an `!expr`, a `!record`, or (inside a template) a
`!parameter`.

```yaml
Variables:
  left score: !var initial score     # seeded from a constant
  ball velocity: !vec { X: 3, Y: 3 }
  is dead: false
  inventory: !record [ { Type: Item, kind: potion, count: 3 } ]   # a record-list variable
```

### `Records` — map `schemaName → (fieldName → RecordFieldDto)`

Declares named **record schemas** — typed field bags. Each schema is just a map of field name to a
`{ Type, Default }` definition; there is no schema-level metadata. Instantiate a schema with the
`!record` tag (see tag table). Unset fields fall back to `Default`.

```yaml
Records:
  Item:                              # schema name (referenced by !record { Type: Item, … })
    kind:       { Type: string }                 # required field, no default
    count:      { Type: int,   Default: 1 }      # Type is required; Default optional
    durability: { Type: float, Default: 1.0 }
```

`RecordFieldDto` fields: `Type` (string — `int` | `float` | `bool` | `string`), `Default` (optional
scalar).

### `Expressions` — map `id → ExpressionDto`

Named, reusable code snippets called via `!expr { Do: <id>, With: [...] }`. The `Expression:` body is
**code, not YAML** — a strict procedural C# subset; author it with the `unity-expression-compiler`
skill. Prefer the global helpers in [`Libraries.md`](Libraries.md) over registering statics.

```yaml
Expressions:
  paddle bounce:
    ArgumentTypes: [ int, int ]              # optional — types of the declared params, positional
    ArgumentNames: [ a, b ]                  # optional — must match ArgumentTypes length
    ReturnType:    int                       # required — int | float | bool | string | vector | colour | record | record list | …
    RegisterTypes: [ UnityEngine.Vector3 ]   # optional — lets the body construct these by bare name
    RegisterTypeStatics: [ UnityEngine.Random ] # optional — lets the body call these statics unprefixed
    Expression:    "a + b;"                  # the method body (code)
    CallableAs:    bounce                    # optional — an alias the !expr Do: can also dispatch on
```

`ExpressionDto` fields: `ArgumentTypes[]`, `ArgumentNames[]`, `ReturnType`, `RegisterTypes[]`,
`RegisterTypeStatics[]`, `Expression`, `CallableAs`. You often don't need a named entry at all — a
one-off body can be written inline at the `!expr` call site.

### `Templates` — map `id → EntityDto`

Reusable entity blueprints. Each value has the **same shape as an entity** (see
[Entity structure](#entity-structure)) and may contain `!parameter` slots filled at instantiation.
See [Templates](#templates-1).

### `Entities` — map `id → EntityDto`

The scene's entities. The **YAML mapping key is the entity id**. Each value is an `EntityDto` — see
[Entity structure](#entity-structure).

### `Controls` — `ControlsDto`

The semantic input layer: abstract **actions** plus **per-platform bindings** that feed them. Listen
to an action with the `input action` behaviour (whose `Action` property names an action key here).

```yaml
Controls:
  Actions:                                   # map: action id → ActionDto
    move: { Type: value,  ValueType: vector2 }  # value action — emits axis/x/y every frame
    jump: { Type: button, Phase: down }         # button action — Phase: down | up | hold
  Bindings:                                  # map: platform → (action id → list of BindingDto)
    desktop:
      jump: [ "<Keyboard>/space" ]              # scalar binding = a single control path
      move:                                      # mapping with Composite = a composite binding
        - Composite: 2DVector
          Up:    "<Keyboard>/w"
          Down:  "<Keyboard>/s"
          Left:  "<Keyboard>/a"
          Right: "<Keyboard>/d"
    gamepad:
      jump: [ "<Gamepad>/buttonSouth" ]
      move: [ "<Gamepad>/leftStick" ]
```

- `ActionDto`: `Type` (`button` | `value`), `Phase` (`hold` | `down` | `up` — button only),
  `ValueType` (e.g. `vector2` — value only).
- `BindingDto`: either a **scalar** path string, or a **mapping** with `Composite` + named `Parts`
  (e.g. `Up`/`Down`/`Left`/`Right` for `2DVector`). Read by `BindingTypeConverter`.

### `Localisation` — `LocalisationDto`

Per-locale string table. **All user-facing strings should go through this** via `!text`, never inline
literals.

```yaml
Localisation:
  DefaultLocale: en                  # string — fallback locale
  Locales:                           # map: locale → (key → template string)
    en:
      hud.score: "Score: {0}"        # {0},{1},… are string.Format placeholders; escape literals as {{ }}
      menu.start: "Press Space to start"
```

A missing key renders as a visible `#key#` marker rather than crashing.

### `Navigation` — `NavigationDto`

Configures the walkability grid used by pathfinding/perception behaviours.

```yaml
Navigation:
  CellSize: 0.5                                  # float — grid cell edge length
  Bounds:                                        # BoundsDto — world-space extent the grid spans
    Min: !vec { X: -9, Y: -7 }
    Max: !vec { X:  9, Y:  7 }
  ObstacleTag: wall                              # string — entities with this tag block cells
  Plane: xy                                      # string, optional — "xy" (default) or "xz" (ground plane)
  Diagonal: true                                 # bool, optional — allow diagonal steps (default true)
  DefaultAgentRadius: 0                          # float, optional — DEFAULT clearance for agents that don't set their own; inflates obstacles by this many world units (default 0)
```

`BoundsDto`: `Min` / `Max` as `!vec` corners. `NavigationDto`: `CellSize`, `Bounds`, `ObstacleTag`,
`Plane`, `Diagonal`, `DefaultAgentRadius`. The `navigate` and `grid mover` behaviours each take their own
optional `AgentRadius` that overrides this default (omit it to inherit), so differently-sized agents can
take different paths.

---

## Entity structure — `EntityDto`

An entity (and a template, and a child) all share this shape. Every field is optional.

```yaml
entity id:                           # the YAML key is the entity id
  Tags: [ ball, dynamic ]            # list<string> — used by tagged listeners and TagsToDetect
  Position: !vec { X: 0, Y: 0 }      # value (object) — !vec | !var | !expr resolving to a Vector3
  Rotation: !vec { X: 0, Y: 0, Z: 0 }# value (object) — Euler angles as a Vector3
  Template:                          # TemplateRefDto — optional; layers with inline Behaviours
    Id: paddle_template              # string — a key under top-level Templates:
    Parameters:                      # map: slot name → value, fills the template's !parameter slots
      up_action: move-left-up
  Variables:                         # map id → initial value — PER-ENTITY (per-instance) variables
    health: 3
  Behaviours:                        # map: behaviour id → BehaviourDto
    behaviour id:
      Type: velocity                 # string — MUST be a Type from Behaviours.md
      Properties: { Velocity: !vec { X: 0, Y: 5 } }   # map — keys/types from Behaviours.md
      Listeners: [ … ]               # list<ListenerDto> — only meaningful on trigger behaviours
      Tags: [ scoreable ]            # list<string> — used by behaviour-tag listeners
  Children:                          # map: child id → EntityDto (same shape, recursive)
    child id:
      Behaviours: { … }
```

`EntityDto` fields: `Id`, `Template`, `Tags`, `Position`, `Rotation`, `Behaviours`, `Variables`,
`Children`. Notes:

- There is **no entity-level `Scale`** field — scale is *read* off another entity via
  `!entity { Property: Scale }`, not set here.
- `Position` / `Rotation` are untyped (`object`), so they accept any value form that resolves to a
  `Vector3` (`!vec`, `!var`, `!expr`).
- `Children` ids are **path-joined** onto the parent (`ui` → child `hud` → `ui/hud`). Target a nested
  entity by its full path in `EntityId:`.

### Behaviour — `BehaviourDto`

```yaml
behaviour id:                        # the YAML key is the behaviour id (unique within the entity)
  Type: condition gate               # string, required — verbatim from Behaviours.md
  Properties: { Condition: !expr { … } }  # map<string, value> — names + types from Behaviours.md
  Listeners: [ … ]                   # list<ListenerDto> — see Listeners
  Tags: [ scoreable ]                # list<string>
```

`BehaviourDto` fields: `Type`, `Tags`, `Properties`, `Listeners`. Property **values** may be any
scalar, tag, or expression the loader supports.

---

## Listeners — `ListenerDto`

Triggers (behaviours whose `Type` ends in `trigger`, plus `input action`, `ui button`, `ui slider`,
…) **fire** events; their `Listeners:` list names what **executes** in response. There are four
forms — pick exactly one shape per list entry:

```yaml
Listeners:
  # 1. Direct — a named behaviour on a named entity
  - EntityId: ball spawner            # entity id (or !parameter self_id inside a template)
    BehaviourId: spawn ball           # a behaviour id on that entity
    Outputs:                          # optional — bind this trigger's named outputs to local names
      contact_point: hit_point        # <output name from Behaviours.md>: <local name read via !output>

  # 2. Entity-tag — that behaviour on EVERY entity carrying the tag (resolved at notify time)
  - EntityTag: !var target tag        # string value source (literal | !var | !expr)
    BehaviourId: self destruct

  # 3. Behaviour-tag — EVERY behaviour anywhere carrying the tag (no entity named)
  - BehaviourTag: !var scoreable tag  # string value source

  # 4. Game over — ends + unloads the whole game
  - !gameover
```

`ListenerDto` fields: `EntityId` (value), `BehaviourId` (string), `EntityTag` (value),
`BehaviourTag` (value), `Outputs` (map `outputName → localName`). The `!gameover` tag deserialises to
a distinct `GameOverListenerDto` marker.

- **At least one reachable `!gameover` listener is required** — the build fails without one. For an
  end-on-condition game, poll the condition: an `every frame trigger` → `condition gate` whose
  `Listeners` contains `- !gameover`.
- Only triggers with declared **Outputs** in [`Behaviours.md`](Behaviours.md) produce values to bind.
  Bind them in `Outputs:`, then read the bound local name with `!output <localName>` downstream
  (usually inside an `!expr` `With:` list).

---

## Templates

A template is an `EntityDto` declared under top-level `Templates:`. An entity instantiates one via a
`Template:` ref and inherits its `Tags`, `Variables`, and `Behaviours`, with `!parameter` slots filled
from `Parameters:`.

```yaml
Templates:
  paddle_template:
    Tags: [ paddle ]
    Variables:                        # per-instance — each spawned copy owns its own
      health: !parameter initial_health
    Behaviours:
      up action:
        Type: input action
        Properties: { Action: !parameter up_action }
        Listeners:
          - EntityId: !parameter self_id   # refers to THIS instance, not the template literal
            BehaviourId: move up

Entities:
  left paddle:
    Template:
      Id: paddle_template
      Parameters:
        up_action: move-left-up
        initial_health: 3
    Tags: [ left paddle ]             # extra tags layer on top of the template's
```

- **`self_id`** is the implicit parameter every template has — use `EntityId: !parameter self_id`
  whenever a template behaviour references its own entity.
- **Spawners** (`Type: spawner`) instantiate a template at runtime, passing `Parameters:` (including
  ones that seed per-instance `Variables`).
- An entity may use a `Template:` **and** declare extra `Behaviours:` — they layer together.

---

## Value types and scalar inference

Wherever a slot is untyped (most `Properties:`, `Constants`, `Variables` values), the parser infers
the type from the YAML. Inference rules (`ObjectNodeDeserializer` / `ParseScalar`):

- **Plain (unquoted) scalars** are type-inferred with **invariant culture**, in order: `int` → `float`
  → `bool` → otherwise `string`. So bare `1` is an `int`, `1.5` a `float`, `true` a `bool`.
- **Quoted or block scalars are always strings**, regardless of contents — `"1"` is the string `"1"`,
  not an int. Quote any string that looks numeric/boolean, and quote hex colours (`"#FF8800"`) so YAML
  doesn't treat `#` as a comment.
- **Force a scalar's type** with an explicit tag: `!int`, `!float`, `!bool`, `!string`.
- **Mappings/sequences** become nested structures; tagged ones (`!vec`, `!colour`, `!expr`, `!record`)
  dispatch to their DTO.

### Typed lists

A YAML sequence can carry an element tag to type its items: `!int [ … ]`, `!float [ … ]`,
`!bool [ … ]`, `!string [ … ]`, `!vec [ … ]`, `!colour [ … ]`, `!record [ … ]`. An untagged sequence
is an untyped `list<object>`. Use the tagged form for a homogeneous list (e.g. a `!record [...]`
record-list variable, or `!vec []` for an empty vector list).

```yaml
TagsToDetect: [ left paddle, right paddle ]    # plain sequence of strings
inventory: !record [ { Type: Item, kind: potion, count: 3 } ]   # typed record list
```

---

## Custom YAML tags

Tags carry a leading `!` and tell the loader how to interpret a value. The full registry (from
`GameFileParser.cs`):

| Tag | DTO | Accepted form(s) | Meaning |
|---|---|---|---|
| `!vec` | `VecDto` | `!vec { X: 0, Y: 0, Z: 0 }` (Z optional, defaults 0) | A `Vector3` literal. There is no `Vector2`; 2D quantities are `Vector3` with Z=0. `X`/`Y`/`Z` may themselves be expressions. |
| `!colour` | `ColourDto` | `!colour red` · `!colour "#RRGGBB"` (also `#RGB`, `#RRGGBBAA`) · `!colour { R:1, G:0, B:0, A:1 }` (A optional, defaults 1) | A colour: named, hex string (quote it), or RGBA mapping. |
| `!var` | `VarRefDto` | `!var some id` (scalar) | Reads a value by id: per-entity `Variables` → global `Variables` → `Constants`. The only read tag — there is no `!const`. |
| `!parameter` | `ParamRefDto` | `!parameter slot` (scalar) | Inside a template, a parameter slot filled at instantiation. `!parameter self_id` = this entity's own id. |
| `!expr` | `ExprRefDto` | `!expr { Do, With, … }` (mapping) | Evaluates code. See [`!expr`](#expr-detail). |
| `!asset` | `AssetRefDto` | `!asset asset_id` (scalar **only** — mapping form fails) | References a top-level `Assets:` entry by id, for asset-typed properties. |
| `!output` | `OutputRefDto` | `!output local_name` (scalar) | Reads a trigger output previously bound by a listener's `Outputs:` map. |
| `!entity` | `EntityRefDto` | `!entity { Id: other, Property: Position }` (mapping) | A **live** per-frame read of another entity's transform `Property` (`Position` \| `Rotation` \| `Scale`) as a `Vector3`. |
| `!rigidbody` | `RigidbodyRefDto` | `!rigidbody { Id: car, Property: Velocity }` (mapping) | A live read of an entity's `Rigidbody` `Property` (`Velocity` \| `AngularVelocity` \| `Position`). |
| `!query` | `EntityQueryRefDto` | `!query { Kind, EntityTag, Origin, MaxRange }` (mapping) | A one-off spatial perception query against the live entity index. `Kind` selects the verb (e.g. `NearestId`, `NearestPosition`). |
| `!clock` | `ClockRefDto` | `!clock deltaTime` (scalar) | Reads a game-clock property (`deltaTime` \| `time` \| `frameCount` \| `unscaledDeltaTime`) as a number. Respects pause/slow-mo. |
| `!text` | `TextRefDto` | `!text menu.start` (scalar) · `!text { Key: hud.score, Arguments: [ … ] }` (mapping) | Resolves a `Localisation:` key. **Mapping form uses `Arguments:`, not `With:`.** |
| `!record` | `RecordLiteralDto` | `!record { Type: SchemaName, field: value, … }` · `!record [ { … }, … ]` | Instantiates a `Records:` schema (defaults fill unset fields), or a list thereof. The reserved `Type` key names the schema; all other keys are field values. |
| `!gameover` | `GameOverListenerDto` | `- !gameover` (a listener) | Ends + unloads the game. At least one reachable instance is required. |

Lists of values accept either flow (`[ a, b ]`) or block (`- a`) syntax.

<a id="expr-detail"></a>

### `!expr` — the expression call site

```yaml
!expr
  Do:   <name-or-inline-body>        # required
  With: [ <arg>, <arg>, … ]          # optional — the operands
  # inline-only hints (ignored on a named Do; the named expression declares its own):
  ReturnType:  int                   # required when the use-site type can't be inferred (object slots)
  ArgumentTypes: [ int, int ]        # explicit per-operand types (positional to With)
  RegisterTypes: [ UnityEngine.Vector3 ]
  RegisterTypeStatics: [ UnityEngine.Mathf ]
```

`ExprRefDto` fields: `Do`, `With`, `ReturnType`, `ArgumentTypes`, `RegisterTypes`,
`RegisterTypeStatics`. `Do` dispatches **by name first**:

- **Named call** — if `Do` matches an `Expressions:` id (or a `CallableAs` alias), it calls that
  expression; `With` supplies its declared arguments in order.
- **Inline body** — otherwise `Do` is compiled as an anonymous C# body, and `With` binds
  **positionally** to `arg0`, `arg1`, `arg2`, … inside it. A zero-arg body needs no `With`.

`Do`/`With` is the only accepted form. The legacy `ExpressionId` / `Arguments` keys are gone (the only
surviving `Arguments:` is inside `!text`, which is unrelated). Inline bodies are still **code** —
author them with the `unity-expression-compiler` skill and prefer [`Libraries.md`](Libraries.md)
helpers over `RegisterTypeStatics`.

---

## Validation

Two headless validators boot Unity in batch mode and check a descriptor end-to-end. Run them after
writing or editing a descriptor:

| Script | Checks |
|---|---|
| `Tools/validate-yaml.sh <file>` | YAML well-formedness + duplicate keys (structure only). |
| `Tools/check-expression.sh <file>` | Every embedded `!expr` / `Expressions:` body compiles. |
| `Tools/validate-game.sh <file>` | The descriptor builds through **structure → deserialise → parse → resolve → instantiate**, reporting the exact failing stage. The authoritative "does it build" check. |

`validate-game.sh` with no argument sweeps every file in `Assets/ExampleGameDescriptors/`.
