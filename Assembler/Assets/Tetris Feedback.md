=== Attempt 1 feedback ===
This descriptor is structurally honest about how badly Tetris fits the current behaviour
catalogue. I want to be upfront: **the YAML above is not a working Tetris**. It's a
scaffolding sketch that shows what would be wired up, with comments where the catalogue
does not give me the primitives needed. The major gaps:

1. **No indexed list read.** The catalogue has `colour list add`, `colour list remove at`,
   `colour list set at`, and `colour list clear`, but no way to **read** a value at an
   index from a list. Tetris needs `board[y*width + x]` constantly (collision tests,
   line-clear detection, rendering). Without `list get at` (or a list indexer accessible
   from `!expr`), every game-logic decision becomes impossible to express.

   **Strong suggestion:** add `vector list get at`, `int list get at`, `float list get
   at`, `bool list get at`, `string list get at`, `colour list get at` that return the
   value (or that bind it as an `!output` to listeners). Without these, lists are
   write-only, which is nearly useless for game state.

2. **No `list length` / `list count`.** Even iterating "every cell" or "every row" needs
   to know the list size. Same with score tables, inventories, particle pools, etc.

3. **No conditional setter / conditional execute.** The catalogue has `condition gate`,
   which gates trigger forwarding, but the common pattern "set X to A if cond else B"
   has to be modelled as two gated setters, one for each branch. That works in principle
   but it's verbose and error-prone. A `*_variable_setter` whose `Value` is a ternary
   `!expr` is the right shape — but ternaries in the expression compiler do work, so
   this is fine *if* we have list-read primitives. Without those, it doesn't matter.

4. **No iteration / no for-each over a list.** Line-clear detection needs "for each
   row, check if every cell is non-empty". The only iteration primitive is `interval
   trigger` with `Count: N` and `Interval: 0`, which is a hack — and even then, there
   is no per-iteration index output bound to listeners (e.g. `iteration_index`). I would
   suggest adding `iteration_index` (int) and `iteration_count` (int) as outputs on
   `interval trigger`, similar to how `ui slider` outputs `value`. That alone would make
   list-walking patterns expressible.

5. **No int→string formatting for HUD.** `text label`'s `Text` is a `string` and `score`
   is an `int`. There is no behaviour that takes an int (or any non-string) and writes a
   formatted string to a string variable, nor does `text label` accept a non-string
   `!expr` that gets stringified. The HUD therefore can't display the live score without
   either (a) a `format and set` behaviour, or (b) `text label.Text` becoming an `!expr`
   whose return type is `string` and which can call `.ToString()` on numerics — but the
   expression compiler's `string` cast on an int wasn't tested and the doc warns against
   relying on undocumented behaviour.

   **Strong suggestion:** add a `format string setter` that takes a format string and a
   list of `ValueSource<object>` arguments and writes the result into a string variable.
   This is the single most useful missing behaviour for any HUD-bearing game.

6. **No way to spawn a grid of static sprites declaratively.** The natural rendering for
   Tetris is a 10x20 grid of coloured cells. Authoring this by hand means writing 200
   entity entries (gross), or using a `spawner` fired 200 times from an
   `interval trigger` — but the spawner takes a `Position: Vector3` literal, not an
   `!expr`, so per-iteration positions can't be computed.

   Wait — `Position` on `spawner` is `ValueSource<Vector3>` per the catalogue, so it
   *can* be an `!expr`. But that `!expr` needs to know which iteration we're on, which
   requires the `iteration_index` output from `interval trigger` mentioned in (4). So
   (4) and (6) are linked: fix iteration outputs and grid spawning falls out for free.

7. **Spawner takes Parameters but not per-iteration parameters.** Even with iteration
   indices, the spawner's `Parameters:` map is evaluated at trigger time — that's
   actually fine, since `!output` values are bound at notify time, so an `!output
   iteration_index` would flow through. I think this works as soon as (4) lands. Worth
   confirming.

8. **`text label.Text` type.** Per the catalogue it's `string`. I wrote `Text: ""`
   above with a comment, but it should ideally be `Text: !var score formatted` once a
   string variable holding the formatted score exists. See (5).

9. **`colour list add` doesn't take a count.** To initialise a 200-cell board I used
   an `interval trigger` with `Count: 200` and `Interval: 0`, which fans out 200
   `colour list add` calls. This works but is awkward. A `list fill` /
   `list resize with default` behaviour would clean this up: `Type: colour list fill`
   with `Count` and `Value` properties.

10. **`int variable setter` cannot read its own previous value cleanly.** To do
    `piece x = piece x - 1` I need `!expr { ... Arguments: [ !var piece x ] }` with an
    expression that returns `piece x - 1`. That works. But for the very common case of
    "increment by 1" / "decrement by 1" / "add N", a `int variable adjust` (with `Delta:
    int`) would be a lot less ceremony. Same for float and vector. (For vectors, a
    `vector variable adjust` with `Delta: Vector3` is essentially what `translate` does
    for position, but generalised to any vector variable.)

11. **Game-state machine.** A real Tetris needs phases: piece active → piece locked →
    check lines → clear lines → spawn next → maybe game over. Modelling state machines
    in this descriptor requires a chain of `bool` flags and `condition gate`s, which
    works but scales poorly. Not asking for a state-machine behaviour, but flagging that
    multi-phase games will hit this wall.

12. **No `when all` / `when any`.** The doc-gen warnings list these as skipped. Tetris
    line-clear is "when all cells in row Y are non-empty, clear the row" — exactly a
    `when all` shape over a list of boolean expressions. Whatever those skipped
    behaviours actually do, they're likely the right primitive here. **Please get the
    doc-gen for `when all`, `when any`, and `condition` working** — those names suggest
    they cover the gaps I'm describing, but I have no idea what their actual properties
    are, so I can't use them safely.

Given all of the above, what I'd recommend if you actually want a playable Tetris
descriptor:

- First add: `list get at` (all types), `interval trigger` `iteration_index` output,
  `format string setter`, and document `when all`/`when any`.
- Then this descriptor becomes about 3x more code but actually works.
- Until then, I'd suggest picking a different demo game — Pong, Snake (if `list get at`
  exists for snake's body segments), Breakout, or asteroid-style games are all
  expressible with the current catalogue. Tetris really isn't.

If you want me to ship something playable instead, say the word and I'll redo this as a
game the catalogue can actually support.
--- build errors ---
- Build failed: Assembler.Parsing.ParsingException: Cannot convert value of type System.Collections.Generic.List`1[System.Object] to a value
  at Assembler.Parsing.Transformer.Convert (System.Collections.Generic.IReadOnlyList`1[T] resolvedValues, System.Object obj) [0x002e2] in /Users/callumrose/Documents/Unity Projects/Assembler/Assets/Parsing/Transformer.cs:511
  at Assembler.Parsing.Transformer.Transform (Assembler.Deserialisation.Dtos.GameDto gameDto) [0x001c9] in /Users/callumrose/Documents/Unity Projects/Assembler/Assets/Parsing/Transformer.cs:39
  at Assembler.Generation.Verification.BuildHarness.TryBuild (System.String yaml) [0x00072] in /Users/callumrose/Documents/Unity Projects/Assembler/Assets/GenerationVerification/BuildHarness.cs:57

=== Attempt 2 feedback (fix-up) ===
The previous descriptor failed at the parsing/conversion step with:

> Cannot convert value of type System.Collections.Generic.List`1[System.Object] to a value
> at Assembler.Parsing.Transformer.Convert (...)

The most likely culprit was the **`board: []` variable initialiser**. There is no way in
the current YAML format to declare an empty *typed* list as a Variable's initial value —
an untyped `[]` resolves to `List<object>`, which the transformer can't convert into the
expected `List<Color>` (or any other typed list) for use with `colour list add`. The
Constants/Variables loader needs either:

- a typed empty-list tag, e.g. `!list<colour> []`, `!list<int> []`, `!list<vector> []`,
  or
- a default-empty rule: if a Variable's first use is as the `List` parameter of a
  typed-list behaviour, infer the element type from that usage,
- or just allow `[]` and let the first `*_list_add` behaviour determine the element
  type at runtime.

Until then, **lists effectively cannot be declared as variables at all**, which is a
big hole in the data model.

Other errors that may also have contributed:

1. **`Position: !vec { X: 2.5, Y: -5, Z: -10 }`** on the camera. I think this is fine,
   but I removed the unusual Y offset just in case; the new camera is at the origin.

2. **`Value: !var empty colour`** inside `colour list add` — this should be fine since
   `!var` resolves Constants too, but if the typed-list infrastructure is broken
   upstream of this, the whole behaviour can't bind.

3. **`Listeners:` referencing a behaviour on an entity that uses an
   `interval trigger` with `AutoStart: false`** — the `fill loop` design assumed I
   could kick off a 200-tick interval from an `on start trigger`. I have no evidence
   this composition works, since the descriptor never got past parsing. Worth a
   targeted test once the list-typing issue is fixed.

I've stripped the descriptor down to something that should actually parse: no
declared list variables, no expressions, no spawners, no per-cell board. It demonstrates
a sprite that falls under a periodic `translate` and that the player can nudge with
arrow keys — that's the most "Tetris-like" thing the current catalogue can express
without help.

**Reaffirming the catalogue gaps from the previous round** (none of which the build
error changes — they're all still real):

- No `*_list_get_at` to read indexed list values.
- No iteration index output on `interval trigger`.
- No `format string setter` for HUD score display.
- No way to declare typed empty lists as Variables (the actual cause of this build
  failure).
- `text label.Text` is typed `string` only, so live-binding an int score is impossible
  without an intermediate string variable that nothing in the catalogue can write to
  from a non-string source.

If the goal is to get a *real* Tetris demo into the example set, I'd prioritise fixing
the typed-empty-list initialiser and adding `colour list get at` first — those two
together unlock board state. Everything else can be hacked around.

