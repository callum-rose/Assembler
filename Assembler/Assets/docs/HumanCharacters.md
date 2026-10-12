# Human Characters

How to build a person in the house [voxel style](VoxelStyle.md): one fully parameterised body, cut
with Z-axis bevels, dressed by recolouring and by a few extra parts. Every VoxelStyle rule (**R1–R26**)
still applies; the **H** rules here only narrow them for humans, and H10 is the one deliberate
exception (eye shape).

The rules are numbered and normative, each with a one-line **Why**. The source of truth for the
geometry is [`Tools/blender/build_human.py`](../../Tools/blender/build_human.py): it builds any
character from the knobs below, refuses a body that breaks a coupling rule, and feeds straight into
`bake_shade.py`.

| Doc | Owns |
|---|---|
| [`VoxelStyle.md`](VoxelStyle.md) | The voxel art style: grids, parts, palette, faces, articulation, shading |
| **`HumanCharacters.md`** (this file) | Human characters in that style: the body, its knobs, faces, hair, clothing |

---

## Vocabulary

| Term | Meaning |
|---|---|
| **Knob** | A named body dimension in voxels. Every dimension is a knob; none is fixed. |
| **Band** | One horizontal slab of the torso: the **chest** band on top, the **belly** band below it. |
| **Hip line** | The bottom of the belly band, where the legs enter the body. |
| **Coupling rule** | A constraint between knobs that keeps R5, R8 and R15 true. Knobs have no ranges, only these. |
| **Look** | A set of knob values, colours, a hairstyle and clothing that makes one character. |

---

## 1. The body

**H1. A human is six parts, named as below.** Clothing shapes and accessories (H12, H13) add parts;
the total stays within R9's 32.

| Part | Contains | Pivot (R16) |
|---|---|---|
| **Body** (root) | chest band over belly band | origin, the centre between the feet at ground level (R2) |
| **Head** | neck, head, face, hair | the neck's base: centre of the chest top |
| **ArmL, ArmR** | arm, hand, hidden tenon | the chest's side face, half a voxel below the arm's top |
| **LegL, LegR** | leg, shoe | the hip line, over the leg's centre |

*Why:* six rigid parts are what the walk and every other animation move; naming them the same on every
human lets one rig drive them all.

**H2. Every dimension is a knob, and any value is legal if the coupling rules (H5–H11) hold.**
There are no ranges. The template is the default for each knob:

| Group | Knob | Default | Notes |
|---|---|---|---|
| Legs | `leg_length` | 5 | visible height; one more voxel is buried in the hips |
| | `leg_width`, `leg_depth` | 3, 3 | |
| | `leg_gap` | derived | `max(1, belly_width − 2·leg_width − 2)` unless set |
| | `shoe_height` | 1 | rows of shoe colour at the bottom of the leg |
| | `shoe_length` | 1 | toe reaching forward past the leg's front |
| Torso | `belly_length`, `belly_width`, `belly_depth` | 2, 8, 4 | the belly band; trousers colour by default |
| | `chest_length`, `chest_width`, `chest_depth` | 4, 8, 4 | the chest band; top colour by default |
| | `shoulder_bevel` | 1 | |
| Neck | `neck_length` | 1 | visible; one more voxel is buried in the chest |
| | `neck_width`, `neck_depth` | 3, 3 | |
| Head | `head_width`, `head_height`, `head_depth` | 7, 6, 6 | |
| | `head_bevel` | 1 | |
| Face | `eye_inset` | 1 | columns between each eye and the head's side edge |
| | `eye_row` | derived | a third of the way up the head unless set |
| | `eye_height` | derived | H10's 1, or 2 at heads 8+, unless set |
| Arms | `arm_length`, `arm_width`, `arm_depth` | 7, 2, 2 | |
| | `hand_length` | 2 | rows of skin at the end of the arm |

The template stands 18 voxels (R3: a person): legs 5, torso 6, neck 1, head 6.
*Why:* characters differ in build as much as in clothes; ranges would be arbitrary, while the coupling
rules are exactly what keeps any combination buildable and animatable.

**H3. Bevels run along Z only — the edges that run front to back — and are 45° wedges (R7b).** By
default each is 1 × 1:

| Where | Size |
|---|---|
| Top outer corners of the chest (shoulders) | `shoulder_bevel` |
| Top and bottom side edges of the head | `head_bevel` |
| Outer top edge of each arm, outer bottom edge of each hand | 1 |
| Side edges of each sole | 1 |
| Belly corners standing out past the chest (top), chest corners overhanging the belly (bottom) | 1 |
| Bottom corners of the hips, only where the legs sit at least a voxel inside them (H6) | 1 |
| Hair volume (H11) | 1, or 2 where it stands out past the head all round |

No other edge is bevelled, and no part is rounded in plan.
*Why:* Z-only bevels round the silhouette the game camera sees most — the front — and change the fewest
edges; X-only only showed in profile, Y-only barely showed, and bevelling every axis softened the
figure into something else (see Taste calls).

**H4. Colours: at most six per human, from the palette (R10, R11), in fixed roles.** Skin, hair, top,
bottom, shoes and Ink (eyes). A marking or clothing part takes one of those six or replaces a role
that the look doesn't use (a suit's top and bottom are one colour, freeing a slot for a collar).
*Why:* fixed roles make looks interchangeable and keep every human inside R11 without counting.

---

## 2. Coupling rules

**H5. The torso is the chest band on top of the belly band, one part.** Either band may be the wider or
the deeper; where one stands out past the other, its exposed corner takes the H3 bevel.
*Why:* two bands are the least that gives shoulders, a waist and a belly their own widths.

**H6. Legs hang inside the hips.** Legs reach one voxel up into the belly band (R15) and must keep:
- `leg_gap ≥ 1` — legs that touch share a plane through the whole stride (R8);
- `leg_gap/2 + leg_width ≤ belly_width/2 − 0.5` — the leg's outer face never lines up with the hip's;
- `leg_depth < belly_depth` — the buried top stays inside the body.

The hips' bottom corners are bevelled only when `leg_gap/2 + leg_width ≤ belly_width/2 − 1`; otherwise
the bevel would cut into the buried leg.
*Why:* these are the only ways a leg can share a face with the body or open a gap at the hip.

**H7. The neck stands on the flat of the chest top:** `chest_width − 2·shoulder_bevel ≥ neck_width`.
*Why:* a neck wider than the flat sits on the slopes and its corners float.

**H8. Arms are one cross-section from shoulder to hand — no shoulder cap.**
- The arm's top is level with the foot of the shoulder bevel: `torso_top − shoulder_bevel`.
- Its inner face is half a voxel outside the **widest** band it passes (R8).
- It reaches into the body through a hidden 1 × 1 tenon, centred in depth so it is recessed from the
  arm's front face. The tenon enters the shoulder row when the chest is the widest band, and the
  belly's middle row when the belly is wider (R15).
- The hand ends at or below the hip line and above the ground.
- `arm_depth` is less than both torso depths; `hand_length` is at least 1 and shorter than the arm.

*Why:* an arm that widens at the shoulder read as a cap; one that stops above the hips reads as a stump.
The recessed tenon is the least visible way to embed an arm that stands clear of the body.

**H9. The neck and head are one part, the neck buried one voxel in the chest.** The neck differs from
the head in both width and depth, and is shallower than the chest.
*Why:* a nod or turn moves the head and neck together about the neck's base (R16); equal sizes would
share planes (R8).

**H10. Eyes are Ink: 1 × 1 when the head is under 8 tall, 1 wide × 2 tall at 8 or more.** They sit
`eye_inset` (1 or more) columns in from each side, with at least two columns between them, a third of
the way up the head by default, and below the two fringe rows. No mouth (R14: no jaw).
This replaces R13's 2 × 2 for humans.
*Why:* a 2 × 2 eye made a big human head stare; a tall narrow eye keeps it friendly and still clears
the thumbnail. One column of inset stops an eye wrapping a corner, where it read as a slit.

**H11. Hair is recoloured head voxels plus optional volume, merged into the Head part.** Volume may
stand out past the head on the top, sides and back, and forward over the brow, but never covers an
eye. It takes the H3 bevels. Hair hanging behind the neck must clear the chest through the head's full
nod (R8, R15).
*Why:* hair is the strongest identifier a voxel face has; merging it into the head keeps it moving with
the head and costs no parts.

---

## 3. Dressing

**H12. Clothing is markings first, shapes second.**
- **Markings** recolour voxels of an existing part (R12): sleeves, collars, a tie's knot, stripes,
  belts, bare arms.
- **Shapes** are extra parts, rigid with the part they hang from (Body or Head): a tie blade, a skirt,
  coat tails, a hat. Each sits half a voxel proud of the surface under it, shares no plane with any
  other part (R8), and clears every limb at full swing. A blade hanging between the legs is no wider
  than `leg_gap`.

*Why:* markings are free; a shape is worth its part only when it changes the silhouette.

**H13. Accessories follow H12:** glasses and beards as markings or proud parts that leave the eyes
visible; held props follow the prop rules in VoxelStyle.md. They count toward the six colours (H4).
- **Accessories may use the half-voxel grid.** An accessory part (not the body, head or limbs) may be
  built from half-size voxels, so it can be half a voxel thick and sit a quarter voxel proud of the
  surface under it — which keeps it clear of every body plane (R8) without swallowing the face.
- **Glasses** (`glasses`) are a `Glasses` part on that grid, a child of Head: a full rim round each eye,
  a bridge joining the tops of the rims, end pieces out to the temples and arms back along the sides of
  the head, all half a voxel thick. Each rim is 3.5 wide, leaving half a voxel of skin between the eye
  and the frame on every side. That needs `eye_inset` 2+, three columns between the eyes, a row below
  the eyes and one above them under the fringe.
- **Cheeks** (`cheeks`) are a 2 × 1 blush in the row under each eye, toward the outside of the face —
  or under the glasses' bottom rim when there are glasses.

*Why:* the eyes are the one feature every creature must keep (R13); props are props.

---

## Looks

The worked examples, all on the template body unless noted. Build any of them with
`build_human.py -- look=<name>`. Colours are palette indices from VoxelStyle.md.

| Look | Hair | Skin | Hair | Top | Bottom | Shoes | Body |
|---|---|---|---|---|---|---|---|
| `standard` | short | P61 | P15 | P57 | P56 | P06 | template |
| `long` | long, down the back | P60 | P41 | P45 | P39 | P00 | template |
| `afro` | volume one voxel proud, 2 × 2 bevels | P62 | P16 | P42 | P05 | P45 | template |
| `mohawk` | 2-wide crest bevelled to a ridge | P60 | P46 | P06 | P04 | P05 | template |
| `bun` | 3 × 3 × 3 bun at the back of the crown | P61 | P06 | P55 | P40 | P00 | template |
| `buzz` | cropped | P62 | P06 | P44 | P20 | P14 | template |
| `fringe` | fringe dropping a row over one eye | P60 | P51 | P19 | P14 | P15 | template |
| `bald` | grey at the back and sides | P61 | P01 | P48 | P10 | P26 | template |
| `suit` | short | P61 | P13 | P56 | P56 | P08 | broad: chest and belly 10 wide, belly 4 long, chest 2; P00 collar, P45 tie with a blade to mid-thigh |
| `burnham` | combover: short, swept down over half the forehead | P60 | P15 | P56 | P56 | P08 | the suit build with a 9 × 9 × 7 head, `eye_inset` 2, 1 × 1 eyes (`eye_height` 1, against H10); P08 Glasses part, P46 cheeks, P39 tie, no collar |

---

## Taste calls

Decisions from reviews that the rules alone didn't settle — precedent for close calls.

| Rejected | Chosen | Why |
|---|---|---|
| Unbevelled template | Bevels | Without them the figure read as a Minecraft/Roblox character |
| Bevels on every axis; X only; Y only | Z only (H3) | Z rounds the front silhouette; the others only showed in profile, barely showed, or softened the figure too far |
| 2 × 2 shoulder slopes | 1 × 1 (`shoulder_bevel`) | A broader, squarer torso |
| Arm widening to a 3-wide shoulder cap | One cross-section throughout (H8) | The cap read as a separate shoulder pad |
| Shoulders narrower than a wide belly, leaving a gap above the arm | Widen the chest to match | The gap made the arms look detached |
| A new skin swatch between Peach and Orange for an orange-skinned caricature | Orange P44 | Stronger read; no palette change needed |
| 2 × 2 eyes on heads 8+ tall | 1 × 2 (H10) | Large square eyes stared |
| Eyes in the head's corner columns | `eye_inset` ≥ 1 (H10) | A corner eye wraps the edge and reads as a slit |
| Glasses rims a whole voxel thick | Rims half a voxel thick on the accessory grid (H13) | Whole-voxel rims read as a bandit mask or piano keys, and swallowed the face |
| Frames in the hair colour | Ink frames, or a skin row between the top bar and the fringe | Hair-coloured frames touching the fringe merged into it and read as bangs |
| Glasses painted on the face | A `Glasses` part proud of it | The depth and its shadow separate the frames from the face and the eyes |
| Browline frames with no bottom rim | A full rim round each eye | Without a bottom rim the glasses looked unfinished |
| A white collar on a look that also wants cheeks | Drop the collar | Cheeks were the seventh colour (R11); the tie alone still reads as a suit |

---

## Method

1. **Pick the knobs** (H2): start from the template and change only what the character needs.
2. **Check the coupling rules** (H5–H11) — `build_human.py` lists every one it breaks and refuses to
   build.
3. **Colour the roles** (H4), then the eyes (H10) and hair (H11).
4. **Dress it** (H12, H13): markings before shapes.
5. **Run the thumbnail test** (R7c) and strip what doesn't survive.
6. **Animate** about the H1 pivots and re-check R8 and R15 on every frame.
7. **Bake and export**: `blender --background --factory-startup --python bake_shade.py -- build_human.py
   <fbx> Body [key=value ...]`.
