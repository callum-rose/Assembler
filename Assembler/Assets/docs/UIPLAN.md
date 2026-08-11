# UI Plan — Shell UGUI Architecture

_How `Prototypes/app-look-prototype.html` (variant D, "Letterpress") becomes the real app
shell in UGUI. Decided 2026-08-11 in a design session against the live prototype and the
current `Assets/Remote` code. The prototype binds the **look**; every implementation
detail below was decided on its own merits. Deferred items are GitHub issues — see the
ledger at the bottom._

---

## Locked decisions

### 1. Scene & composition root

| # | Decision | Choice |
|---|----------|--------|
| 1.1 | Shell home | One authored scene: `Bootstrap.unity` grows a `ShellRoot` — no separate shell scene |
| 1.2 | `GameShelf` | Demoted to a plain service. Its fetch → cache → parse → guard → build flow survives intact; its procedural UI (canvas, cards, status panel) is deleted wholesale |
| 1.3 | Old scaler constants | The procedural canvas's `1080×1920, match 0.5` numbers die with it — do not copy them into the new shell |
| 1.4 | DI | **EasyDI**. Composition root = application scope in Bootstrap with a `ShellInstaller : MonoInstaller` registering model singletons (game shelf service, `IStatsStore`, theme service), navigator, screen catalog. Theme/config assets arrive as serialized installer fields |
| 1.5 | Scope layering | App → Shell → per-game-session. Assumes EasyDI grows user-defined layers ([EasyDI#41](https://github.com/callum-rose/EasyDI/issues/41)); fallback is plain `CreateChild` without the named-scope sugar — works today |
| 1.6 | Presenter creation | `resolver.Instantiate<TPresenter>(...)` with the view instance passed as an additional argument (supported via `ArgumentInfo`). Per-screen child scopes remain an option for screen-scoped services |
| 1.7 | EventSystem | Authored in Bootstrap (`EventSystem` + `InputSystemUIInputModule`), replacing the runtime `EnsureEventSystem`. Project is already Input-System-only |

### 2. Canvas & coordinate system

| # | Decision | Choice |
|---|----------|--------|
| 2.1 | Canvas structure | One root Canvas (Screen Space – Overlay) → `ScreenHost` / `OverlayHost` / `GameStrip` layers by sibling order. **Each screen and overlay prefab root carries a nested `Canvas` + `GraphicRaycaster`** — isolates rebuilds, cheap enable/disable |
| 2.2 | Reference resolution | **390 × 844, Scale With Screen Size, match = width.** Prototype CSS px transfer 1:1 (16px gutter = 16 units). Reference resolution is a logical coordinate system, not a render resolution — TMP is SDF, geometry rasterises native |
| 2.3 | Raster asset rule | Bitmap art authored at **3–4× its unit size** (44-unit icon ships ≥132px) or it blurs on device |
| 2.4 | Safe area | `SafeAreaPanel` (anchors from `Screen.safeArea`) under each host; paper texture bleeds full-screen **outside** it. Ink-dark header and game strip are full-bleed with safe-area-padded content |
| 2.5 | Orientation | Shell locked portrait. Future landscape *games* unlock autorotation at play time, when only GameStrip + overlays are alive — exactly those hierarchies are orientation-robust. Scaler shim: match = width in portrait, height in landscape (short axis always 390 units). Pause/game-over panels: max content width ~390, centred |

### 3. Navigation

| # | Decision | Choice |
|---|----------|--------|
| 3.1 | Registration | `ScreenCatalog` ScriptableObject: `ScreenId → prefab`. Adding a screen = prefab + catalog entry + one installer line |
| 3.2 | Lifecycle | **Lazy-instantiate on first visit, then cache** (deactivate, don't destroy). Feed scroll position and archive search state survive for free. Per-screen `KeepAlive => false` escape hatch |
| 3.3 | Semantics | Real stack: `Push(id, params)` / `Pop()` / `Replace(id, params)`. Back-button label = the entry beneath the top. "Next game" chooses Push vs Replace per call |
| 3.4 | Params | Typed: `Push(ScreenId.Detail, new DetailParams(gameId))`; detail re-binds on every entry (cheap text/sprite swaps only) |
| 3.5 | Transitions | `OnEnter`/`OnExit` return `Awaitable`; navigator sequences exit → enter. Default is a ~120 ms crossfade — a newspaper turns pages, it doesn't slide drawers |
| 3.6 | Overlays | **Not screens.** `OverlayHost` above `ScreenHost` with its own show/dismiss API: pause sheet, result slip, launch overlay. Never in the back stack |

### 4. MVP pattern

| # | Decision | Choice |
|---|----------|--------|
| 4.1 | Split | Passive View. **V** = screen prefab component exposing only `Bind(viewData)` + events, knows nothing. **P** = plain C# class (no MonoBehaviour), stateless router between model events and view events. **M** = shelf service + stats store + immutable view records (`GameSummary`, `GameDetail`) |
| 4.2 | The stateless-P rule | **View events echo back the identifiers they were bound with** (`event Action<GameId> PlayClicked`) — otherwise presenters silently grow `currentGameId` fields and the stateless claim rots |
| 4.3 | Change propagation | Two plain C# events (`CatalogChanged`, `StatsChanged`). Screens rebind in `OnEnter`, subscribe while active. No reactive framework |
| 4.4 | Derived state | **Played is not stored**: `played ≡ plays > 0`. One source of truth |

### 5. Theming & typography

| # | Decision | Choice |
|---|----------|--------|
| 5.1 | Theme asset | One ScriptableObject: colour roles (`Ink`, `Paper`, `Accent`, `Faint`, …), named `TextStyle` entries (font asset, size, spacing, case, colour role), `Motion` timings block, layout constants |
| 5.2 | Delivery | Role-binder components (`ThemeColor`, `TextStyleBinder`) on every graphic; apply on enable + theme-changed event; `ExecuteAlways` for editor preview. Dark mode later = a second theme asset ([#575](https://github.com/callum-rose/Assembler/issues/575)) |
| 5.3 | Static accessor | Sanctioned heresy: the theme service registers in DI **and** exposes a static accessor used only by leaf binder components. Everything with a constructor uses DI |
| 5.4 | Typeface | **Newsreader** (OFL): display cut for masthead/headlines, text cut for body. All-serif — no UI sans. Swap = one asset edit |
| 5.5 | TMP atlases | Static SDF atlases, full character set, per cut/weight actually used. No cleverness |
| 5.6 | Drop cap | `DropCapFormatter` **static core** (measure → inject `<indent>` → close at line-N+1's first character — a line-boundary insertion, so exactly two passes, deterministic) + ~40-line component wrapper (`ITextPreprocessor` + rect-resize/theme-change re-runs). Cap glyph = child TMP sized from `FaceInfo.capLine` maths. Usable anywhere: one static call or one component |
| 5.7 | Voice rule | **The shell speaks serif; the machine room speaks mono.** In-game chrome (launch overlay, game strip) is monospace, deliberately distinct from the newspaper voice |
| 5.8 | Copy | en-GB fixed date voice ("31 Jul", "Pressed 07:02") — set dressing doesn't localise. All static copy in one `Copy` constants class. i18n is a non-goal ([#577](https://github.com/callum-rose/Assembler/issues/577)) |

### 6. Layout rules

| # | Decision | Choice |
|---|----------|--------|
| 6.1 | Chrome vs flow | Anchors for screen fixtures (masthead, header rule, strip); layout groups only for flowing content inside scrolls |
| 6.2 | Fitter discipline | **One `ContentSizeFitter` per scroll, at the content root, nothing below it.** Cards/rows are fixed-size; titles clamp/ellipsize, never grow their cell. The hero block is the one content-sized child |
| 6.3 | Feed grid | `GridLayoutGroup` (plain gutters, no column rules) + `GridCellSizeDriver`: `cols = max(2, floor(width / minCellWidth))`, cell size derived on rect change. Column-aware now; extra columns activate when tablet work lands ([#572](https://github.com/callum-rose/Assembler/issues/572)) |
| 6.4 | Archive | `VerticalLayoutGroup` of fixed-height `ArchiveRow`s (a dense index page, not a grid). Rows are stateless `Bind(GameSummary)` so virtualisation is a swap-in ([#574](https://github.com/callum-rose/Assembler/issues/574)) |

### 7. Prefabs & interaction

| # | Decision | Choice |
|---|----------|--------|
| 7.1 | Prefab rule | **Atomic elements are prefabs by default** (self-contained + bind surface), even single-use. Screen skeletons stay inline. Exception by fiat: the detail stat band (`StatRow`/`StatCell`) is inline — no reuse expected |
| 7.2 | Inventory | `LetterpressButton` (+variants Accent/Quiet/Icon), `SectionHeader`, `GameCard`, `ArchiveRow`, `HowToPlayBlock` (detail **and** pause sheet — one prefab, two hosts), `SheetFrame` (overlay chrome), plus invisibles: `DropCap`, theme binders, `SafeAreaPanel`, `GridCellSizeDriver` |
| 7.3 | Button | **`Selectable` subclass** (own `onClick`, `IPointerClickHandler`/`ISubmitHandler`, interactable gating — EventSystem still gives slide-off-cancel free). Plate/Face structure; press = Face translates ~2 units onto Plate via DOTween ~80 ms; disabled = CanvasGroup alpha fade. `Navigation = None` |
| 7.4 | Hit targets | **Dedicated stationary `HitTarget` element** per button (≥44×44, never animated, invisible Graphic). Global rule: *nothing raycasts except things named HitTarget* — every decorative Graphic sets `raycastTarget = false` |
| 7.5 | Played art | Alpha fade **on the art image only** — tick badge stays full-opacity. Greyscale shader later ([#571](https://github.com/callum-rose/Assembler/issues/571)) |
| 7.6 | Card thumbs | Deterministic generated placeholder art (seeded from game id, letterpress motifs) renders instantly and doubles as the loading state; real thumbnails replace it when GameInfo lands ([#570](https://github.com/callum-rose/Assembler/issues/570)) |

### 8. Motion

| # | Decision | Choice |
|---|----------|--------|
| 8.1 | Library | **DOTween** is the app-wide animation backbone |
| 8.2 | Rule 1 | Every tween `SetLink(gameObject)` kill-on-disable — cached screens deactivate constantly; an unlinked tween completing invisibly corrupts the next `OnEnter` |
| 8.3 | Rule 2 | Overlay + strip tweens run unscaled (`SetUpdate(true)`) — they animate while `timeScale = 0` |
| 8.4 | Rule 3 | Durations/easings live in the theme's `Motion` block, never as literals |
| 8.5 | Await bridge | `ToAwaitable(this Tween, CancellationToken = default)` → engine-native `Awaitable` (Unity 6000.4.5f1). Resolves on **kill** (the one guaranteed terminal event — `OnComplete`-only deadlocks under rule 1). Cancellation: `TrySetCanceled` **then** `Kill()` → await throws `OperationCanceledException`; registration disposed in `onKill`; compose (don't replace) existing callbacks. Main-thread tokens only (`destroyCancellationToken`, `Application.exitCancellationToken`) |
| 8.6 | Set pieces | Result slip: verdict **stamp** (scale-slam + slight rotation) and **odometer** score count-up — digits wrapped in `<mspace>` during the count (TMP has no tabular-figures support) |

### 9. Data & persistence

| # | Decision | Choice |
|---|----------|--------|
| 9.1 | Store | **EasySave** behind an `IStatsStore` interface — the asset appears in exactly one compilation unit |
| 9.2 | Schema | One **versioned root object per domain** (`"stats"` dict of per-game records, `"settings"` block) — migrations transform one DTO, not a keyspace. Settings block reserved from day one (theme choice, dev-mode flag) |
| 9.3 | Write policy | Stats written **immediately** on game end (before any slip animation — a crash mid-animation must not lose the score) and on `OnApplicationPause` |
| 9.4 | Content model | Three tiers ([#570](https://github.com/callum-rose/Assembler/issues/570)): **manifest** (index + card-tier: `Title`, `PublishedAt`, `Channel`, `shortDesc`, `Version`) → **GameInfo** (full description, how-to-play, embedded thumbnail) → **descriptor** (pure game). All version-keyed caches |

### 10. Game session contract

| # | Decision | Choice |
|---|----------|--------|
| 10.1 | Handle | `IGameSession { event Action<GameResult> Ended; void Pause(); void Resume(); void Quit(); }` |
| 10.2 | Breaking change | **`!gameover` stops destroying the game root** and raises the end signal instead; teardown authority moves to the shell (`Quit()` is the only destroyer). Replaces today's `WaitUntil(gameRoot == null)` polling |
| 10.3 | Freeze | `Time.timeScale = 0` (confirmed safe for the runtime) + `AudioListener.pause = true`. Shell sounds, if ever, set `ignoreListenerPause` |
| 10.4 | End flow | `Ended` → freeze → **write stats** → slip animates over the frozen game (unscaled) → user picks → `Quit()` → navigate |
| 10.5 | Result data | No live score broadcast exists; result payload design is deferred ([#569](https://github.com/callum-rose/Assembler/issues/569)). v1 `GameResult` = game id + optional untyped payload; slip defaults to the no-score variant |
| 10.6 | Launch states | `Cached / Downloading / Parsing / Building / Failed` surface on the detail play button + the mono launch overlay (black, centred, progress bar) |
| 10.7 | Strip | Pause glyph (44-unit HitTarget) + game title in mono; right side reserved-empty. Contended real estate — validate against real games ([#573](https://github.com/callum-rose/Assembler/issues/573) for cutout-adjacent chrome) |

### 11. Editorial rules (feed/slip presenters)

| # | Decision | Choice |
|---|----------|--------|
| 11.1 | Ordering | Newest first by `PublishedAt`, feed and archive |
| 11.2 | Lead | **Newest visible game** — no live-only guard (only the developer sees staging entries) |
| 11.3 | Feed size | Lead + N (N in `ShellConfig` SO — editorial numbers are data, not code), then "Open the archive — N editions" |
| 11.4 | Staging gate | Dev mode = editor + development builds free, plus a hidden gesture in release (persisted flag, `DevModeService`) |
| 11.5 | Edition № | Count of visible games |
| 11.6 | Next game | Newest unplayed, excluding the one just played; absent when caught up. Routes to **detail**, never autoplay — "the detail page is where the controls are taught; a direct launch trades one tap for a confused first ten seconds in an unfamiliar game" |
| 11.7 | Hero states | PLAY stamp unplayed / tick + "Played today" played; button copy flips "Play today's" ↔ "Run it again" |
| 11.8 | Settings screen | Theme row, hidden dev row, about/folio. **No sound/haptics row** — v1 is primitive-assets only; nothing to turn down, add no dead slider |

---

## Build order

Each phase is shippable-reviewable on its own; later phases depend on earlier ones.

**Phase 1 — Foundations.**
Install EasyDI + DOTween + EasySave. Bootstrap: `ShellRoot` canvas (390×844, match shim, nested-canvas hosts, `SafeAreaPanel`s), authored EventSystem, application/shell scopes + `ShellInstaller`. Theme SO with colour roles + text styles + motion block; `ThemeColor`/`TextStyleBinder` (ExecuteAlways); static theme accessor; Newsreader TMP atlases; `ShellConfig`.

**Phase 2 — Primitives.**
`Tween.ToAwaitable(ct)` extension (kill-resolving, cancellation-throwing). `LetterpressButton` (Selectable subclass, Plate/Face/HitTarget, DOTween press, variants Accent/Quiet/Icon). `GridCellSizeDriver` (column-aware). `DropCapFormatter` + component. `SectionHeader`, `SheetFrame`, hairline/paper primitives. Raycast rule enforced from the first prefab.

**Phase 3 — Navigation.**
`ScreenCatalog` SO; navigator (stack, typed params, lazy+cache, `Awaitable` transitions, presenter creation via `Instantiate` with view argument); `OverlayHost` API. Empty screen prefabs (Feed/Detail/Archive/Settings) wired end-to-end — navigating between blank pages proves the shell before content exists.

**Phase 4 — Model.**
Demote `GameShelf`: extract the fetch/cache/parse/guard/build flow into a service; delete the procedural UI. `IStatsStore` + EasySave implementation (versioned root objects, settings block). Manifest v2 parsing (new fields optional-tolerant). View records + `CatalogChanged`/`StatsChanged`.

**Phase 5 — Screens.**
Feed (hero with drop cap + stamp, grid, folio, archive button) → Detail (kicker, description, `HowToPlayBlock`, inline stat band, play-button states) → Archive (search + rows) → Settings (theme/dev/about). Placeholder art generator ships here (7.6).

**Phase 6 — Game integration.**
`IGameSession`; the `!gameover` signal change (10.2); launch overlay; `GameStrip`; pause sheet (freeze + `HowToPlayBlock` reuse); result slip (stamp, `<mspace>` odometer, no-score default, next-game routing). Stats write-on-end.

**Phase 7 — Content pipeline.**
GameInfo split ([#570](https://github.com/callum-rose/Assembler/issues/570)): publish tooling, descriptor slimming, client cache, embedded thumbs replacing placeholders progressively.

---

## Deferred ledger

| Issue | What | Unblocks |
|-------|------|----------|
| [#569](https://github.com/callum-rose/Assembler/issues/569) | `!gameover` result payload (`GameResult` contract) | Real scores on the slip; best-score stats |
| [#570](https://github.com/callum-rose/Assembler/issues/570) | GameInfo split + embedded thumbnails | Real art; detail before download; slim descriptors |
| [#571](https://github.com/callum-rose/Assembler/issues/571) | Greyscale shader for played art | Print-faithful played treatment |
| [#572](https://github.com/callum-rose/Assembler/issues/572) | Tablet scale clamp + multi-column grid | Tablet support |
| [#573](https://github.com/callum-rose/Assembler/issues/573) | In-game chrome beside notches (`Screen.cutouts`) | Reclaimed vertical space in play |
| [#574](https://github.com/callum-rose/Assembler/issues/574) | Archive virtualisation | Catalogue at hundreds of entries |
| [#575](https://github.com/callum-rose/Assembler/issues/575) | Dark mode theme asset + follow-OS | Round-3 palette ships |
| [#576](https://github.com/callum-rose/Assembler/issues/576) | Streaks (stats v2) | Roadmap stat + stat-band cell |
| [#577](https://github.com/callum-rose/Assembler/issues/577) | Localisation | Post-v1, if ever |
| [EasyDI#41](https://github.com/callum-rose/EasyDI/issues/41) | User-definable lifetime scope layers | Named App→Shell→Game scopes (workaround: plain `CreateChild`) |
