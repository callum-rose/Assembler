# App UX Design — "The Daily Assembler"

> **Status: design sketch, not a spec.** This document proposes a full player-facing
> UX for Assembler and the flow between its screens. It is a starting point for
> discussion — the open decisions in [§8](#8-open-decisions) should be settled before
> any of it is built. No code implements this yet.

## Contents

1. [Where things stand today](#1-where-things-stand-today)
2. [Design concept: "The Feed"](#2-design-concept-the-feed)
3. [Screen map](#3-screen-map)
4. [Wireframes](#4-wireframes)
5. [Flow (state machine)](#5-flow-state-machine)
6. [Architectural decisions](#6-architectural-decisions)
7. [Data-model additions](#7-data-model-additions)
8. [Content delivery — the daily drop](#8-content-delivery--the-daily-drop)
9. [Suggested phasing](#9-suggested-phasing)
10. [Open decisions](#10-open-decisions)

---

## 1. Where things stand today

The runtime has no UX. What exists:

- **The shell is one MonoBehaviour.** `GameBootstrap.cs` sits in `Scenes/Bootstrap.unity`,
  reads a single hard-coded descriptor name from `StreamingAssets` (default
  `MiniRacer3D.yaml`), and runs it through `Builder.BuildAsync`. That is the entire
  player-facing runtime. ("MiniRacer" is not an engine — it's just the example
  descriptor the bootstrap happens to load.)
- **A "game" is a YAML descriptor.** Player-facing metadata today is only
  `Game.Title` + `Game.Description` (`InfoDto`). ~47 example games exist under
  `Assets/ExampleGameDescriptors/`.
- **Every game runs under one `GameController` root.** `EndGame()` currently just does
  `Debug.Log("Game Over")` and `Destroy(gameObject)` — the game vanishes with **no
  dialogue and no return path**. Every descriptor is required to have a reachable
  `!gameover` (enforced by `GameOverReachability`).
- **UI already exists as composable uGUI blocks** — `ui canvas`, `ui container`,
  `text label`, `ui button`, `ui slider` — authored per-game in YAML, live-bound to
  variables/expressions. Per-game HUDs are a solved primitive; they just compose these.
- **No scene management.** A new game is "destroy the old `GameController` root,
  instantiate the new one" within a single session — there are no additive scene loads.
- **Delivery seam already present.** `GameBootstrap` reads from `StreamingAssets`, and
  Addressables `2.9.1` is in the manifest with remote-content support.

The gaps this design fills: (1) an app-shell / menu layer that does not exist at all,
(2) a real game-over dialogue with a return path, and (3) a little more per-game
metadata so a "news feed" is possible.

## 2. Design concept: "The Feed"

The mental model is a **news app where every headline is a playable game**. Not a game
launcher dressed up as news — the news framing *is* the product. A user opens the app
the way they'd open a news app in the morning, sees today's stories, and taps one to
play. The "article" is the game.

Three ideas anchor it:

- **Today is the hero.** The app opens on today's drop. One or a few fresh games, front
  and centre, dated. Yesterday and before recede below or live in an archive. This is
  what makes it a *daily* habit.
- **Categories mirror a newspaper.** Politics, Sport, Tech, Business, Weird — each game
  is tagged to a section, giving the feed familiar structure and letting you filter.
- **Every game shares one frame.** Same header treatment, same "how to play"
  affordance, same game-over card. Games differ wildly inside; the chrome around them is
  uniform, which is what makes 47+ auto-generated games feel like one app.

## 3. Screen map

```
                         ┌─────────────┐
                         │  Splash/Boot │  (logo, warm-up, fetch today's drop)
                         └──────┬──────┘
                                │
                    ┌───────────▼────────────┐
                    │      THE FEED (home)     │  ◄── main menu = news feed
                    │  today's stories + past  │
                    └───┬─────────┬────────┬───┘
        tap a story ────┘         │        └──── tabs / nav
                │                  │
        ┌───────▼────────┐   ┌─────▼──────┐   ┌──────────┐  ┌──────────┐
        │  STORY DETAIL   │   │  ARCHIVE   │   │ PROFILE  │  │ SETTINGS │
        │  (game preview) │   │ (calendar) │   │ (streak, │  │ (sound,  │
        │   [ PLAY ]      │   │            │   │  stats)  │  │  etc.)   │
        └───────┬────────┘   └────────────┘   └──────────┘  └──────────┘
                │ Play
        ┌───────▼────────┐
        │   IN-GAME       │  ◄── the Assembler descriptor runs here
        │  HUD + pause    │      (per-game UI via ui blocks)
        └───────┬────────┘
                │ !gameover fires
        ┌───────▼────────────┐
        │  GAME-OVER CARD     │  ◄── universal, framework-provided
        │  outcome + score    │
        │  [Replay][Share]    │
        │  [Next story ▸]     │
        └───────┬────────────┘
                │
          back to THE FEED
```

Bottom navigation is just **Feed / Archive / Profile / Settings**. Four is plenty.

## 4. Wireframes

### 4a. The Feed (home / main menu)

```
┌──────────────────────────────────────┐
│  THE DAILY ASSEMBLER      🔥 7   ⚙︎    │  ← masthead, streak count, settings
│  Wednesday, 1 July 2026                │
├──────────────────────────────────────┤
│ ┌──────────────────────────────────┐ │
│ │  [ TODAY ]        ● NEW           │ │  ← hero card = today's headline game
│ │                                   │ │
│ │   thumbnail / animated preview    │ │
│ │                                   │ │
│ │  POLITICS · 2 min · ★★☆           │ │  ← section · est. time · difficulty
│ │  "Coalition Collapse:             │ │  ← headline (game title)
│ │   Keep the Cabinet Standing"      │ │
│ │                        [ PLAY ▸ ] │ │
│ └──────────────────────────────────┘ │
│                                        │
│  MORE FROM TODAY                       │
│ ┌────────────┐ ┌────────────┐         │  ← smaller cards, 2-up
│ │ SPORT      │ │ TECH       │         │
│ │ thumb      │ │ thumb      │         │
│ │ "Penalty   │ │ "Ship It   │         │
│ │  Shootout" │ │  Friday"   │         │
│ │ 1m ·★☆☆    │ │ 3m ·★★★    │         │
│ └────────────┘ └────────────┘         │
│                                        │
│  YESTERDAY  ──────────────  see all ▸ │  ← older drops recede downward
│  ▸ "Rate Hike Runner"   BUSINESS ✓    │  ← ✓ = already played
│  ▸ "Heatwave Dodge"     WEATHER       │
│                                        │
├──────────────────────────────────────┤
│   📰 Feed    🗓 Archive   👤   ⚙︎        │  ← bottom nav
└──────────────────────────────────────┘
```

The whole feed can be one scrolling list; "today" is just the top, pinned section.
Played games get a subtle ✓ and desaturate slightly so unfinished ones pull the eye.

### 4b. Story detail (game preview / launch)

A lightweight "article" screen for the hero (small cards can direct-launch). This is
where the game is sold and controls are taught:

```
┌──────────────────────────────────────┐
│  ‹ Back            POLITICS · ★★☆      │
├──────────────────────────────────────┤
│         large preview / gif           │
│                                        │
│  Coalition Collapse                    │  ← Game.Title
│  Keep the Cabinet Standing             │
│                                        │
│  Ministers are resigning. Reshuffle    │  ← Game.Description (already exists!)
│  fast enough to keep a majority.       │
│                                        │
│  ┌─ HOW TO PLAY ──────────────────┐   │
│  │  ⇐ ⇒  move    ⎵  reshuffle      │   │  ← derived from the Controls section
│  └────────────────────────────────┘   │
│                                        │
│  Best: 12,400   ·   Played 3×          │  ← from local profile store
│                                        │
│            [   PLAY   ]                 │
└──────────────────────────────────────┘
```

The **"How to play" block can be generated from the descriptor's `Controls.Actions` /
`Controls.OnScreen`** rather than hand-authored — every game already declares its
inputs, so the shell renders a control legend for free.

### 4c. In-game HUD (per-game, shared conventions)

Games draw their own HUD via the existing `ui` blocks. The shell contributes only a
thin, consistent overlay so every game has a pause/exit path:

```
┌──────────────────────────────────────┐
│ ⏸            SCORE 1,240        ⏱ 0:45 │  ← top strip: pause (shell) + game HUD
│                                        │
│                                        │
│            [ the game itself ]         │
│                                        │
│                                        │
│   ◀  ▲  ▼  ▶            (on-screen     │  ← OnScreen controls (already in YAML)
│                          touch pads)   │
└──────────────────────────────────────┘
```

Pause opens a small sheet: **Resume / Restart / How to play / Quit to Feed**. Quit
routes through the same `!gameover` teardown path so there is one exit code path.

### 4d. Game-over dialogue (universal — the important one)

This should **not** be hand-authored per game. Today `EndGame` just logs and destroys.
Instead, make the game-over card a *framework-provided* overlay that every game gets for
free, fed by an outcome the game reports:

```
┌──────────────────────────────────────┐
│                                        │
│            ╭──────────────╮            │
│            │  GAME OVER    │            │  ← or "YOU WIN" / custom, from outcome
│            ╰──────────────╯            │
│                                        │
│              SCORE                     │
│              1,240                      │  ← from a reported score variable
│         Best 12,400  ·  New? ✦         │
│                                        │
│   ┌──────────┐   ┌──────────────┐     │
│   │  REPLAY  │   │  NEXT STORY ▸ │     │
│   └──────────┘   └──────────────┘     │
│                                        │
│      [ Share ]     [ Quit to Feed ]    │
│                                        │
└──────────────────────────────────────┘
```

**"Next story ▸" is the retention hook** — it keeps a player rolling through today's
drop the way autoplay keeps you on a video app.

## 5. Flow (state machine)

```
 Boot ─► Feed ─► StoryDetail ─► Playing ─┬─► GameOver ─┬─► Feed
   ▲       ▲          │            ▲      │             ├─► Playing (Replay)
   │       │          └─(direct)───┘      │             └─► StoryDetail (Next)
   │       └───────────────────────────────┘  (Quit to Feed)
   └─(cold start only)
```

Two rules keep it simple:

- **Every game entry point goes through `Builder.BuildAsync(descriptor)`** — the same
  call the bootstrap and editor launcher already use. The shell just chooses *which*
  descriptor and *when*.
- **Every game exit goes through the `!gameover` teardown**, which now shows the
  game-over card *before* destroying the `GameController`, then hands control back to the
  shell.

## 6. Architectural decisions

These shape everything above and should be settled first.

### 6a. Is the shell native Unity, or an Assembler descriptor?

- **Native scene** (uGUI / UI Toolkit driving `Builder`): conventional, easy to make
  polished, but a second UI system to maintain alongside the `ui` blocks.
- **Dogfood the descriptor system** (the feed is itself a descriptor whose buttons
  launch other descriptors): everything is one system, proves the UI blocks are strong
  enough for real screens, and a generated menu could vary daily. But it needs the engine
  to load a game *from within* a game (nested / sequenced descriptors), which does not
  exist yet.

**Recommendation: native shell, descriptor games.** Keep the persistent chrome (feed,
nav, game-over card, pause) native and durable; keep the disposable daily content as
descriptors. A native game-over card means all ~47 games inherit it with zero YAML
changes. This fits the "no scene management, swap the `GameController` root" model
already in place.

### 6b. Game-over becomes a framework feature, not a per-game responsibility

Extend the existing `!gameover` path to carry an **outcome payload** — win/lose + a
score value + an optional message. Concretely: `EndGame.Execute` already runs; have it
report an outcome (e.g. a designated `score` variable and a win/lose flag) up to the
shell, which renders the universal card. Games opt into richer cards just by naming a
score variable; games that don't get a plain "Game Over → Replay / Next."

This is the single highest-leverage change: it makes *every existing game* shippable
without editing any of them.

## 7. Data-model additions

The feed's cards need a little more than `Title` + `Description`. Extend `InfoDto` (the
`Game:` block) with **optional** metadata so descriptors stay backward-compatible:

| Field | Purpose in UX | Example |
|---|---|---|
| `Section` / `Category` | Newspaper section, filtering, card colour | `Politics` |
| `Headline` | Short punchy card title (distinct from `Title`) | `"Coalition Collapse"` |
| `Date` | Which daily drop it belongs to | `2026-07-01` |
| `Difficulty` | The ★★☆ chip | `2` |
| `EstPlaySeconds` | The "2 min" chip | `120` |
| `ScoreVariable` | Which runtime variable feeds the game-over score | `score` |
| `Thumbnail` | Card art (the asset-generation pipeline already exists) | `coalition.png` |

All optional — a descriptor with none of these still shows up in the feed with sensible
defaults. The LLM generation prompt can be extended to emit them, so daily drops arrive
feed-ready.

## 8. Content delivery — the daily drop

`GameBootstrap` already reads descriptors from `StreamingAssets`, and Addressables is in
the manifest. The delivery story:

1. A daily job runs the existing generate → build → verify loop, producing N descriptors
   + metadata + thumbnails for the day.
2. They are published as an Addressables content catalog (remote).
3. On launch, the shell fetches "today's" catalog entry, populates the feed, and
   downloads descriptors on demand (or prefetches today's set).
4. Played state / scores / streak live in local storage (`PlayerPrefs` or a small save
   file) keyed by descriptor id + date.

This means **no app-store update to ship new games** — the whole premise works.

## 9. Suggested phasing

1. **Universal game-over card + return-to-shell** ([§6b](#6b-game-over-becomes-a-framework-feature-not-a-per-game-responsibility)).
   Highest leverage; makes all ~47 games feel finished. Build a minimal native shell
   that lists descriptors and launches them.
2. **The Feed proper** — masthead, today's hero, category chips, played/streak state,
   bottom nav.
3. **Metadata + LLM emits it** ([§7](#7-data-model-additions)), story-detail screen,
   generated "how to play" from `Controls`.
4. **Archive/calendar, Profile/stats, Share, "Next story" autoplay.**
5. **Remote daily drop** via Addressables ([§8](#8-content-delivery--the-daily-drop)).

## 10. Open decisions

- **Shell tech** — native chrome + descriptor games (recommended), or dogfood the `ui`
  blocks for the menus too? ([§6a](#6a-is-the-shell-native-unity-or-an-assembler-descriptor))
- **Game-over** — framework-owned card (recommended, highest leverage) vs. per-game
  authored? ([§6b](#6b-game-over-becomes-a-framework-feature-not-a-per-game-responsibility))
- **Card tap** — instant play vs. story-detail "article" screen in between?
  ([§4b](#4b-story-detail-game-preview--launch))
