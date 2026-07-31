# Prototypes

Throwaway code that answers a design question. Nothing here ships, and nothing here
should be promoted as-is — the code is written under prototype constraints (no tests,
no error handling, no abstractions). Rewrite properly when folding a decision in.

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
sheet, game over.** Four decisions were settled before building and are *not* varied:

1. Every card opens a **detail page** — nothing direct-launches. The one exception is
   "Next game" on the game-over card, which launches directly because the hook only
   works with momentum.
2. **"How to play" is authored** in the descriptor (`Game.HowToPlay`), not derived from
   `Controls` bindings — mobile bindings are Input System paths like
   `<Touchscreen>/primaryTouch/position` and would render as garbage. The pause sheet
   reuses the same block verbatim.
3. The feed shows the lead + the newest 6, then an archive button. **Search and the
   played/unplayed filter live on the archive only.**
4. **Game-over outcome rides the `EndGame` behaviour** (`Outcome: win|lose`, `Score:`),
   not the `Game:` header — a descriptor can have several `!gameover` listeners, so the
   outcome belongs at the call site. Descriptors that set nothing get a plain
   "Game Over" card, so all ~47 existing ones keep working untouched.

**Open question, varied in the prototype — navigation.** Switch with the arrows in the
yellow bar, the ← / → keys, or `?nav=A|B|C`:

| | Treatment | Trade-off |
|---|---|---|
| **A** | Search + gear in the masthead | Newspaper identity survives, full height is content; two icons to learn |
| **B** | Bottom tab bar | Unmissable, and the only variant with an obvious future home for Profile; eats ~74px everywhere and reads as app chrome on a front page |
| **C** | Gear only; archive at the foot of the feed | Fewest chrome elements; scroll halfway down and there is no route to the archive at all |

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
