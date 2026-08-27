# Models

How to compose an entity's visual out of primitive parts with the `model` behaviour, and how to make
the result look like the thing it is meant to be. The mechanics half explains what `model` actually
does with `Size`, `Anchor`, `Rotation` and `Mirror`; the composition half is craft — proportion,
silhouette, palette, grounding — and ends in ten worked recipes that can be copied and adapted.

Unlike `Behaviours.md` and `Libraries.md`, this document is **hand-written and never regenerated**,
so it can drift. **When this document and [`Behaviours.md`](Behaviours.md) disagree, `Behaviours.md`
wins** — it is generated from the behaviour's own XML doc comment, and anything restated here is a
copy that may have rotted. Property names and types are its job; numbers, traps and taste are this
document's.

| Doc | Owns | Use it for |
|---|---|---|
| [`GameDescriptorSchema.md`](GameDescriptorSchema.md) | The document shape: sections, nesting, value types, tags | "What keys exist and how do they nest?" |
| [`Behaviours.md`](Behaviours.md) | The behaviour catalogue: every `Type:`, its `Properties:`, its trigger `Outputs` | "What behaviour types and property names are legal?" |
| [`Libraries.md`](Libraries.md) | Global expression helpers callable by bare name from any `!expr` | "What functions can an expression body call?" |
| **`Models.md`** (this file) | Composing a multi-part visual out of primitives, and making it look right | "How do I build a prop that reads as a car?" |

Every worked recipe below is instantiated verbatim in
[`PrimitiveModels.yaml`](../ExampleGameDescriptors/PrimitiveModels.yaml), which is the only proof
that the numbers here land where they claim to. If you change a recipe, change it there too and
re-run `unity command validate_game --targets Assets/ExampleGameDescriptors/PrimitiveModels.yaml`.

---

## When to reach for `model`

| The entity needs | Use | Why |
|---|---|---|
| Exactly one shape | `primitive` | One mesh, one behaviour, no `Parts:` ceremony. |
| Two or more shapes | `model` | Each part gets its own offset, rotation, size, anchor and colour. |
| Two or more shapes | ~~two `primitive` behaviours~~ | **Never.** Every `primitive` on an entity renders at the entity origin, axis-aligned, on top of the others. Stacking them produces one visible shape and some hidden ones. |

There is no single-shape shorthand: `model` without a `Parts:` list is a parse error that tells you
to use `primitive` instead.

A `model` is **one entity**. Split into child entities only when pieces must move independently,
carry their own behaviours, or be addressed by id.

---

## Mechanics

### Shapes and true world size

A part's `Size` is its **true world bounding box in metres**, not a scale factor: Unity's built-in
primitives are not all unit-sized, so `model` divides the native dimensions out before assigning
`localScale`.

| `Shape` | Unity's native mesh | `localScale` `model` computes | Worked example |
|---|---|---|---|
| `cube` | 1 x 1 x 1 | `Size` verbatim | `Size 2, 0.5, 3` -> `localScale 2, 0.5, 3` -> 2 x 0.5 x 3 m |
| `sphere` | 1 diameter | `Size` verbatim | `Size 2, 1, 2` -> an ellipsoid 2 m wide and 1 m tall |
| `cylinder` | **2 tall**, 1 diameter | `(x, y / 2, z)` | `Size 1, 3, 1` -> `localScale 1, 1.5, 1` -> genuinely 3 m tall |
| `capsule` | **2 tall**, 1 diameter | `(x, y / 2, z)` | `Size 0.6, 1.2, 0.6` -> `localScale 0.6, 0.6, 0.6` -> 1.2 m tall |
| `plane` | **10 x 10** in XZ, no thickness | `(x / 10, y, z / 10)` | `Size 4, 1, 4` -> `localScale 0.4, 1, 0.4` -> genuinely 4 x 4 m |
| `quad` | 1 x 1 in XY, no thickness | `Size` verbatim | `Size 2, 1, 1` -> 2 m wide, 1 m tall |

Three consequences worth internalising:

- **A cylinder's `Size.Y` is its height, full stop.** Under `primitive` the same number gives twice
  that; if a column looks double height, this is why.
- **`plane` has no thickness and `quad` has no depth.** A plane's `Size.Y` and a quad's `Size.Z` are
  accepted and then do nothing. For a thin slab use a `cube` with one small `Size` component.
- **A flat part is single-sided** — URP's Lit shader does not render back faces. An unrotated `plane`
  faces **+Y**; an unrotated `quad` faces **-Z**, which is toward a default-facing camera. Turn
  either around and it disappears; check this first when a part has vanished but its numbers look
  right.

> **Capsule caps are hemispheres only when `Size.Y == 2 * Size.X == 2 * Size.Z`.** Unity's capsule is
> a 1-tall cylinder with two radius-0.5 hemispheres, so under a non-uniform `Size` the caps become
> half-ellipsoids: `Size 0.20, 0.85, 0.20` gives caps 0.2125 long against a 0.10 radius, about 2.1x
> stretched. Often right for a limb, wrong for a pill-shaped button. Below `Y = 2X` they flatten.

### The `primitive` / `model` `Size` divergence

> **`primitive.Size` is a raw `localScale`. `model`'s part `Size` is a true world bounding box.**
> They are different quantities with the same name, and only `cube`, `sphere` and `quad` — the
> already-unit-sized meshes — make them agree.
>
> ```yaml
> Type: primitive                       # a cylinder 6 m tall — localScale.Y 3 on a 2-tall mesh
> Properties: { Shape: cylinder, Size: !vec { X: 1, Y: 3, Z: 1 } }
> ```
> ```yaml
> Type: model                           # a cylinder 3 m tall — Size.Y 3 means 3 metres
> Properties:
>   Parts:
>     - Shape: cylinder
>       Size: !vec { X: 1, Y: 3, Z: 1 }
> ```
>
> Porting from `primitive` to `model`: **double every cylinder's and capsule's `Size.Y`, and divide a
> plane's `Size.X`/`Size.Z` by ten.**

### Anchors

By default every primitive mesh is pivoted at its **centre**, so a part at `Position 0, 0, 0` is
half-buried in the ground. `Anchor` names a different point on the part — the point that lands on
`Position`.

| Axis | Tokens | `Anchor` component |
|---|---|---|
| X | `left` / `right` | -1 / +1 |
| Y | `bottom` / `top` | -1 / +1 |
| Z | `back` / `front` | -1 / +1 (+Z is forward) |

Tokens are hyphen-separated and order-agnostic; an omitted axis stays centred, so `Anchor: bottom`
centres X and Z and only lifts Y. Naming one axis twice (`left-right`) is a parse error, not a
silent last-wins. The offset applied is:

```
offset = -Anchor * Size / 2          # component-wise, using the un-normalised Size
```

so `Anchor: bottom` on a part of `Size.Y 2.2` lifts its centre by 1.1 and the part occupies
`Y 0 .. 2.2`. `Anchor: right` on a part of `Size.X 2.7` shifts its centre 1.35 to the **left**, so
that its right-hand face lands on `Position`.

> **The offset uses `Size`, not `localScale`.** A cylinder of `Size.Y 3` anchored `bottom` rises by
> 1.5 — half its true height — even though its `localScale.Y` is 1.5. You never have to do the
> halving twice.

> **`Anchor` cannot be templated.** It is baked at transform time into a plain direction vector, so
> it rejects `!var`/`!expr`/`!parameter` outright and template parameters do not substitute into it.
> A prop template can vary `Size`, `Position`, `Rotation` and `Colour`; two anchors means two
> templates.

### Rotation pivots about the anchor

`Rotation` is applied to the part *and* to the anchor offset, so the part turns **about its anchored
point** rather than about its centre. That makes `Anchor` + `Rotation` a hinge, and it is the only
way to place an angled part without solving for a rotated centre by hand.

The `streetlight` arm is the worked case. A cylinder of `Size 0.13, 1.5, 0.13`, `Anchor: bottom`, at
`Position (0, 4.1, 0)`, rotated `Z -70`:

```
the anchored foot stays at        (0, 4.10)
the arm runs 1.5 m along          (sin 70, cos 70) = (0.940, 0.342)
so its tip lands at               (1.5 * sin 70, 4.10 + 1.5 * cos 70) = (1.410, 4.613)
```

Change the angle or the length and the foot does not move — only the tip does, so anything mounted
there must be re-derived. That is the price of the technique, and it is still far less brittle than
computing a rotated centre by hand on every edit.

### Mirror

`Mirror` emits reflected duplicates of a part **in addition to** the original, so a symmetric shape
is authored once. Read this table off `ModelGeometry` in
[`Model.cs`](../Behaviours/Visual/Model.cs):

| `Mirror` | Copies | Position x | Rotation x | Anchor x | Name suffix |
|---|---|---|---|---|---|
| `none` (default) | 1 | — | — | — | — |
| `x` | 2 | `(-1, 1, 1)` | `(1, -1, -1)` | `(-1, 1, 1)` | `" (mirrored x)"` |
| `z` | 2 | `(1, 1, -1)` | `(-1, -1, 1)` | `(1, 1, -1)` | `" (mirrored z)"` |
| `xz` | 4 | plus `(-1, 1, -1)` | plus `(-1, 1, -1)` | plus `(-1, 1, -1)` | `" (mirrored xz)"` |

Four traps:

> - **`Size` is never negated.** Only `Position`, `Rotation` and `Anchor` flip — `Anchor: right`
>   becomes `left`, `Rotation.Z 6` becomes `-6`.
> - **A mirrored part needs a non-zero offset on the mirrored axis**, from `Position` *or* from
>   `Anchor`. Without one the twins are coincident and z-fight. The `house` roof is the legitimate
>   exception: its `Position.X` is 0, but `Anchor: right` flips to `left`, so the two slabs sweep
>   away from the same ridge in opposite directions.
> - **`xz` is not a pyramid.** Mirror reflects; it never rotates. Four-sided radial detail — wheel
>   spokes, rocket fins — is separate authored parts.
> - **There is no Y mirror.** Barrel hoops and wheel rims are two authored parts. `Mirror: y` does
>   not exist.

### Colour

Colour resolves in three rungs, and the first one that is set wins:

1. the part's own `Colour`,
2. the model-wide `Colour` on the behaviour,
3. neither — the part keeps the shared `Materials/Primitive` material's own colour.

Put the **dominant** colour on the behaviour and override only the parts that differ — a ten-part
prop in two colours is two `Colour:` lines, not ten. Both rungs accept `!var`/`!expr`, so a
model-wide colour can be driven live (team colour, damage state) while parts stay fixed.

`Colour` must carry the `!colour` tag, and a hex string must be quoted or YAML reads the `#` as a
comment: `Colour: !colour "#a64535"`.

### What `model` is not

- **Parts carry no colliders.** `model` strips the one `GameObject.CreatePrimitive` adds — a model
  is a visual, and collision is declared separately. See [Collision](#collision) below.
- **It is not a listener target.** `model` is continuous/passive. To change a prop at runtime, bind
  a part's `Position`/`Rotation`/`Size`/`Colour` to a `!var` or `!expr` and write to that.
- **It is matte.** No emissive channel, no light. A lens, a glowing eye or a screen is a matte part
  **plus** a child entity carrying a `light` — see the `streetlight` recipe.
- **`Shape`, `Name` and `Mirror` are read once** when the meshes are built; only `Position`,
  `Rotation`, `Size` and `Colour` are live. A `!var` shape will not swap the mesh at runtime.

<a id="collision"></a>

### Collision

Parts are visual only, so collision is a second behaviour on the same entity. **Never hand-write a
collider's `Size` or `Radius` to match a model** — both options below measure the meshes themselves:

| Want | Use | Gives you |
|---|---|---|
| One collider around the whole prop | `box collider` or `sphere collider` with `Fit: bounds` | One collider fitted to the visual's combined bounds. Fits **once**, from the initial values. |
| A collider per part | `part colliders` | A compound collider, each part's shape matched to the primitive that built it. Re-fits for free when a part's `Size` is live-bound. |

```yaml
Behaviours:
  body:
    Type: model
    Properties:
      Parts: [ ... ]
  hull:
    Type: box collider          # must come AFTER the visual it measures
    Properties: { Fit: bounds }
```

> **The collider behaviour must be listed *after* the visual behaviour**, or initialisation throws.
> Behaviours initialise in declaration order, and a collider that fits to meshes cannot measure
> meshes that do not exist yet.

`part colliders` maps each part's shape to a collider: `cube`, `quad` and `plane` get a
BoxCollider, `sphere` gets a SphereCollider, and `capsule` **and `cylinder`** get a CapsuleCollider —
so a cylinder collides with rounded ends. That is usually right for a leg or a barrel, and
occasionally wrong for a wheel or a pillar; use `Fit: bounds` there instead. Renderers belonging to
child entities are excluded — a child entity declares its own collision.

Reach for `Fit: bounds` by default and `part colliders` when one box around the whole thing is too
coarse to play against — a `humanoid`, a `streetlight` whose arm should not be a solid slab, a
`tree` whose canopy overhangs walkable ground. Hand-sizing (`Size`/`Radius` with no `Fit`) remains
for collision that should deliberately *differ* from the visual — a wider pickup radius, a
forgiving hitbox. A `sphere collider` with `Fit: bounds` and `IsTrigger: true` is the standard
pickup: see `coin`.

---

## Composition principles

Six rules that decide whether a pile of primitives reads as a car or as a pile of primitives.

**1. Build at real-world scale — 1 unit = 1 metre.** Nothing rescues a scene where this is wrong.
Reach for these when you have no better reference:

| Prop | Height | Prop | Footprint |
|---|---|---|---|
| person | 1.7 – 1.9 m | car | 1.8 x 4.2 m |
| door | 2.0 m | single-storey house | 4 x 3 m |
| crate, barrel | 0.8 – 1.1 m | tree canopy | 2 – 4 m across |
| streetlight | 4 – 6 m | coin, pickup | 0.3 – 0.6 m |

**2. Silhouette before detail.** A prop is recognised by its outline at twenty metres, never by its
trim. Get the two to four big masses right first — chassis and cabin, walls and roof, trunk and
canopy — then add detail. If the silhouette does not read, more parts will not save it.

**3. Ground everything.** `Anchor: bottom` with `Position.Y 0` is the default posture: the part sits
*on* the origin rather than half-buried in it. Departing from it should be a decision you can name —
pickups float (`coin`), rocks sink (`rock`). An accidentally half-buried prop is the commonest way a
generated scene looks broken.

**4. Proportion beats part count.** Three to eight authored parts covers almost everything here.
Detail smaller than about 2 % of the prop's largest dimension is invisible in play and costs a draw
call; a 0.02 m bolt on a 4 m car is a rounding error, not a detail.

**5. Keep the palette tight.** Four colours maximum, one dominant and sitting on the model-wide
`Colour`. Separate parts by **value** (light against dark) rather than hue: six saturated hues reads
as a toy, and reads worse once real lighting lands. Muted, slightly desaturated colours survive
being lit; pure `red` and pure `blue` do not.

**6. Author symmetry once.** If a shape has a mirror plane, use `Mirror` — wheels, headlights,
windows, limbs, roof slopes. Typing the twin by hand is how the halves end up 0.01 apart.

### Five techniques worth naming

**The oversized slab.** A part 2 – 5 % larger than the box it passes through reads as trim, glazing
or a rim on *every* side it emerges from, for the cost of one part. The `car`'s glass band is one
slab 0.03 wider than the cabin and reads as glazing on all four sides. Radially it does the same
job: the `barrel`'s hoops, the `coin`'s rim.

**The proud detail.** Sit a small part's **centre on the host's surface** so half protrudes. One
number, robust to the host changing size, and it never gaps — whereas solving for tangency is exact
and wrong the moment anything moves. The `turret`'s eye, the `car`'s headlights, the `humanoid`'s
eyes.

**The overlap rule.** Adjacent masses overlap by 10 – 20 % of the smaller one. **Never let two parts
merely touch.** Coplanar faces z-fight, and a butt joint plus shadow acne is a visible seam even
when they do not. Every stack below overlaps: the `tree`'s trunk 0.3 into the canopy, the
`humanoid`'s head 0.06 into the torso, the `crate`'s lid rim 0.05 into the body.

**Anchor as a hinge.** `Anchor` plus `Rotation` turns a part about its anchored end — the only
practical way to build an angled part (roof slope, lamp arm, splayed limb), because the alternative
is recomputing a rotated centre on every edit. See `streetlight` and `house`.

**The tilted fill.** An axis-aligned cube cannot be a triangle — but a slab *rotated to the
diagonal's own angle* hugs it exactly. Fill the diagonal edge with a rotated slab, then cover what
its underside misses with one upright part; overlaps disappear inside the mass. The `house`'s gable
end is the worked case: one slab at the roof's own `-31`, mirrored `xz`, plus one centre board,
closes the whole triangle. Reach for it whenever a shape wants a slope — a windscreen, a ramp, a
gable — before approximating with stacked steps, which close at best 80 – 90 % and read as stairs
up close.

---

## Recipes

Ten props authored to the rules above: 1 unit = 1 m, +Z forward, `Anchor: bottom` at `Y 0` unless
there is a reason not to, `Mirror` for every symmetric pair, four colours at most. **Start from the
nearest recipe and adapt it** — these proportions are checked against a render, which is not true of
anything composed from scratch. Each block drops straight into an entity's `Behaviours:` map.

| Recipe | Authored | Rendered | Bounding box (X x Y x Z) | Teaches |
|---|---|---|---|---|
| [`tree`](#tree) | 4 | 4 | 2.60 x 3.90 x 2.60 | the cylinder halving; the overlap rule; a tiered canopy |
| [`house`](#house) | 7 | 13 | 4.72 x 3.72 x 3.46 | anchor as a hinge; `Mirror: x` at X 0; the tilted fill |
| [`car`](#car) | 7 | 12 | 2.04 x 1.47 x 4.20 | `Mirror: xz`; the oversized slab; a tilted windscreen |
| [`crate`](#crate) | 3 | 6 | 1.06 x 1.05 x 1.06 | the cleanest `Mirror: xz` |
| [`barrel`](#barrel) | 4 | 4 | 0.84 x 1.08 x 0.84 | there is no Y mirror; halving four times |
| [`rock`](#rock) | 4 | 4 | 2.34 x 1.29 x 1.72 | the exception to `Anchor: bottom`; tilted cubes for stone |
| [`streetlight`](#streetlight) | 5 | 5 | 1.84 x 4.69 x 0.36 | anchor as a hinge; a matte lens needs a real `light` |
| [`coin`](#coin) | 3 | 3 | 0.62 x 0.62 x 0.10 | `Rotation X 90`; the deliberate float |
| [`turret`](#turret) | 6 | 8 | 1.20 x 1.10 x 1.90 | `Rotation X 90` + `Anchor: bottom` grows forward; mass order |
| [`humanoid`](#humanoid) | 7 | 11 | 1.06 x 1.70 x 0.51 | capsule caps; splayed limbs; endpoint-centred hands |

> **Every `!vec` needs `X` **and** `Y`.** Only `Z` defaults to 0. `!vec { Y: 2.2 }` is a parse
> error — `Invalid component value: null` — not a shorthand. Write all three components; the
> recipes below do.

### `tree`

**4 authored parts, 4 rendered · 2.60 x 3.90 x 2.60 m (Y +0.00 .. +3.90)** — a fat trunk under a three-tier canopy stack.

```yaml
body:
  Type: model
  Properties:
    Colour: !colour "#4e8f3d"
    Parts:
      - Name: trunk
        Shape: cylinder
        Size: !vec { X: 0.5, Y: 1, Z: 0.5 }
        Anchor: bottom
        Colour: !colour "#7a4f2a"
      - Name: canopy low
        Shape: sphere
        Size: !vec { X: 2.6, Y: 1.6, Z: 2.6 }
        Position: !vec { X: 0, Y: 0.7, Z: 0 }
        Anchor: bottom
      - Name: canopy mid
        Shape: sphere
        Size: !vec { X: 2, Y: 1.3, Z: 2 }
        Position: !vec { X: 0, Y: 1.9, Z: 0 }
        Anchor: bottom
        Colour: !colour "#5da84b"
      - Name: canopy top
        Shape: sphere
        Size: !vec { X: 1.3, Y: 1.05, Z: 1.3 }
        Position: !vec { X: 0, Y: 2.85, Z: 0 }
        Anchor: bottom
        Colour: !colour "#71c05a"
```

The cylinder halving, plainly: `Size.Y 1.0` becomes `localScale.Y 0.5` and the trunk is genuinely
1 m tall — under `primitive` the same number would give 2 m.

Everything overlaps, and the canopy is a three-tier stack: 2.6 wide at the bottom, then 2.0, then
1.3, each tier squashed flatter than it is wide and each starting inside the tier below (1.9
against the lowest tier's 0.7 – 2.3, 2.85 against the middle's 1.9 – 3.2). The trunk's top is
buried 0.3 inside the lowest tier. Three tiers in three greens — darkest at the bottom, lightest
at the top — read as foliage with depth; one big sphere on a stick reads as a lollipop, and two
that merely touch read as a snowman.

*Adapt:* squash the tiers harder (`Y` well under half of `X`) and it leans conifer; a dead tree
drops the canopy and adds two thin cylinders rotated `Z ±40`, `Anchor: bottom`, part-way up the
trunk. *Collide:* `part colliders` — a bounds box would block walking under the canopy's edge.

### `house`

**7 authored parts, 13 rendered · 4.72 x 3.72 x 3.46 m (Y +0.00 .. +3.72)** — walls, a mirrored pitched roof with a ridge beam, a tilted-fill gable, a door and mirrored windows.

```yaml
body:
  Type: model
  Properties:
    Colour: !colour "#e2d4b6"
    Parts:
      - Name: walls
        Shape: cube
        Size: !vec { X: 4, Y: 2.4, Z: 3 }
        Anchor: bottom
      - Name: roof
        Shape: cube
        Size: !vec { X: 2.7, Y: 0.18, Z: 3.4 }
        Position: !vec { X: 0, Y: 3.6, Z: 0 }
        Rotation: !vec { X: 0, Y: 0, Z: 31 }
        Anchor: right
        Mirror: x
        Colour: !colour "#a64535"
      - Name: ridge
        Shape: cube
        Size: !vec { X: 0.3, Y: 0.2, Z: 3.46 }
        Position: !vec { X: 0, Y: 3.62, Z: 0 }
        Colour: !colour "#a64535"
      - Name: gable slope
        Shape: cube
        Size: !vec { X: 2.35, Y: 0.6, Z: 0.14 }
        Position: !vec { X: 1, Y: 2.95, Z: 1.47 }
        Rotation: !vec { X: 0, Y: 0, Z: -31 }
        Anchor: top-front
        Mirror: xz
      - Name: gable centre
        Shape: cube
        Size: !vec { X: 2.5, Y: 0.75, Z: 0.14 }
        Position: !vec { X: 0, Y: 2.2, Z: 1.45 }
        Anchor: bottom-front
        Mirror: z
      - Name: door
        Shape: cube
        Size: !vec { X: 0.85, Y: 1.75, Z: 0.1 }
        Position: !vec { X: 0, Y: 0, Z: 1.47 }
        Anchor: bottom-back
        Colour: !colour "#4a3423"
      - Name: window
        Shape: cube
        Size: !vec { X: 0.7, Y: 0.6, Z: 0.1 }
        Position: !vec { X: 1.25, Y: 1.35, Z: 1.47 }
        Anchor: back
        Mirror: x
        Colour: !colour "#7db8dc"
```

The anchor-as-hinge showcase, the one legitimate mirrored part sitting at `X 0`, and the tilted
fill.

**The roof.** Pitch 31 deg is `atan(1.2 / 2.0)` — a 1.2 m rise over the walls' 2.0 m half-width. The
slab is anchored `right`, so its **ridge end** lands on `(0, 3.6, 0)` and the slab swings about that
point; its far end comes to rest at `(-2.314, 2.209)`, which is 0.314 clear of the wall edge and
0.19 below the wall top — a proper overhanging eave, and none of those numbers had to be typed.
`Mirror: x` leaves `Position.X` at 0 but flips the anchor to `left` and `Rotation.Z` to `-31`, so
the twin sweeps the other way from the same ridge. **This is the one case where a mirrored part at
`X 0` is correct rather than a z-fight** — the anchor, not the position, supplies the offset. The
two slabs can only ever butt at the apex, leaving a V-notch there, so the `ridge` beam lies along
the joint and hides it — what the overlap rule does when a seam cannot overlap.

**The gable end is the tilted fill.** One slab rotated to the roof's own `-31`, anchored
`top-front` with its top edge centred on the underside's midpoint `(1.0, 2.95)`, hugs the slope
exactly; `Mirror: xz` makes all four copies — two slopes, front and back — and the upright
`gable centre` covers the region under their bottom edges. Both gable parts sit 0.03 and 0.05
behind the wall's front plane: recessed, never coplanar with the wall or with each other, so
nothing z-fights.

*Adapt:* for two storeys, raise the walls' `Size.Y` and every roof-line part's `Position.Y` by the
same amount — pitch, eave and gable geometry are all measured from the ridge downward and still
hold. *Collide:* one `box collider` with `Fit: bounds`.

### `car`

**7 authored parts, 12 rendered · 2.04 x 1.47 x 4.20 m (Y +0.00 .. +1.47)** — chassis, cabin, a glass band, a tilted windscreen, four wheels from one part, headlights and bumpers.

```yaml
body:
  Type: model
  Properties:
    Colour: !colour "#d94f42"
    Parts:
      - Name: chassis
        Shape: cube
        Size: !vec { X: 1.8, Y: 0.7, Z: 4 }
        Position: !vec { X: 0, Y: 0.35, Z: 0 }
        Anchor: bottom
      - Name: cabin
        Shape: cube
        Size: !vec { X: 1.5, Y: 0.5, Z: 1.8 }
        Position: !vec { X: 0, Y: 0.95, Z: -0.3 }
        Anchor: bottom
      - Name: glass band
        Shape: cube
        Size: !vec { X: 1.53, Y: 0.28, Z: 1.84 }
        Position: !vec { X: 0, Y: 1.06, Z: -0.3 }
        Anchor: bottom
        Colour: !colour "#2c3e50"
      - Name: windscreen
        Shape: cube
        Size: !vec { X: 1.46, Y: 0.58, Z: 0.08 }
        Position: !vec { X: 0, Y: 1, Z: 0.93 }
        Rotation: !vec { X: -40, Y: 0, Z: 0 }
        Anchor: bottom
        Colour: !colour "#2c3e50"
      - Name: wheel
        Shape: cylinder
        Size: !vec { X: 0.66, Y: 0.24, Z: 0.66 }
        Position: !vec { X: 0.9, Y: 0.33, Z: 1.25 }
        Rotation: !vec { X: 0, Y: 0, Z: 90 }
        Mirror: xz
        Colour: !colour "#1a1a1a"
      - Name: headlight
        Shape: sphere
        Size: !vec { X: 0.22, Y: 0.18, Z: 0.14 }
        Position: !vec { X: 0.58, Y: 0.75, Z: 2 }
        Mirror: x
        Colour: !colour "#ffe9a8"
      - Name: bumper
        Shape: cube
        Size: !vec { X: 1.86, Y: 0.18, Z: 0.16 }
        Position: !vec { X: 0, Y: 0.45, Z: 1.94 }
        Anchor: back
        Mirror: z
        Colour: !colour "#1a1a1a"
```

`Mirror: xz`, the oversized slab, and the tilted fill as a windscreen.

**The wheels** are one authored part that becomes four. `Size 0.66, 0.24, 0.66` gives
`localScale.Y 0.12`, `Rotation: Z 90` lays the cylinder's axis along world X so it rolls the right
way, and centre `Y 0.33` is exactly the radius, so the tyre touches `Y 0` with no anchor needed.
Both mirrored axes carry a non-zero `Position`, so no twin is coincident with another.

**The glass band** is the oversized slab: 1.53 x 0.28 x 1.84 against a 1.50 x 0.50 x 1.80 cabin, so
it stands proud on every face it passes through. One part reads as glazing on all four sides; four
separate panes would be four parts and four chances to leave a gap.

**The windscreen** is a slab anchored `bottom` on the bonnet at `(0, 1.0, 0.93)` and rotated
`X -40`, leaning back until its top edge lands at the cabin's front roof edge
(`z = 0.93 - 0.58 sin 40 = 0.56`, `y = 1.0 + 0.58 cos 40 = 1.44`). Its foot starts 0.05 below the
bonnet's surface and its head sinks into the cabin's front face — both joins hidden by overlap.
The sloped pane is what separates a car silhouette from two stacked boxes.

**The gap under the chassis is deliberate** — anchored `bottom` at `Y 0.35`, that clearance is what
the wheels live in. The headlights are proud details, their centres on the chassis's front face.

*Adapt:* a van lengthens the cabin, moves it forward and drops the windscreen rake to `X -20`.
Wheels that must actually spin have to become child entities — `model` parts cannot carry a
`rotate`. *Collide:* one `box collider` with `Fit: bounds`.

### `crate`

**3 authored parts, 6 rendered · 1.06 x 1.05 x 1.06 m (Y +0.00 .. +1.05)** — a cube body, four corner posts from one part, a lid rim.

```yaml
body:
  Type: model
  Properties:
    Colour: !colour "#a9793f"
    Parts:
      - Name: body
        Shape: cube
        Size: !vec { X: 1, Y: 1, Z: 1 }
        Anchor: bottom
      - Name: corner post
        Shape: cube
        Size: !vec { X: 0.14, Y: 0.96, Z: 0.14 }
        Position: !vec { X: 0.45, Y: 0.02, Z: 0.45 }
        Anchor: bottom
        Mirror: xz
        Colour: !colour "#7a5427"
      - Name: lid rim
        Shape: cube
        Size: !vec { X: 1.06, Y: 0.1, Z: 1.06 }
        Position: !vec { X: 0, Y: 0.95, Z: 0 }
        Anchor: bottom
        Colour: !colour "#7a5427"
```

The cleanest `Mirror: xz`: one authored corner post becomes four.

The posts stand 0.02 proud of the body's ±0.50 faces so they read as battens rather than paint. They
are also inset 0.02 top and bottom (`Size.Y 0.96` at `Position.Y 0.02`) so that **no post face is
coplanar with a body face** — two coplanar, co-facing surfaces z-fight, and a flickering stripe
along the top of a crate is a hard bug to attribute later. Inset the smaller part; do not match the
larger one exactly.

*Adapt:* two colours is the whole palette and it is enough. *Collide:* one `box collider` with
`Fit: bounds` — this prop is the textbook case.

### `barrel`

**4 authored parts, 4 rendered · 0.84 x 1.08 x 0.84 m (Y +0.00 .. +1.08)** — staves, two hoops typed out separately, a lid.

```yaml
body:
  Type: model
  Properties:
    Colour: !colour "#7a5a34"
    Parts:
      - Name: staves
        Shape: cylinder
        Size: !vec { X: 0.78, Y: 1.05, Z: 0.78 }
        Anchor: bottom
      - Name: lower hoop
        Shape: cylinder
        Size: !vec { X: 0.84, Y: 0.09, Z: 0.84 }
        Position: !vec { X: 0, Y: 0.22, Z: 0 }
        Anchor: bottom
        Colour: !colour "#4a4a52"
      - Name: upper hoop
        Shape: cylinder
        Size: !vec { X: 0.84, Y: 0.09, Z: 0.84 }
        Position: !vec { X: 0, Y: 0.74, Z: 0 }
        Anchor: bottom
        Colour: !colour "#4a4a52"
      - Name: lid
        Shape: cylinder
        Size: !vec { X: 0.72, Y: 0.08, Z: 0.72 }
        Position: !vec { X: 0, Y: 1, Z: 0 }
        Anchor: bottom
        Colour: !colour "#8a6a40"
```

**The no-Y-mirror recipe.** The two hoops are the same part typed out twice, because `Mirror` has no
Y axis and never will. Anything that repeats up its own vertical axis — hoops, rungs, storeys, stair
treads — gets typed out.

The cylinder halving bites four times: `localScale.Y` comes out 0.525, 0.045, 0.045 and 0.04 for
`Size.Y` of 1.05, 0.09, 0.09 and 0.08. Trust `Size`; never reason about `localScale`. The hoops are
0.03 wider in radius than the staves — the oversized slab, applied radially.

*Adapt:* to lay it on its side, put `Rotation: !vec { X: 90, Y: 0, Z: 0 }` on the **entity**, not on
every part. Turning the whole prop is the entity's job. *Collide:* one `box collider` with
`Fit: bounds`.

### `rock`

**4 authored parts, 4 rendered · 2.34 x 1.29 x 1.72 m (Y -0.42 .. +0.86)** — three tilted cubes and an ellipsoid pebble, sunk below the ground plane.

```yaml
body:
  Type: model
  Properties:
    Colour: !colour "#85868c"
    Parts:
      - Name: boulder
        Shape: cube
        Size: !vec { X: 1.25, Y: 0.85, Z: 1.1 }
        Position: !vec { X: -0.15, Y: 0.22, Z: -0.05 }
        Rotation: !vec { X: -14, Y: 28, Z: 10 }
      - Name: shoulder
        Shape: cube
        Size: !vec { X: 0.9, Y: 0.65, Z: 0.8 }
        Position: !vec { X: 0.55, Y: 0.12, Z: 0.25 }
        Rotation: !vec { X: 12, Y: -20, Z: -14 }
        Colour: !colour "#9798a0"
      - Name: chunk
        Shape: cube
        Size: !vec { X: 0.6, Y: 0.45, Z: 0.55 }
        Position: !vec { X: -0.7, Y: 0.05, Z: 0.3 }
        Rotation: !vec { X: 8, Y: 50, Z: -6 }
        Colour: !colour "#74757b"
      - Name: pebble
        Shape: sphere
        Size: !vec { X: 0.45, Y: 0.32, Z: 0.4 }
        Position: !vec { X: 0.9, Y: 0.05, Z: -0.45 }
        Rotation: !vec { X: 0, Y: 35, Z: 0 }
        Colour: !colour "#6e6f75"
```

**The deliberate exception to `Anchor: bottom`.** All four parts are centre-pivoted and sunk,
reaching 0.42 below `Y 0`. Burying the lower third is what stops a rock reading as a prop resting
on a floor: a grounded rock looks placed, a sunk one looks like it was always there.

**Stone is angular, so the masses are cubes tilted on all three axes.** A cube rotated on X, Y and
Z at once presents corners and slanted facets instead of walls and a flat top, and three of them
at different tilts, overlapped into one silhouette, read as fractured stone. Smooth ellipsoids
were tried here first and read as eggs — stone is the one material where `sphere` is the wrong
first instinct. The `pebble` keeps the counter-lesson: rotating a *sphere* is a no-op unless it is
non-uniformly scaled, so it is an ellipsoid, and its `Rotation.Y 35` genuinely turns it.

> **This recipe assumes ground at `Y 0`.** On a floating platform a third of it hangs in mid-air.
> Raise the entity, or raise every part's `Position.Y` equally — do not switch to `Anchor: bottom`,
> which defeats the point.

*Collide:* one `box collider` with `Fit: bounds` — the fitted box includes the sunk third, which
is harmless under flat ground.

### `streetlight`

**5 authored parts, 5 rendered · 1.84 x 4.69 x 0.36 m (Y +0.00 .. +4.69)** — base, column, a hinged arm, a head and a matte lens.

```yaml
body:
  Type: model
  Properties:
    Colour: !colour "#3c4046"
    Parts:
      - Name: base
        Shape: cylinder
        Size: !vec { X: 0.36, Y: 0.16, Z: 0.36 }
        Anchor: bottom
      - Name: column
        Shape: cylinder
        Size: !vec { X: 0.16, Y: 4.2, Z: 0.16 }
        Position: !vec { X: 0, Y: 0.1, Z: 0 }
        Anchor: bottom
      - Name: arm
        Shape: cylinder
        Size: !vec { X: 0.13, Y: 1.5, Z: 0.13 }
        Position: !vec { X: 0, Y: 4.1, Z: 0 }
        Rotation: !vec { X: 0, Y: 0, Z: -70 }
        Anchor: bottom
      - Name: head
        Shape: cube
        Size: !vec { X: 0.5, Y: 0.16, Z: 0.3 }
        Position: !vec { X: 1.41, Y: 4.61, Z: 0 }
      - Name: lens
        Shape: cube
        Size: !vec { X: 0.42, Y: 0.06, Z: 0.24 }
        Position: !vec { X: 1.41, Y: 4.55, Z: 0 }
        Anchor: top
        Colour: !colour "#ffe9b0"
```

Anchor-as-hinge worked end to end, and what to do about matte.

**The arm** is anchored `bottom` at `(0, 4.1, 0)` and rotated `Z -70`, so it pivots at its foot and
its tip lands at `x = 1.5 * sin 70 = 1.410`, `y = 4.10 + 1.5 * cos 70 = 4.613`.

**The head's `Position` is that tip, with no anchor at all.** Centring the head on the tip buries
the arm's flat end cap inside it. Anchoring the head `top` at the same point would put the cap on
the head's top face, where a 0.065 radius disc tilted 20 deg from horizontal pokes 0.06 through it.
When a rotated part meets a static one, **centre the static part on the rotated part's endpoint**
rather than trying to make faces meet.

> **The lens does not glow.** `model` has no emissive channel, so a lens, a screen or a glowing eye
> is a pale matte part that only *reads* as lit when a real light is near it. Give the entity a
> child carrying a `light`:
>
> ```yaml
> Children:
>   lamp:
>     Position: !vec { X: 1.41, Y: 4.42, Z: 0 }     # just under the lens
>     Behaviours:
>       light:
>         Type: light
>         Properties: { Type: point, Colour: !colour "#ffd9a0", Intensity: 4, Range: 12 }
> ```

*Adapt:* a double-headed lamp is `Mirror: x` on the arm, head and lens — `Rotation.Z -70` flips to
`+70` and the second arm sweeps the other way, for free. *Collide:* `part colliders` — one bounds
box around this L-shape would block the pavement under the arm.

### `coin`

**3 authored parts, 3 rendered · 0.62 x 0.62 x 0.10 m (Y +0.14 .. +0.76)** — an upright rim and face with a diamond emblem, floating.

```yaml
body:
  Type: model
  Properties:
    Colour: !colour "#d4a017"
    Parts:
      - Name: rim
        Shape: cylinder
        Size: !vec { X: 0.62, Y: 0.06, Z: 0.62 }
        Position: !vec { X: 0, Y: 0.45, Z: 0 }
        Rotation: !vec { X: 90, Y: 0, Z: 0 }
      - Name: face
        Shape: cylinder
        Size: !vec { X: 0.55, Y: 0.08, Z: 0.55 }
        Position: !vec { X: 0, Y: 0.45, Z: 0 }
        Rotation: !vec { X: 90, Y: 0, Z: 0 }
        Colour: !colour "#f0c33c"
      - Name: emblem
        Shape: cube
        Size: !vec { X: 0.22, Y: 0.22, Z: 0.1 }
        Position: !vec { X: 0, Y: 0.45, Z: 0 }
        Rotation: !vec { X: 0, Y: 0, Z: 45 }
        Colour: !colour "#a87708"
```

**The deliberate float.** No anchor, no grounding: the coin sits between `Y 0.14` and `Y 0.76`,
because a pickup resting on the floor does not read as a pickup. "Ground everything" is a default,
not an absolute.

`Rotation: X 90` swings the cylinder's axis from world Y onto world Z, standing the disc upright in
the XY plane. The rim is **wider but thinner** than the face — 0.62 across and 0.06 deep against
0.55 and 0.08 — so only a 0.035 ring of it shows, and the face's extra depth keeps their flat
surfaces from ever being coplanar. The emblem is a cube rotated `Z 45` into a diamond, 0.1 deep
against the face's 0.08, so one part gives an emblem on both faces.

*Adapt:* spin it with a `rotate` plus an `every frame trigger` on the entity;
`Displacement: !vec { X: 0, Y: 2, Z: 0 }` takes an upright disc edge-on to face-on. *Collide:* a
`sphere collider` with `Fit: bounds` and `IsTrigger: true` — the standard pickup.

### `turret`

**6 authored parts, 8 rendered · 1.20 x 1.10 x 1.90 m (Y +0.00 .. +1.10)** — base, a dominant housing, a squashed dome, eye and twin forward-growing barrels.

```yaml
body:
  Type: model
  Properties:
    Colour: !colour "#6b7a52"
    Parts:
      - Name: base
        Shape: cylinder
        Size: !vec { X: 1.15, Y: 0.22, Z: 1.15 }
        Anchor: bottom
      - Name: housing
        Shape: cube
        Size: !vec { X: 1.2, Y: 0.6, Z: 1.25 }
        Position: !vec { X: 0, Y: 0.18, Z: 0 }
        Anchor: bottom
      - Name: dome
        Shape: sphere
        Size: !vec { X: 0.85, Y: 0.42, Z: 0.85 }
        Position: !vec { X: 0, Y: 0.68, Z: 0 }
        Anchor: bottom
        Colour: !colour "#7d8c63"
      - Name: eye
        Shape: sphere
        Size: !vec { X: 0.28, Y: 0.28, Z: 0.28 }
        Position: !vec { X: 0, Y: 0.9, Z: 0.42 }
        Colour: !colour "#e8483c"
      - Name: barrel
        Shape: cylinder
        Size: !vec { X: 0.2, Y: 1.15, Z: 0.2 }
        Position: !vec { X: 0.3, Y: 0.48, Z: 0 }
        Rotation: !vec { X: 90, Y: 0, Z: 0 }
        Anchor: bottom
        Mirror: x
        Colour: !colour "#3d434c"
      - Name: muzzle
        Shape: cylinder
        Size: !vec { X: 0.3, Y: 0.26, Z: 0.3 }
        Position: !vec { X: 0.3, Y: 0.48, Z: 1.02 }
        Rotation: !vec { X: 90, Y: 0, Z: 0 }
        Anchor: bottom
        Mirror: x
        Colour: !colour "#3d434c"
```

The killer combination — **`Rotation: X 90` plus `Anchor: bottom` makes a cylinder grow forward
from its `Position`** — and a lesson in mass order.

`Rotation X 90` maps the cylinder's local +Y onto world +Z, and `Anchor: bottom` puts the *near* end
on `Position` rather than the centre. The barrel therefore starts at `Z 0`, inside the housing, and
ends at `Z 1.15` — and lengthening it is a one-number edit to `Size.Y` with `Position` untouched.
Without the anchor, every length change would move the midpoint and need `Position.Z` corrected by
half the delta. The muzzle uses the same rotation and anchor at `Z 1.02`, overlapping the barrel's
last 0.13.

**Mass order is what makes it read as a weapon.** The housing is the widest mass here (1.2 across,
overhanging the 1.15 base disc), and the dome — 0.85 wide but only 0.42 tall — is a squashed cap
on top of it, not a second body. An earlier draft inverted that order (1.5 base, 1.0 housing,
tall dome) and the same six parts read as a mushroom. The eye is a proud detail, its centre on the
dome's surface.

*Adapt:* a turret whose dome actually tracks a target has to split — base and housing on the parent,
dome and barrels in a child entity with its own `model` and a `look at`. That is the case where one
`model` should become two. *Collide:* one `box collider` with `Fit: bounds`.

### `humanoid`

**7 authored parts, 11 rendered · 1.06 x 1.70 x 0.51 m (Y +0.00 .. +1.70)** — legs, torso, splayed arms with hands, an oversized head, hair and eyes.

```yaml
body:
  Type: model
  Properties:
    Colour: !colour "#e07b39"
    Parts:
      - Name: leg
        Shape: capsule
        Size: !vec { X: 0.24, Y: 0.7, Z: 0.24 }
        Position: !vec { X: 0.16, Y: 0, Z: 0 }
        Anchor: bottom
        Mirror: x
        Colour: !colour "#3f5875"
      - Name: torso
        Shape: cube
        Size: !vec { X: 0.6, Y: 0.62, Z: 0.36 }
        Position: !vec { X: 0, Y: 0.62, Z: 0 }
        Anchor: bottom
      - Name: arm
        Shape: capsule
        Size: !vec { X: 0.18, Y: 0.58, Z: 0.18 }
        Position: !vec { X: 0.36, Y: 1.2, Z: 0 }
        Rotation: !vec { X: 0, Y: 0, Z: 8 }
        Anchor: top
        Mirror: x
      - Name: hand
        Shape: sphere
        Size: !vec { X: 0.16, Y: 0.16, Z: 0.16 }
        Position: !vec { X: 0.44, Y: 0.63, Z: 0 }
        Mirror: x
        Colour: !colour "#eec39a"
      - Name: head
        Shape: cube
        Size: !vec { X: 0.46, Y: 0.44, Z: 0.46 }
        Position: !vec { X: 0, Y: 1.18, Z: 0 }
        Anchor: bottom
        Colour: !colour "#eec39a"
      - Name: hair
        Shape: cube
        Size: !vec { X: 0.5, Y: 0.16, Z: 0.5 }
        Position: !vec { X: 0, Y: 1.54, Z: 0 }
        Anchor: bottom
        Colour: !colour "#43322a"
      - Name: eye
        Shape: cube
        Size: !vec { X: 0.07, Y: 0.1, Z: 0.05 }
        Position: !vec { X: 0.11, Y: 1.42, Z: 0.23 }
        Mirror: x
        Colour: !colour "#43322a"
```

Capsule caps, splayed limbs, endpoint-centred hands, and big-head proportions.

**The arms** are anchored `top` at the shoulder and rotated `Z 8` so they splay clear of the torso.
`Mirror: x` negates `Rotation.Z` to `-8`, so the twin splays outward rather than crossing the body —
the mirror table's second trap, made concrete. Their shoulder ends sit inside the torso, so the
joint cannot gap. Each **hand** is a sphere centred on its arm's rotated endpoint
(`x = 0.36 + 0.58 sin 8 = 0.44`, `y = 1.2 - 0.58 cos 8 = 0.63`) — the streetlight-head move,
applied twice for free via `Mirror: x`.

**The head is deliberately oversized**: a 0.46 cube on a 0.60-wide torso, wearing a hair slab 0.02
proud on every side (the oversized slab again) and eyes half-sunk into the face plane (the proud
detail). Character props read through caricature — a big head on chunky limbs reads as *someone*
at twenty metres, where anatomically faithful proportions read as a mannequin.

> **The capsule caps here are stretched about 1.5x.** The legs are `Size.Y 0.70` against
> `Size.X 0.24`, where true hemispheres need `Size.Y = 0.48`. That elongation is what makes them
> read as limbs rather than pills, so it is deliberate — but swapping `capsule` to `cylinder` at
> identical `Size` is a one-word change with no arithmetic if you want hard limbs instead.

*Adapt:* recolour torso and legs for a uniform; for a robot, drop the hair and swap the two eyes
for one pale visor bar. A walk cycle needs child entities; `model` parts cannot be animated
independently. *Collide:* `part colliders`, or one hand-sized `box collider` for a forgiving
hitbox.

---

## Troubleshooting

| Symptom | Cause |
|---|---|
| `model 'x': needs a Parts list. For a single shape use the 'primitive' behaviour instead.` | `Parts:` missing or empty. There is no single-shape shorthand. |
| `model 'x': Parts must be a list of part maps.` | `Parts:` is a mapping or a scalar. It is a sequence — every entry starts with `- `. |
| `model 'x' part 2: each Parts entry must be a { Shape, ... } map.` | An entry is a bare scalar — usually `- cube` where `- Shape: cube` was meant. |
| `model 'x' part 0: needs a Shape (cube, sphere, capsule, cylinder, plane, quad).` | `Shape` missing or misspelled. Property **keys** are case-sensitive (`Shape`, not `shape`); enum **values** are not. |
| `unknown Anchor token 'middle'. Valid tokens: left, right (X), bottom, top (Y), back, front (Z)` | The vocabulary is exactly those six. There is no `centre` — omit the axis instead. |
| `Anchor 'left-right' names the X axis more than once` | One token per axis, at most. |
| `Anchor 'bottom-' has an empty segment.` | A trailing or doubled hyphen. |
| `Anchor must be a literal token such as "bottom-left" — a !var/!expr/!parameter anchor is not supported.` | `Anchor` is baked at transform time. It cannot be templated or driven. |
| `Invalid component value: null` | A `!vec` missing `X` or `Y`. Only `Z` defaults to 0. |
| Parse failure on a `Colour:` line | `Colour` needs the `!colour` tag and a hex string needs quotes: `Colour: !colour "#8c3b2e"`. |
| A part is invisible from one side | It is a `plane` or a `quad` — both single-sided under URP. A plane faces +Y, a quad faces -Z. |
| A part is twice as tall as intended | A `cylinder`/`capsule` `Size.Y` copied from a `primitive`, where `Size` is a raw `localScale`. Halve it. |
| A plane is ten times too big | A `plane` `Size` copied from a `primitive`. Divide X and Z by ten. |
| A part is half-buried in the ground | Centre pivot with no `Anchor`. Add `Anchor: bottom`. |
| Two parts flicker against each other | Coplanar faces (inset the smaller part by 0.02 rather than matching exactly), or a `Mirror` whose axis has zero offset in both `Position` and `Anchor`, so the twins are coincident. |
| A mirrored limb crosses the body instead of splaying out | Working as designed — `Mirror` negates `Rotation` on two of three axes. Check the twin table and pick the axis whose signs you want. |
| A rotated part's end cap pokes through what it meets | Centre the static part on the rotated part's endpoint instead of anchoring a face to it. |
| A "glowing" part looks flat and dead | `model` is matte. Add a child entity with a `light`. |
| Nothing collides with the prop | Parts carry no colliders by design. Add a `box`/`sphere collider` with `Fit: bounds`, or `part colliders`, to the entity — see [Collision](#collision). |
| A collider behaviour throws on initialisation | It is listed **before** the visual it measures. Behaviours initialise in declaration order; move it after the `model`. |
| A cylinder part collides with rounded ends | `part colliders` maps `cylinder` to a CapsuleCollider. Use `Fit: bounds` for that prop, or accept it. |

Verify with `validate_yaml` first (fastest at catching `Parts:` indentation), then `validate_game`,
which is authoritative and catches bad anchor tokens, untagged colours, `Size` as a sequence and
missing `!vec` components:

```
unity command validate_yaml --targets <file>
unity command validate_game --targets <file>
```

Neither can tell you the prop looks wrong. **Nothing automates that** — a model of pure nonsense
builds and validates perfectly. Run the game and look at it.
