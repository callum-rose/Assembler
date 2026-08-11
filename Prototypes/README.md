# Prototypes

Throwaway code that answers a design question. Nothing here ships, and nothing here
should be promoted as-is — the code is written under prototype constraints (no tests,
no error handling, no abstractions). Rewrite properly when folding a decision in.

## `app-look-prototype.html` — the settled look

**This is the current one.** Six screens — feed, detail, archive, settings, pause,
game over — in the settled structure *and* the settled surface, light and dark.

```bash
open Prototypes/app-look-prototype.html
```

`D` toggles the theme, which also has a row in Settings → Appearance (where a player
is expected to find the switch is part of the question). Theme rides the URL
(`?theme=light|dark`) so a screenshot is reproducible.

### How it was arrived at

Rounds 1–2 settled the **structure** (see `feed-ux-prototype.html`): masthead icons,
a full-width lead then a 2-column grid, lead + 6 then the archive, everything opens a
detail page. Rounds 3–4 settled the **surface**.

Round 3 built three tones across the whole app, each in light and dark:

| | Tone | Fun dial | |
|---|---|---|---|
| **A** | Broadsheet | 1/3 | Round 2 + a dark theme + micro-motion only |
| **B** | Late Edition | 2/3 | Newspaper bones, print-shop swagger |
| **C** | Arcade Edition | 3/3 | The newspaper becomes the cabinet |

Verdict: *"B mostly. A is too bland, C is too arcadey. Make it generally feel a bit
more newspaper-y."* All three are in this branch's history at `2c8a7573` and are the
primary source. Round 4 folded the feedback into **D, "Letterpress"** (`eb0ca995`),
which is what this file now shows.

| Feedback | What it cost |
|---|---|
| Strikethrough on played games is wrong — grey them out with an icon | Strikethrough gone; played art goes greyscale with a tick badge on the corner. |
| The ticker is distracting and carries little | Replaced by a **static folio row** — same three facts, no motion, and it keeps the edition number, which nothing else on the page shows. |
| The completion bar is useless | Cut. Catalogue-completion is meta-progression wearing a hat. |
| Make it feel more newspaper-y | Print furniture that carries information rather than motion: a red kicker rule, a byline, a drop cap, **column rules between the grid cells** (the gap goes to zero — a newspaper's grid is defined by its rules, not its gutters), and a folio at the foot. The card numbers went too: numbering stories is a listicle tell, not a newspaper one. |

**Kept from B**, because that was the part that landed: cream/ink, the rubber PLAY
stamp, letterpress offset buttons, the double section rule, the stamped verdict and
the odometer score.

### The constraint that shaped the whole surface

The obvious levers for game-feel — streaks, share, a calendar archive, difficulty
chips — were all argued out in rounds 1–2 and are on the "not in v1" list. So the
design adds **no new persisted number**. The only numbers on screen are best-score and
play-count, which the app-local store already owes us. If a future change looks like
it is promising progression, that is a bug in the change, not a licence to add the
feature.

### Two calls to re-argue in Unity

1. **Greyscale on played art is conditional on the tick.** Round 2 rejected greyscale
   because a desaturated thumbnail reads as a broken image. The tick badge is what buys
   the reversal — it says *played* where greyscale alone said *failed to load*. Drop
   the tick and the greyscale has to go back to colour-at-42% with it: one decision,
   not two. Note also that the grey goes on the **art**, not on its container —
   filtering the container fades the badge along with the image it exists to explain.
2. **The drop cap is not free.** TextMeshPro has no `::first-letter`; it needs a
   hand-laid glyph or `<size>`/`<voffset>` rich text. Decide before it is promised.

### Dark mode is not a token swap

- **The red.** `#c8102e` is a masthead red on white and a muddy brown on a near-black
  ground. Dark lifts it to `#e8574a` rather than reusing the light value.
- **Inverted print elements don't survive the flip literally.** The hard letterpress
  ledge under the lead art is ink-on-cream in light; reused as-is in dark it reads as
  a white border rather than depth, so it takes its own `--offset` token.
- **Played opacity is not the same number in both themes.** Light dims to 50%, dark to
  42% — on a dark ground the lighter value reads as "still loading".

Nothing in rounds 3–4 adds to round 2's schema blockers — also on purpose.

## `feed-ux-prototype.html` — the rounds 1–2 record

> **Superseded for anything visual.** This file settled the *structure*, and its
> surface is the pre-round-3 look. For what the app should look like, read
> `app-look-prototype.html` above; read this one for how the structure was argued.
> The two cover the same six screens, so it is worth deciding whether this file still
> earns its place now that git history holds the same record.

The player-facing app UX, from the feed through to the game-over card. Open it in a
browser, no server needed:

```bash
open Prototypes/feed-ux-prototype.html
```

### Round 1 — what should the feed look like?

(`SHIPPLAN.md` decision #6 — "catalogue of all generated games with played/unplayed
state; news-like styling", Phase 3, days 15–19.)

**Answer (2026-07-31):** a newspaper front page — masthead, one full-width lead for
today's game, then a 2-column grid of the rest.

Six variants were built and compared over three rounds. The losing five are the primary
source and live in this branch's history:

| Commit | Variants |
|---|---|
| `ffa21591` | A Front page · B Daily edition · C The wire · D Arcade grid |
| `41e40689` | E Front page + grid (A's layout + D's grid + the filter) |
| `000d414a` | F Tighter lead (E, shorter hero, badged, filter demoted) — also carried an icon+popover filter alternative |

### Round 2 — the rest of the app

`Assets/docs/UX.md` predates `SHIPPLAN.md` and is partly stale; the HTML comment block
records what it proposed that we are deliberately **not** building (sections, streaks,
share, a calendar archive, difficulty chips).

Six screens are now covered: **feed, game detail, archive with search, settings, pause
sheet, game over.** The decisions:

1. **Navigation is masthead icons** — search and settings in the masthead, no bottom
   bar, so the newspaper identity survives and the full screen height is content.
   Archive and Settings are pushed screens that back out to wherever they were opened
   from. Chosen over a **bottom tab bar** (unmissable, and the only option with an
   obvious future home for Profile, but it costs ~74px on every screen and reads as app
   chrome on a front page) and an **archive-at-the-foot hybrid** (fewest chrome
   elements, but scroll halfway down the feed and there is no route to the archive at
   all). Both losing variants are in this branch's history. Reconsider the tab bar if
   Profile is ever revived — a fourth destination is where two icons stop scaling.
2. Every card opens a **detail page**, with no exceptions. "Next game" on the game-over
   card goes to that game's detail page too; an autoplay-style direct launch was
   considered and rejected, because the detail page is where the controls are taught and
   dropping a player into an unfamiliar game with no briefing trades one tap for a
   confused first ten seconds.
3. **"How to play" is authored** in the descriptor (`Game.HowToPlay`), not derived from
   `Controls` bindings — mobile bindings are Input System paths like
   `<Touchscreen>/primaryTouch/position` and would render as garbage. The pause sheet
   reuses the same block verbatim.
4. The feed shows the lead + the newest 6, then an archive button. **Search and the
   played/unplayed filter live on the archive only.**
5. **Game-over outcome rides the `EndGame` behaviour** (`Outcome: win|lose`, `Score:`),
   not the `Game:` header — a descriptor can have several `!gameover` listeners, so the
   outcome belongs at the call site. Descriptors that set nothing get a plain
   "Game Over" card, so all ~47 existing ones keep working untouched.

### Blocked on schema

Nothing here can be built until these land:

- **`GameManifestEntry`** is `(Id, Title, Description, DescriptorUrl, Version)` and needs
  `publishedAt`, `thumbnail`, `channel` (`live`/`staging`).
- **`InfoDto`** is `(Title, Description)` and needs `HowToPlay`.
- **`EndGame`** is property-less and needs `Outcome` + `Score`.
- **`GameController.EndGame()`** does `Destroy(gameObject)` immediately; it must freeze,
  hand the outcome to the shell, and destroy only on dismiss. `IGameClock` already has
  `Pause()`/`Resume()`/`TimeScale`, so the freeze seam exists for the pause sheet too.
- **An app-local store** — best score and play count persist nowhere today, and both the
  detail page and the game-over card need them even though Profile is deferred.
