# Ship Plan — Play-the-News Game App (27 days)

_Play-the-news game generator: every day the top news story is turned into a playable game.
The app UX is a news-style feed (à la BBC News); tapping an "article" launches a game._

## Definition of done (day 27)

An installable build of the app on **both**:
- **iOS** — TestFlight (internal or external testing track), and
- **Android** — Google Play closed/internal testing track,

usable by an invited **testing group**. **Not** public production release.

Rationale: testing tracks are fully within our control — no production review, and
crucially they avoid Google's 14-day/12-tester gate (that gate applies only to
*production* access for new personal accounts). Public production is a fast-follow after
day 27.

---

## Locked decisions

| # | Decision | Choice |
|---|----------|--------|
| 1 | Target | Testing tracks both platforms by day 27 (not production) |
| 2 | Accounts | Neither Apple nor Google account exists yet → both enrollments start day 0 |
| 3 | Compiler / AOT | iOS AOT confirmed working; run worst-case cross-platform stress test early + verify Android |
| 4 | Content pipeline | Semi-automated, human-in-the-loop: scheduled scrape → candidate stories + game ideas → approve/edit on phone → daemon generates. Manual trigger also supported |
| 5 | Approval surface | Reuse **GitHub issues + GitHub mobile app** (candidate = unlabeled issue; approve = add `generate` label). No bespoke admin UI |
| 6 | Feed | Catalogue of **all** generated games with played/unplayed state; news-like styling |
| 7 | Quality gate | Two-stage publish: daemon publishes to **staging** manifest → playtest in-app (dev mode) → **promote** to live |
| 8 | Touch controls | Fail-closed validator (fail a mobile build with no touch/OnScreen path) + hard-require mobile controls in the generate skill. Daemon self-repair then forces every game to be touch-playable |
| 9 | Thumbnails | Auto-screenshot from the validation sandbox (hard fallback) + image-gen 4-candidate pipeline (fenced, time-boxed to day 18, approved via the GitHub issue) |
| 10 | Per-game stylized UI | **Cut.** One shared UI restyle only |
| 11 | News source | Public **RSS feeds** for discovery + summary; LLM ranks & proposes ideas. Fetch article body only as *generation input* when summary is insufficient. **Never display article body/photos to users** (copyright line). Not legal advice |
| 12 | Backfill | ~10 real, playtested games — generated as a byproduct of pipeline testing |
| 13 | Testers | ~10–15 lined up; needed by ~day 22 |

### Legal posture (news content)
- **Allowed:** reading an article body as input to generate an original, transformative game.
- **Not allowed:** displaying article body text or the outlet's photos in the app.
- **Scraping caveat:** ToS/robots.txt is a contract issue, not copyright. At ~1 pull/day
  personal scale the practical risk is negligible; the clean path is RSS (its summary is
  often enough context). _Not legal advice._

---

## Current state (from codebase recon)

**Already built (strong):**
- Full `text brief → validated game → CDN → phone` pipeline.
- `Assembler.Remote` (GameShelf, remote client, on-device cache, manifest parser).
- Generation daemon in `RemoteTooling/`: watches GitHub issues labelled `generate`, runs
  the `claude` CLI + `generate-game-descriptor` skill, validates by booting a Unity
  sandbox, self-repairs on failure, publishes to store repo + manifest.
- iOS AOT compiler path (`GameBootstrap`, `link.xml`, `DelegateTypeHelper`) — proven on
  MiniRacer3D (2026-06-11).

**Not built yet (the product premise):**
- News → game ingestion (0% — no scraping/RSS/headline code).
- News-feed UX (0% — GameShelf is self-described throwaway scaffolding).
- Boot scene still launches the old single-game bootstrap; manifest URL is a `USER` placeholder.
- Touch controls not guaranteed on generated games (e.g. `FallingBlocksDodge.yaml` is keyboard-only).
- Android IL2CPP never verified.

---

## Schedule

### Day 0 (today) — start every clock you don't control
- [ ] Apple Developer enrollment (identity verification).
- [ ] Google Play Console registration + identity verification.
- [ ] Register bundle ID / package name.
- [ ] Create the real `assembler-games` store repo (kills the `USER` placeholder).
- [ ] Send the ask to testers.

### Phase 1 — De-risk the unknowns (Days 1–7)
- [ ] **Days 1–2** — Compiler worst-case stress test: one descriptor exercising LINQ +
      value-type generics + indexers + numeric promotion + nested control flow, built
      IL2CPP on **iOS and Android**. Clears the general-case AOT + Android unknowns.
- [ ] **Days 2–3** — Touch-controls fail-closed fix: validator rule + hard requirement in
      the generate skill. Re-generate `FallingBlocksDodge` as a test.
- [ ] **Days 3–4** — Wire the spine: boot scene → `GameShelf`, manifest → real repo,
      remote-load-and-play working end-to-end on a phone (dev build).
- [ ] **Days 5–7** — Mobile signing spike (early, deliberately): throwaway build onto at
      least one testing track. Android internal testing first if Apple enrollment is slow.

### Phase 2 — Build the news loop (Days 8–14)
- [ ] News-ingestion command in `RemoteTooling`: RSS pull → LLM ranks + proposes ideas →
      opens **candidate** GitHub issues (unlabeled).
- [ ] Two-stage publish: staging vs live manifest + `promote` action + app **dev-mode**.
- [ ] Auto-screenshot capture in the validation sandbox → published as the thumbnail.
- [ ] **End-to-end dry run once:** candidates → approve on phone → generate → validate
      (touch-enforced) → screenshot → staging → playtest in-app → promote → appears in feed.

### Phase 3 — Feed UX + polish (Days 15–19)
- [ ] Replace throwaway shelf with the **news-like feed**: cards (thumbnail + headline +
      blurb + played/unplayed), scrollable catalogue, local played/unplayed state.
- [ ] One shared UI restyle (font, palette, buttons, cards).
- [ ] **Day 18 checkpoint** — thumbnail pipeline decision: reliable → integrate 4-candidate
      flow; not reliable → ship plain screenshots and park it.
- [ ] Begin backfilling ~10 games through the live pipeline.

### Phase 4 — Store readiness + real builds (Days 20–24)
- [ ] Store listings both platforms: name, description, screenshots, icon, **privacy
      policy** (host a one-page site), **content rating/IARC**, **Google Data Safety** form.
- [ ] Real signed builds → TestFlight + Play closed/internal testing; add testers.
- [ ] Fix what real device builds surface.

### Phase 5 — Test, fix, ship to testers (Days 25–27)
- [ ] Testers in, feedback collected, top bugs fixed.
- [ ] Daily loop confirmed running; final catalogue seeded.
- [ ] **Both tracks live to the testing group = done.**

---

## Risk read

Tight but achievable **because the engine and delivery pipeline already exist** — the work
is ingestion, a feed skin, quality gates, and store plumbing, not a game engine. The two
biggest budget threats are **mobile signing** (mitigated by the early spike) and **scope
creep** (mitigated by the fences below).

### Cut ladder (if behind — cut top-down, no agonizing)
1. Image-gen thumbnails → plain screenshots (already fenced at day 18).
2. Scheduled cron ingestion → trigger the daily pull manually (loop is identical; only the
   trigger is manual).
3. Android → iOS-only for day 27, Android as immediate fast-follow.
