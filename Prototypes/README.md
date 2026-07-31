# Prototypes

Throwaway code that answers a design question. Nothing here ships, and nothing here
should be promoted as-is — the code is written under prototype constraints (no tests,
no error handling, no abstractions). Rewrite properly when folding a decision in.

## `app-tone-prototype.html` — round 3, OPEN

**Question:** how dark is dark, and how loud is "game-like" allowed to get before the
app stops reading as news?

```bash
open Prototypes/app-tone-prototype.html
```

Three tone variants of the whole app, each in light **and** dark. Cycle with the
floating bar, the `←`/`→` keys, or `?tone=a|b|c&theme=light|dark`; `D` toggles the
theme, which also has a row in Settings → Appearance (where the switch belongs is
part of the question).

| | Tone | Fun dial | What it changes |
|---|---|---|---|
| **A** | Broadsheet | 1/3 | Round 2 exactly, plus a dark theme and micro-motion. Press depth, a pulsing "new" dot, a score that counts up. Nothing else moves. |
| **B** | Late Edition | 2/3 | Newspaper bones, print-shop swagger. Cream/ink, a highlighter second accent, an edition ticker, hard offset buttons, numbered cards, a rubber-stamp verdict and an odometer score. |
| **C** | Arcade Edition | 3/3 | The newspaper becomes the cabinet. Condensed caps, a neon duo, scanlined thumbnails, a catalogue progress meter, cartridge cards, chunky 3D buttons, and a full-bleed result screen with a rank letter. |

Round 2's **structure is frozen** — masthead icons, lead + 6 + archive, everything
through detail. The variants disagree about surface only. Judge them on the **feed**,
not the game-over card: all three game-over cards are enjoyable, and only some of the
mastheads still read as a newspaper.

**The constraint that shaped all three:** the obvious levers for game-feel — streaks,
share, a calendar archive, difficulty chips — were all argued out in rounds 1–2 and
are on the "not in v1" list. So **no variant adds a single persisted number.** The fun
is typography, colour, motion, framing language, and making the game-over moment an
event. C's rank letter is derived from the best score the app-local store already owes
us. A variant that looks like it is promising progression is a bug in the variant, not
a licence to add the feature.

**Three dark-mode calls worth arguing:**

1. **Played cards.** Round 2 kept the played thumbnail in colour at 42% opacity because
   greyscale on a light ground reads as a broken image. On a dark ground 42% reads as
   *still loading* instead, so dark dims to 55% **and** desaturates, and the headline
   carries more of the played state. Check this against a real screenshot — the fake
   thumbnails flatter it.
2. **The red.** `#c8102e` is a masthead red on white and a muddy brown on `#101215`.
   Every tone lifts its accent in dark rather than reusing the light value; the accent
   is a token, not a constant.
3. **Inverted print elements don't survive the flip.** B's hard "ledge" shadow and its
   ticker strip are ink-on-cream in light; reused literally in dark they become a white
   border and the brightest thing on screen. Both take a separate dark token.

Nothing here adds to round 2's schema blockers — also on purpose.

## `feed-ux-prototype.html`

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
