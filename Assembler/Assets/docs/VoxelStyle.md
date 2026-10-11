# Voxel Style

The house art style for anything Assembler builds out of parts: creatures, vehicles, props and
buildings. It is voxels — but each **part** sits on its **own grid**, rotated to suit the part, so a
fin, a limb or a roof is a clean angled slab instead of a staircase.

The rules are numbered and normative; each carries a one-line **Why** so an edge case can be decided
by intent rather than by guesswork. Rules marked **[checkable]** can be verified by a program; until
tooling exists, verify them by hand. Everything here is about how a model *looks and moves* — the
file format a model is stored in is deliberately out of scope.

| Doc | Owns |
|---|---|
| [`Models.md`](Models.md) | Composing visuals from the engine's primitive meshes with the `model` behaviour |
| **`VoxelStyle.md`** (this file) | The voxel art style: grids, parts, palette, faces, articulation, buildings, shading |

![The worked example, three-quarter view](../../../docs/images/voxel-style/shark-three-quarter.png)

---

## Vocabulary

| Term | Meaning |
|---|---|
| **Voxel** | One cube, 0.1 m on a side. The only unit of size in this style. |
| **Part** | A rigid block of voxels on one grid. Parts are the unit of composition, colour and motion. |
| **Grid** | A part's own voxel lattice: an offset and a rotation relative to the model. |
| **Main grid** | The model's own axis-aligned grid. |
| **Root part** | The one part every other part hangs from — the body, the chassis, the main building mass. |
| **Seam** | Where a part meets the part it attaches to (its **parent**). |
| **Pivot** | The single point a moving part rotates about. |
| **Embedded** | The portion of a part that reaches inside its parent and is never seen. |

---

## 1. Scale and axes

**R1. One unit is one metre; one voxel is 0.1 m, everywhere.** A voxel is the same size on a turtle
as on a house.
*Why:* a single voxel size is what makes every model in the game read as the same material.

**R2. Axes: forward is +Z, up is +Y. The origin is the point the model would turn about in the
game, at ground level** — for anything that stands, the centre between its contact points (the feet
of a biped or quadruped, the wheels of a vehicle, the footpads of a lander). For a subject that
stands on nothing (a swimming shark, a bird in flight) it is the centre of the root part. The origin is defined at the rest pose; a bobbing root leaves it
during the animation.
When authoring in Blender, up is +Z and the front faces −Y; the FBX export (forward −Z, up Y)
converts to Unity's axes.
*Why:* matches [`Models.md`](Models.md) and Unity, and puts the origin where gameplay code will
rotate and place the model.

**R3. Real-world size sets the scale; the reference sets the proportions.** If the model's spec gives
a size, use it. Otherwise size the model from the subject's real size — a ~5 m giraffe is ~50 voxels tall, a 2.4 m shark ~24 voxels long, a person
~18 tall — then shape every part to the reference's proportions within that size. A stubby toy-car
reference stays stubby: the taxi is ~3 m long, not a 4.2 m sedan. The spec may override the
reference's pose or details (an octopus standing on straight tentacles instead of the reference's
sprawl); the reference then supplies character, colour and whatever the spec doesn't replace. Anything meant to read as a
creature or object is at least 12 voxels on its longest side; small subjects are scaled up
toy-style until they clear that floor, and if the reference's proportions need more voxels than the
floor (a 5-voxel head on a 12-voxel shell makes the turtle 19 long), the model gets them.
*Why:* real size keeps models consistent with each other; the reference's proportions are what give
each one its charm — a toy car stretched to real proportions stops being a toy car. Below ~12 voxels
a creature is a smudge.

**R3a. No reference image? Write a design note first, and treat it as the reference.** List the
features that make the subject recognisable, mark which you keep and which you drop (and why), and
state the key proportions as numbers ("roof : wall height 35 : 26", "legs splay 45°"). Everywhere this
document says *the reference*, read *the reference or the design note*; compare the model against the
note, and run the thumbnail test (R7c).
*Why:* without a target there is nothing to iterate against; a written note is a target a generator
can produce, check and be held to.

---

## 2. Parts and grids

**R4. Every part is made of whole voxels and R7b wedges; its dimensions are whole voxels on every
axis. The smallest part is 1 × 1 × 1.** [checkable]
*Why:* whole-voxel parts are what keep this a voxel style rather than free modelling.

**R5. A part's grid may sit on a half-voxel offset — the root included.** A 5-wide snout centres on
the same line as a 6-wide body by shifting half a voxel; a 9-wide body centres on the origin with its
edges at ±4.5. Offsets are measured in the parent's grid.
*Why:* lets parts of odd and even widths share a centreline, and is the main tool for satisfying R8.
[checkable: every offset component is a multiple of 0.5 voxel]

**R6. At the rest pose, a part's rotation may be about any axis, in steps of 5°.** Animation is
unconstrained: once the model moves, any angle is fine. [checkable]
*Why:* snapped rest angles keep independently generated parts consistent and make R8 tractable;
snapping motion would only get in the way (a car body pitching 5° is a wheelie).

**R7. A part gets its own rotated grid when its dominant edge is more than 10° off the main axes;
otherwise it stays on the main grid. The root part is never rotated** (it may still take a
half-voxel offset, R5), **and its whole-body motion — a bob, a roll, a sway — pivots about the
origin.**
*Why:* a mechanical threshold a generator can apply; an unrotated root keeps the model square to the
world, and pivoting it about the origin keeps whole-body motion where gameplay already rotates the
model.

**R7a. Prefer slopes to staircases.** Where a form rises, falls or tapers in a run of two or more
steps in the same direction, replace the run with an angled part at a 5° step — usually 45°, and a
45° bevel is always a wedge (R7b). A domed turtle shell is a level top ringed by 45° wedges, not a
stack of shrinking tiers. Single
steps and deliberate tiers may stay: a mixture of slopes and steps is the look. Most models —
creatures, vehicles, anything organic — should have angled parts; a model should be fully
orthogonal only when its subject is (a die, a crate).
- **Silhouette first.** On boxy subjects — vehicles, buildings, props — slopes are few and short,
  used as bevels. A toy car with long slopes everywhere reads as a different, angrier vehicle.
- **Build a slope by tiering, then bevelling.** Step the mass down in tiers and bevel the steps with
  wedges (R7b). Laying an angled slab over a box corner adds bulk instead of cutting the corner away.
- **Small lips are acceptable.** A slope at an angle other than 45° rarely meets both its ends
  exactly in whole voxels; a lip or notch of up to about half a voxel where it lands is fine.
- **Form only.** This rule is about shape. Markings (R12) are colour and may step diagonally — a
  triangular window painted in voxels is fine.
*Why:* angled parts are what distinguish this style from plain voxels; a model built only on the
main grid has thrown away the style's main tool — but slopes serve the silhouette, never the
reverse.

**R7b. A 45° bevel is a wedge that exactly fills its corner.** To bevel a k × k corner, notch the
mass k × k and fill the notch with a right-angled wedge whose two legs are k voxels: its sloped face
is exactly k√2 wide and meets both neighbouring faces edge to edge, so the corner reads as one
continuous angled surface. The wedge runs the full width of the surface it bevels, flush with its
sides — never inset, never proud. Its legs and its length are whole voxels.
- **A wedge is merged into the part it bevels**, not a part of its own: it is shape, not structure.
  Merged, it shares no faces with its part and does not count toward R9's cap.
- **Where wedges meet at a corner**, cut both ends on the diagonal so neighbours share an end face and
  the bevel turns the corner continuously (a shell crest, a roof edge). Length is measured along the
  wedge's outer bottom edge.
- **Colour bands and markings stop at the edge of a bevel**; the wedge takes the colour of the faces
  it joins. Where those two faces differ, it takes the colour of the surface it continues — the
  larger or upper one — and the other colour stops at the bevel.
- **A round part** — a wheel, a dish — is one square slab with all four corners bevelled this way:
  an octagon, every piece the same thickness. A dish may recess its centre. Use round parts only
  where the roundness survives the thumbnail test (R7c): things that spin, and round outlines that
  face the camera and define the subject. Everything else stays square.
- **Bevelled openings** — the inside corners of a hole bevelled into an octagon window or a
  round-topped door — are allowed on holes that go right through, and should be rare: a square
  opening usually carries the same character (see Taste calls). A door leaf's bevel matches its
  opening's.

[checkable: a wedge's legs equal its notch; its length equals the bevelled edge]
*Why:* a rotated box cannot fill a corner — a whole-voxel slab is either too long or too short for
the k√2 diagonal, so its ends poke out past the faces it should meet or stop short of them, and an
inset slab leaves a step at each side. A wedge sized to its corner is indistinguishable from a
bevelled surface.

**R7c. Keep only what survives the thumbnail test.** Render the model about 80 pixels tall from the
thumbnail camera — a three-quarter view, 30° above the horizon, until the game's own camera is fixed,
then that camera. A feature stays only if it changes the silhouette at that size or is part of what
makes the subject recognisable; otherwise simplify it to the simplest shape that still passes
(square beats octagon, no lip beats a lip) or drop it. Small insets, lips, trims and roundings go,
even when the reference shows them.
- A feature *reads* if it is at least 3 pixels across in the thumbnail. Eyes always stay.
- A round part earns an octagon only if it is at least 8 voxels across; smaller, it is square.
- Parts another rule requires (a ridge cap, R21) stay, in their simplest form; the test strips
  optional detail only.
*Why:* detail the player can't see costs parts, colours and checks, and makes generated models look
fussy. The test turns "too much detail" from taste into something a generator can run. Where it
cannot decide, the [Taste calls](#taste-calls) are precedent.

**R8. Overlapping parts must not share a face plane — in any pose — unless the coinciding faces are
the same colour.** Coincident faces of *different* colours flicker in the game. Separate them by
giving the parts different widths, a half-voxel offset (R5) or a different angle. In practice a child
part usually needs a half-voxel offset or a width different from its parent: treat that as the
default, not the exception — and choose widths across the whole model, not just child against
parent, since unrelated parts collide too. Check the rest pose, every frame of every animation, and
each moving part at the instant it passes back through its rest angle — wherever that falls in the
cycle, even between frames. At that instant its faces can line up exactly with its parent's; a hair
either side they don't, so a check that only samples frames can pass by luck. A part that dwells at
its rest angle (a hip through a whole stance) is checked throughout. [checkable]
The same-colour exemption covers only parts that bake their AO as one group (R24) — parts that
never move relative to each other. Parts that move relative to each other carry different baked AO,
so even same-colour coincident faces flicker between them: moving parts never share a plane.
*Why:* this was the most frequent artefact while developing the style — snout against jaw, tail
stalk against body, an octopus's knees. Offline renderers (Blender's Cycles) show any coincident
faces as black, so previews may look wrong where the game will not.

**R9. At most 32 parts per creature, vehicle or prop.** Buildings are unlimited, but every distinct
roof plane counts as one part.
*Why:* a cap forces simplification — an octopus gets fewer, chunkier tentacle segments, which suits
the style.

---

## 3. Colour and finish

**R10. Every colour comes from the [shared palette](#palette).** [checkable]
*Why:* one palette is what makes separately generated models sit together in one scene.

**R11. At most 6 palette colours per model — eyes and mouth included.** Using a neighbouring shade
from the same family to separate forms the reference shows in one colour (a shell rim from its
shell) is encouraged; it still counts toward the six. [checkable]
*Why:* few colours read as designed; many read as noise.

**R12. Flat matte colour, one colour per voxel. No textures, gradients, soft bevels or outlines.**
Markings — spots, patches, stripes, a checker band — are recoloured voxels within a part. That is
not a texture; they count toward R11's six colours.
*Why:* the silhouette and the angled planes carry the form; surface treatment fights them.

---

## 4. Faces

**R13. Every creature has eyes, in Ink (P08), that read at thumbnail size (R7c).** As a minimum, one
voxel per eye, or 2 × 2 when the head is 8 or more voxels tall or the model is 40 or more voxels on
its longest side; the shape may follow the reference (a moose's 2 × 1 slit) as long as it still
reads. Eyes are recoloured voxels of the head part, not parts of their own, so they move with it. A
mouth (R14) is Mouth (P07).
*Why:* missing eyes was the most common failure across the test models; a creature without them
reads as an object.

**R14. A mouth is optional, and is a dark row on a hinged jaw part** (see R19). Only creatures whose
silhouette has a jaw get one.
*Why:* a jaw row reads as a closed mouth at rest and a gape when it opens, with no extra parts.

---

## 5. Articulation

Parts exist so they can move. Every rule here came from an artefact: necks that detached, seams that
went black, a tail that floated, a jaw that clipped.

**R15. A moving part must never show a gap at its seam — anywhere in its range of motion.** Achieve
it by letting the part reach inside its parent: the embedded portion is never seen at rest, and
fills what would otherwise open up as the part turns. How deep is whatever works; one voxel is the
usual starting point. A gap means daylight through the joint: test that the child's buried end
stays inside the parent in every pose, including both extremes of the motion, *and* that no light
passes between the two parts. Side faces turning out of the parent as the part swings are expected,
not a gap; so is a small notch on the outside of a bend between two limbs of equal width (R17).
[checkable]
*Why:* a part that merely touches its parent visibly detaches the moment it rotates.

**R16. A moving part only rotates, about one fixed pivot placed where the real joint would be — a
hip, a shoulder, the base of a neck, a hinge edge, an axle — on or inside its parent.** It never
moves its position relative to its parent, and its pivot is never in empty space. Only the root may
move as a whole (R7). A part's grid placement (R5) and its pivot are independent — don't force them
to coincide. Pivot rules apply to moving parts only; a part that never moves may anchor wherever
keeps its grid on the lattice. [checkable: the pivot is fixed and on or inside the parent]
*Why:* a pivot off the parent makes the part slide across the seam and look "floaty"; a pivot where
the real joint is keeps it attached, swings it the way the eye expects (a door on its hinge edge
opens to 85°; on its centre it jams at 45°), and makes R8 provable.

**Ground contact.** In an in-place walk, the root bobs so the lowest foot always touches the ground:
a leg that swings about its hip otherwise pushes its sole below it. Feet sliding along the ground is
fine — that cartoony walk is part of the look. Where a foot must visibly lift, make the leg a chain
of two parts (thigh and shin), each rotating about its own seam pivot; R16 already allows chains.

**Vehicles.** Wheels hang from the body, so any bob or pitch of the body lifts them: keep it slight,
with the lower axle on the ground. A lift of up to about half a voxel is invisible at game scale.
There is no suspension. Things that fly or land (a lander, a bird) may hover clear of the ground;
their rest pose is the touchdown pose.

**Cycle speed.** Smaller animals step faster. A walk loop lasts
**frames = round(36 × √h)** at 24 fps, where **h** is the *real* animal's hip height in metres —
not the model's, which R3 may have scaled up. A goose (0.3 m) walks in 20 frames, a dog (0.6 m) in
28, a moose (1.2 m) in 39, a giraffe (1.8 m) in 48. A subject without legs to measure (a fish, an
octopus) uses its real body height instead. Character may override it: a turtle is slower than its
size suggests.

**Loops.** Every animation loops seamlessly: the last key equals the first, with cyclic or linear
interpolation at the seam, so the loop doesn't hitch.
*Why:* a small animal on a big animal's cycle looks floaty — the goose did, on the shark's 36 frames.

**R17. A moving part either narrows into its parent, or matches its width with a hidden tenon and
a diagonal hinge.**
- **Narrowing** (the default for tails, necks, stalks): the child sits inside the parent's
  cross-section — a 3 × 3 tail stalk enters a 4 × 4 body rear, on a half-voxel offset (R5).
- **Constant width** (limbs that should read as one even width, like an octopus's tentacles): the two
  segments meet end to end with the pivot at the centre of the join; only the child's buried portion
  is narrower — a tenon, e.g. a 1 × 1 stub inside 2 × 2 segments — so it fills the joint as it bends.
  The hinge axis must run diagonally to the limb's faces, never parallel to a pair of them, or those
  faces stay coplanar through the bend and flicker. A small notch on the outside of the bend is
  accepted (R15).

*Why:* a child the same size as its parent shares its planes, which turning cannot separate — an R8
failure on every frame. The octopus's side tentacles, hinged parallel to their faces, flickered at
every bent knee; its corner tentacles, hinged diagonally, never did.

**R18. Any hierarchy is fine, while building and in the export, as long as the animation plays
correctly in Unity.** Verify it there, not in the authoring tool.
*Why:* only the result in the game matters. Caution: nested parts have come back rotated or
misplaced through the FBX export before; if that happens, flatten — every part a direct child of the
root, with chained motion baked into each part's own animation.

**R19. A jaw shares its hinge line with the head, at the mouth corner, and rests one 5° step further
open than the head's own pitch. Opening only rotates it further away.**
*Why:* at the head's exact pitch the two slabs would touch face-to-face (R8); one step open leaves a
thin dark mouth line, and rotating away from the head can never clip into it.

---

## 6. Buildings

**R20. Real-world building dimensions, in whole voxels:**

| Element | Voxels | Metres |
|---|---|---|
| Wall thickness | 2 | 0.2 |
| Storey, floor to floor | 30 | 3.0 |
| Floor slab | 2 | 0.2 |
| Door opening | 10 × 21 | 1.0 × 2.1 |
| Window sill height | 9 | 0.9 |

*Why:* a person is 18 voxels tall (R3); these are the sizes that make a building usable at that
scale.

**R21. Each roof plane is one part, on a grid rotated to the roof pitch, 2 voxels thick.**
- **Ridge.** Two planes meet cleanly only at 45°; above that, both planes' top ends poke past each
  other, so a pitch steeper than 45° needs a ridge cap on the main grid.
- **Gables.** A gable wall on the main grid steps up under the roof; bury every step inside the roof
  plane's thickness, so no step shows and R7a is not broken.
- **Kinks.** Where two planes of different pitch meet (a flared eave), the upper plane runs one voxel
  past the joint, and the two planes differ in width so their end faces don't coincide. Parent the
  lower plane to the upper one, so the joint lands on whole voxels in its grid (R5).

*Why:* the same move as a fin — a clean slope instead of a staircase — with the joins the first real
roof needed.

**R22. Buildings are enterable: floors, internal walls and stairs. Furniture is props, and follows
the prop rules.** A door is a leaf one voxel larger than its opening at each side and the top,
hung on the inside face of the wall, hinged on its edge (R16), opening at most to one 5° step short
of flat against the wall — 85°.
*Why:* the building owns its structure; everything you could pick up or move is a prop.

---

## 7. Shading

Flat palette colour (R12) plus ambient occlusion baked once, offline, into the mesh — real occlusion
from a path tracer, free at runtime. The bake has to stay believable while parts move, which is what
these rules are for.

![The shark pulled apart, showing each part's baked AO](../../../docs/images/voxel-style/shark-exploded-ao.png)

**R23. Bake AO offline into a dedicated vertex channel, per face corner; palette colours stay
untouched.** Use Blender Cycles' AO bake into a colour attribute named `AO` in the face-corner
domain, so faces keep hard edges. The game shader multiplies base colour by it. Keep the AO reach
short — about **2 voxels** — and leave the ground out of the bake (where a model stands is not known
in advance). Subdivide large faces — every wedge face — to one segment per voxel before baking, so
the AO has the same detail everywhere. [checkable: every mesh carries an `AO` channel]
*Why:* real occlusion at no runtime cost; a separate channel keeps palette colours exact and the
strength tunable; a short reach keeps contact shading close to the seams, where pivots (R16) keep it
nearly still.

**R24. Bake each part with only itself, its parent and its children visible, averaged over the
animation.** Parts that never move relative to each other (a body's bumpers, its cabin, its roof
sign) bake as one group. Spread the poses across each part's motion *relative to its parent* — for a
spin, across its angle of symmetry — and sample often enough to catch the extremes of the fastest
motion: at least 6 poses, more for motions that peak twice per cycle. Extremes that fall between
samples are acceptable. Bake the part at each pose with its neighbours present, and average. This
one bake carries three layers:
- **self-occlusion** — a rigid part's own creases, correct in every pose;
- **seam contact** — the parent darkening the child where it enters, which barely moves because the
  pivot is on the seam;
- **averaged contact** — a moving child's shadow on its parent, smeared over the child's range so it
  never shows a hard gap or a shadow left behind.
*Why:* a single rest-pose bake goes stale as parts move — an opened jaw keeps the darkness of being
shut. Averaging trades a slightly soft contact for one that is never wrong.

**R25. Big movers never darken their parent.** Big movers are jaws, striding legs, doors and hinged
lids. Anything that sways, spins, pans or twitches is not — fins, tails, heads, wings, dishes,
wheels. Leave a big mover out of its parent's bake (R24) — its own
seam contact is kept.
*Why:* a contact shadow that has to cover a large sweep becomes a dark smear across the parent; a
missing one is never noticed.

**R26. A model that does not animate bakes once, at rest, with every part present.**
*Why:* with no motion there is nothing to go stale; the full bake is the most accurate.

Embedded portions (R15) bake almost black. That is correct — they are never seen.

---

## Palette

49 colours, grouped into material families, each family ordered light to dark. Pick by material
("wood", "roof"), then by shade. **Provisional** — seeded from the first thirteen test models plus
building materials; tune the values freely, the indices are what models refer to.

| # | Name | Hex | # | Name | Hex |
|---|---|---|---|---|---|
| **Neutrals** | | | **Wood** | | |
| P00 | White | `#f6f6f2` | P24 | Pale wood | `#c89a60` |
| P01 | Pale grey | `#dcdcd8` | P25 | Oak | `#9a6a3a` |
| P02 | Light grey | `#b2b2b2` | P26 | Walnut | `#6b4528` |
| P03 | Grey | `#8a8b90` | P27 | Dark wood | `#4a3020` |
| P04 | Slate | `#66676c` | **Stone & brick** | | |
| P05 | Charcoal | `#4a4a50` | P28 | Limestone | `#b8b0a4` |
| P06 | Graphite | `#2b2b30` | P29 | Stone | `#8a837a` |
| **Features** | | | P30 | Dark stone | `#5c5650` |
| P07 | Mouth | `#3a2630` | P31 | Brick | `#b5543a` |
| P08 | Ink | `#141414` | P32 | Dark brick | `#8a3a28` |
| **Fur & earth** | | | **Roof** | | |
| P09 | Cream | `#ece0c0` | P33 | Terracotta | `#9c4a36` |
| P10 | Sand | `#dcc08e` | P34 | Dark tile | `#6a2e2a` |
| P11 | Fawn | `#c49470` | P35 | Slate roof | `#4a5260` |
| P12 | Tan | `#b07a50` | **Glass & water** | | |
| P13 | Chestnut | `#8e5634` | P36 | Glass | `#cfe6f2` |
| P14 | Brown | `#6e3d22` | P37 | Sky | `#8ab4d8` |
| P15 | Dark brown | `#4a2818` | P38 | Water | `#2f7fa8` |
| P16 | Umber | `#2e1810` | P39 | Blue | `#4a66b4` |
| **Foliage** | | | P40 | Deep blue | `#3a4d8c` |
| P17 | Lime | `#a6ee28` | **Accents** | | |
| P18 | Sage | `#bcc584` | P41 | Butter | `#ffd070` |
| P19 | Leaf | `#8fc23a` | P42 | Yellow | `#f6d021` |
| P20 | Olive | `#9ea26a` | P43 | Amber | `#eea62a` |
| P21 | Grass | `#5a9440` | P44 | Orange | `#f08236` |
| P22 | Forest | `#3b7038` | P45 | Red | `#c83a2a` |
| P23 | Pine | `#23452a` | P46 | Pink | `#e8899a` |
| | | | P47 | Purple | `#b868c0` |
| | | | P48 | Plum | `#7a3e86` |

---

## Taste calls

Decisions from reviews that the rules alone didn't settle. Treat them as precedent: when a choice is
close, do what these did. Every review that makes a call adds one here.

| Model | Rejected | Chosen | Why |
|---|---|---|---|
| Taxi | Real 4.2 m proportions with long slopes on bonnet, windscreen and boot | The toy reference's stubby proportions with four short bevels | Long slopes turned a cute toy car into an angular, aggressive one (R3, R7a) |
| Lander | Octagonal footpads | Square footpads | Seen from the game camera the pads are edge-on; their roundness never shows (R7c) |
| Goose | A one-voxel lip around the top of the body, copied from the reference | A plain body | The lip isn't part of what makes it a goose (R7c) |
| Cottage | Octagon window and round-topped door as bevelled openings | Square openings | The house has exactly the same character either way; the bevels were cost without gain (R7b) |
| Turtle | Shell as a stack of shrinking tiers | Level crest ringed by mitred 45° wedges | The staircase was the one place the reference read as stepped rather than shaped (R7a, R7b) |
| Moose | Antler palms rolled outward 15° | Flat palms on the main grid | The roll read as a reindeer |
| Octopus | Tentacles sprawled as in the reference | Eight straight tentacles, two joints each | Readable and animatable; the sprawl couldn't walk (R3) |
| Octopus | Tentacles tapering from 2 × 2 to 1 × 1 | Constant 2 × 2 with a hidden tenon | An even width reads as one limb; the taper read as a stick on a block (R17) |
| Cottage | Chimney leaning 5° with a further 5° cap | One stack leaning 10° | Same crookedness, one part fewer; the cap didn't survive the thumbnail (R7c) |

---

## Method

The order that has worked, as a guide rather than a rule:

1. **Silhouette first.** Block the root part on the main grid at real size (R1–R3); with no
   reference, write the design note first (R3a). Get the outline right before anything else.
2. **Split off everything diagonal.** Any part whose main edge leans more than 10° (R7) becomes its
   own part, rotated in 5° steps (R6), with its embedded portion reaching into its parent (R15).
   Then look for staircases — any run of two or more steps — and turn them into slopes (R7a).
3. **Place every moving part's pivot at its real joint** (R16). Parts that never move need no pivot.
4. **Separate coincident planes** with widths and half-voxel offsets (R5, R8, R17).
5. **Colour from the palette** (R10–R12), then eyes (R13) and, if it has a jaw, a mouth (R14, R19).
   Then run the thumbnail test (R7c) and strip what doesn't survive.
6. **Animate, then re-check R8 and R15 on every frame**, not just the rest pose — and confirm the
   animation plays in Unity (R18).
7. **Bake AO last** (R23–R26), once the shape and the animation are final: any change to either
   invalidates the bake.

---

## Examples

Five models built to these rules. Each exercises a different part of the style: the shark rotated
grids and a jaw, the turtle wedges on a creature, the taxi wedges and wheels on a boxy subject, the
lander working without a reference, the cottage the building rules. The turtle and taxi previews are lit directly with their baked AO
multiplied in (R23) — the closest stand-in for how the game shows them; the shark's are path-traced
renders, and its baked AO is shown in the [Shading](#7-shading) section.

### Shark — rotated grids, a jaw and a swim chain

Ten parts, five colours (Grey P03, White P00, Ink P08, Slate P04, Mouth P07), ~24 voxels long.
Every rotation is a multiple of 5°; every offset a multiple of half a voxel; every moving part pivots
on its parent's seam.

| Part | Grid | Size (W × L × H voxels) | Placement | Notes |
|---|---|---|---|---|
| Body | main (root) | 6 × 11 × 6 | origin | Top row inset; rear two rows narrow to 4 × 4; belly rows White; gills Slate |
| Snout | offset ½ in X, pitched 10° nose-down | 5 × 7 × 3 | hinged at the head seam | 2 rows embedded in the body; eyes 1 × 1 Ink (head is 5 tall, R13) |
| Jaw | pitched 15° | 4 × 5 × 2 | same hinge line as the snout | Top front row Mouth; opens 15° → 35° (R19) |
| Dorsal fin | offset ½ in X, swept back 35° | 1 × 4 × 9, tapering 4-3-2-1 | base embedded in the body | Straight leading edge; the taper steps only on the trailing edge |
| Tail stalk | offset ½ in X and Z, main angle | 3 × 4 × 3 | pivots on the body's rear face | Sits inside the body's 4 × 4 rear (R17) |
| Tail root | main | 2 × 3 × 2 | pivots on the stalk's end face | Reaches one voxel back into the stalk (R15) |
| Upper lobe | swept back 40° | 1 × 2 × 7 | rigid with the tail root | Hinged above the root so it never overlaps the lower lobe (R8) |
| Lower lobe | swept down-back 40° | 1 × 2 × 4 | rigid with the tail root | Hinged below the root |
| Pectoral fins ×2 | swept 35°, drooped 25° | 7 × 3 × 1, tapering 3-2-1 | pivot inside the body | One voxel embedded |

| Side | Top |
|---|---|
| ![Side](../../../docs/images/voxel-style/shark-side.png) | ![Top](../../../docs/images/voxel-style/shark-top.png) |

**Swim cycle.** A wave runs head to tail through a chain of seam pivots: the body sways 4° and bobs;
the head counter-sways 3°; the stalk follows at 13°, the tail at 20°, each a little later than the
one in front; the pectorals flap; the jaw opens once per cycle.

![Swim cycle — three-quarter and top views](../../../docs/images/voxel-style/shark-swim.gif)

### Turtle — a stepped reference, softened with wedges

The reference is plain stacked voxels. The model keeps its blocky character — a cube head, square
legs, a stepped rim — and replaces only the staircase at the top of the shell with a bevel (R7a):
a level crest ringed by four 45° wedges, mitred where they meet (R7b). That is the mix the style is
after: slopes where the reference staircases, steps where it is deliberately tiered.

![Reference (left) and model (right)](../../../docs/images/voxel-style/turtle-vs-reference.png)

12 × 19 × 10 voxels — a 0.3 m animal scaled up past the 12-voxel floor, and to 19 long because its
head and shell need it (R3). Four colours: Lime P17, Forest P22, Pine P23 (the rim, a neighbouring
shade to separate it from the shell, R11), Ink P08.

| Part | Grid | Size (W × L × H voxels) | Pivot | Notes |
|---|---|---|---|---|
| Shell | main (root) | 12 × 12 × 7 | origin, centre between the feet (R2) | Lime underside, Pine rim, Forest block; 6 × 6 crest |
| Crest wedges ×4 | main, merged into the shell | legs 2 × 2, length 10 | — | Fill 2 × 2 notches along each crest edge; ends mitred so the bevel turns the corners |
| Legs ×4 | offset ½ in Y | 3 × 3 × 5, 2 embedded | centre of each hip on the shell's underside | Spaced so their inner faces never meet the neck's planes (R8) |
| Head | offset ½ in X, Y and Z | 5 × 8 × 5 (head and neck) | centre of the neck where it enters the shell | Eyes 1 × 1 Ink |
| Tail | offset ½ in X, Y and Z | 1 × 4 × 1 | centre of the tail where it enters the rim | |

**Walk.** Legs swing ±20° in diagonal pairs, the head turns ±10° and nods ±5°, the tail wags ±20°.
The shell bobs so the lowest sole always touches the ground; the feet slide — the cartoony walk.
**AO:** the legs are big movers (R25) and leave the shell out of their shadow; head and tail sway, so
their contact is averaged into the shell (R24).

![Walk cycle](../../../docs/images/voxel-style/turtle-walk.gif)

### Taxi — a boxy subject, bevels and wheels

The style's hardest case for charm: an earlier attempt at real car proportions (4.2 m) with long
slopes everywhere read as a different, angrier vehicle. This one takes its proportions from the toy
reference (R3) and uses slopes only as four short bevels (R7a), so the silhouette stays a cute boxy
toy.

![Reference (left) and model (right)](../../../docs/images/voxel-style/taxi-vs-reference.png)

~3 m long: 30-voxel body, 32 with bumpers, 18 wide at the wheels. Nine parts, six colours — Yellow
P42, Ink P08, White P00, Graphite P06, Blue P39, Amber P43 — the cap exactly (R11). The checker band
and windows are markings (R12) and stop short of every bevel (R7b).

| Part | Grid | Size (W × L × H voxels) | Pivot | Notes |
|---|---|---|---|---|
| Chassis | main (root) | 16 × 30 × 6 | origin, centre of the wheels (R2) | Nose and boot bevelled by 2 × 2 wedges, 16 long — the full chassis width, flush with its sides |
| Cabin | main | 14 × 15 × 7 | — | Roof front and rear bevelled by 2 × 2 wedges, 14 long; windscreen and windows are Blue markings |
| Bumpers ×2 | offset ½ in X and Z | 17 × 2 × 2 | — | One voxel embedded |
| Roof sign | offset ½ in Z | 6 × 2 × 3, stepped | — | |
| Wheels ×4 | main | 2 × 7 × 7 octagon | on the axle, where it crosses the flank (R16) | One slab, four corners bevelled by Ink wedges (R7b); Graphite hub with a White bar so the spin reads |

**Drive.** Each wheel turns twice per loop, a whole number of turns so the loop is seamless. The body
pitches ±1° and bobs to keep the lower axle on the ground (Vehicles note). **AO:** the body parts
never move relative to each other and bake as one group (R24); a spinning octagon's contact is the
same at every angle, so the wheels are not big movers.

![Drive cycle](../../../docs/images/voxel-style/taxi-drive.gif)

### Lunar lander — no reference, a design note instead

Built from a text prompt alone ("an Apollo lunar module"), so a design note stood in for the
reference (R3a): the recognisable features kept and dropped, and the key proportions as numbers —
67 tall to 94 across the footpads, legs splayed exactly 45°, descent to ascent stage height 17 : 27.
The thumbnail test (R7c) then stripped the engine bell to a block, the docking tunnel to a box, and
the footpads from octagons to squares (see Taste calls). The two dishes keep their octagons: they face
the camera and read as round.

![Three-quarter, front and top](../../../docs/images/voxel-style/lander-views.png)

Thirteen parts, six colours — Amber P43 (descent stage), Butter P41 (legs), Light grey P02 (ascent
stage), Slate P04, Graphite P06 (windows), White P00 (dishes). The descent stage is an octagon of 45°
wedges (R7b); the legs are rotated-grid struts; the faceted ascent stage mixes wedges and steps.

**Idle.** It hovers just above the ground (Vehicles note) with a slow circular wobble about the origin
— the centre of its four footpads (R2) — while the S-band dish pans ±30° and the radar nods, each on
its mount (R16). Nothing on it is a big mover (R25).

![Hover idle](../../../docs/images/voxel-style/lander-hover.gif)

### Cottage — a building

The building rules (R20–R22) on a fairy-tale cottage, also from a design note. The charm is all
proportion and crookedness: a roof bigger than the walls (35 : 26), pitched 60° with a 45° flare at
the eaves, a chimney leaning 10°, one shutter hanging askew by 10°. Openings are plain rectangles
(see Taste calls).

![Three-quarter, front, top and cutaway](../../../docs/images/voxel-style/cottage-views.png)

5.2 × 4.4 × 7 m, six colours — Cream P09, Tan P12 (thatch), Walnut P26, Stone P29, Red P45 (door),
Grass P21 (shutters). The roof follows R21: a ridge cap because the pitch is above 45°, gable steps
buried in the roof planes, and each flare parented to its upper plane and running a voxel past the
kink. Inside: an internal wall, a loft floor and a ladder (R22).

**Door.** The only moving part: a leaf a voxel larger than its opening, hung on the inside face and
hinged on its edge, swinging to 85° and back (R22). It is a big mover, so the house bakes its AO
without it (R25).

![Door swing — three-quarter and cutaway](../../../docs/images/voxel-style/cottage-door.gif)
