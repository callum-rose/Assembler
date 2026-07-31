# Prototypes

Throwaway code that answers a design question. Nothing here ships, and nothing here
should be promoted as-is — the code is written under prototype constraints (no tests,
no error handling, no abstractions). Rewrite properly when folding a decision in.

## `feed-ux-prototype.html`

**Question:** what should the play-the-news feed look like?
(`SHIPPLAN.md` decision #6 — "catalogue of all generated games with played/unplayed
state; news-like styling", Phase 3, days 15–19.)

**Answer (2026-07-31):** a newspaper front page — masthead, one full-width lead for
today's game, then a 2-column archive grid grouped by recency, with a quiet inline
filter riding the first section header.

Open it in a browser, no server needed:

```bash
open Prototypes/feed-ux-prototype.html
```

Six variants were built and compared over three rounds. This file is the winner,
collapsed to one version; the losing five are the primary source and live in this
branch's history:

| Commit | Variants |
|---|---|
| `ffa21591` | A Front page · B Daily edition · C The wire · D Arcade grid |
| `41e40689` | E Front page + grid (A's layout + D's grid + the filter) |
| `000d414a` | F Tighter lead (E, shorter hero, badged, filter demoted) — also carried an icon+popover filter alternative |

The rationale, the design calls worth re-arguing, and the manifest blocker are all
recorded in the comment block at the top of the HTML file. Read that before
implementing the uGUI version.

**Known blocker:** `Assembler.Remote.GameManifestEntry` is `(Id, Title, Description,
DescriptorUrl, Version)`. This design additionally needs `publishedAt`, `thumbnail`
and `channel` (`live`/`staging`). That is a store-side schema change and should land
before the feed is built.
