# Assembler.Shell

The app shell — the newspaper the games are published in. Everything outside a running game lives
here: the canvas the app draws into, the theme it wears, and the composition root the rest hangs
from. The full design is [`Assets/docs/UIPLAN.md`](../docs/UIPLAN.md); this README covers what is
built so far (phases 1–2: foundations and primitives) and the traps in it.

## What is here

| Piece | What it does |
| --- | --- |
| `ShellRoot` | The one screen-space-overlay canvas, plus its three layers |
| `ShellHost` | One layer: a full-bleed rect with its own nested canvas and a safe-area child |
| `Layout/ShortAxisCanvasScaler` | Keeps the canvas's short axis at 390 units in either orientation |
| `Layout/SafeAreaPanel` | Anchors a rect to `Screen.safeArea` |
| `Theming/ShellTheme` | Colour roles, typographic scale, motion timings, layout measurements |
| `Theming/ScriptableEnum` | Base for the asset-backed enums — one asset per `ColorRole` / `TextStyleId` member |
| `Theming/ThemeService` + `Theme` | The theme in force, via DI and via a deliberately narrow static accessor |
| `Theming/Binders/*` | `ThemeColor` and `TextStyleBinder` — the components that paint from a role |
| `Composition/*` | The EasyDI scope chain and the shell's installer |
| `ShellConfig` | Editorial numbers (how long the feed runs) |
| `Motion/TweenExtensions` | The two rules every shell tween carries, and the bridge from DOTween to `Awaitable` |
| `Controls/HitTarget` | The only thing in the shell a pointer may hit |
| `Controls/LetterpressButton` | Face, plate and ledge; the press that consumes the ledge |
| `Controls/Rule` | A horizontal rule at one of the newspaper's three weights |
| `Controls/SectionHeader` | The rubric that opens a section, and its double rule |
| `Controls/SheetFrame` | Overlay chrome: scrim, rising surface, content slot |
| `Layout/GridCellSizeDriver` | The feed grid's column arithmetic |
| `Typography/DropCapFormatter` + `DropCap` | The drop cap, as arithmetic and as a component |
| `Prefabs/*` | The primitives above, authored (see **Primitives**) |

## Coordinates

The canvas is **390 × 844 units, matched on the short axis**. That is the prototype's viewport in CSS
pixels, so every number in `Prototypes/app-look-prototype.html` transfers 1:1 — a 16px gutter is 16
units, a 15px body size is a 15-unit font size. It is a *logical* coordinate system, not a render
resolution: text is SDF and geometry rasterises at native device pixels. Bitmap art is the exception
and must be authored at 3–4× its unit size (a 44-unit icon ships at ≥132px) or it blurs on device.

## Theming

Nothing hard-codes a colour, a font size or a tween duration. A graphic carries a `ThemeColor` naming
a `ColorRole`; a label carries a `TextStyleBinder` naming a `TextStyleId`; a tween reads a `MotionSpec`
off the theme. Dark mode is then a second `ShellTheme` asset rather than a pass over every prefab.

**`ColorRole` and `TextStyleId` are assets, not C# enums.** One asset per member, under `Theming/Roles`
and `Theming/TextStyles`, and a prefab binds one by GUID — so renaming a member, reordering the folder
or deleting one in the middle cannot silently repaint the app, and adding a role is a new asset plus a
row on the theme rather than an edit to an enum and a re-check of every prefab that serialised a number.
The inspector still draws them as a dropdown (`Editor/ScriptableEnumDrawer`), so picking one feels the
same as picking an enum member. What each member is *for* is authored on the asset itself, in its
`description`, which the dropdown shows as the tooltip.

Three things to know:

- **A binder starts out bound to nothing.** The enum version defaulted to `Ink` / `Body`; a reference
  field cannot, so a freshly added `ThemeColor` or `TextStyleBinder` paints nothing at all until a
  member is picked. That is deliberate — it leaves the authored colour alone while you wire the object
  up, rather than flooding the scene with magenta and a warning per repaint.
- **A label wants `TextStyleBinder` *instead of* `ThemeColor`, not as well.** A text style already
  names a colour role, and `TMP_Text` is a `Graphic`, so both components would fight over the colour.
- **`Theme` is a static accessor, and that is on purpose.** Binders run under `[ExecuteAlways]` in the
  editor, where no scope exists and a `MonoBehaviour` has no constructor to inject through. It is for
  leaf binders only — anything with a constructor takes `IThemeService` from DI. When nothing has
  bound a service, it falls back to `Resources/Shell/ShellTheme`.

## Composition

The chain is **application → shell**, with a per-game-session scope to come.

`ApplicationLifetimeScope` lives on `ApplicationScope.prefab` and is named on `EasyDISettings.asset`;
EasyDI instantiates it into `DontDestroyOnLoad` before the first scene loads. `ShellLifetimeScope` is
authored in `Bootstrap.unity` and registers the shell's services through `ShellInstaller`.

The shell scope overrides `DoParentTransformToParentScope` to `false`. By default a scope reparents
onto its parent, which would drag it out of `Bootstrap` and into `DontDestroyOnLoad` — leaving a scope
that outlives the scene objects its installer holds references to.

**EasyDI needs the settings asset to exist.** Its initialiser reads `EasyDISettings.RootLifetimeScope`
before every play and throws if there is no asset, so deleting `Assets/Shell/EasyDISettings.asset`
breaks play mode rather than merely disabling DI.

## Primitives

Ten prefabs under `Prefabs/`, all authored by `Assembler > Shell > Build Shell Prefabs`:
`HitTarget`, `Rule`, `PaperGround`, `LetterpressButton` and its `Accent` / `Quiet` / `Icon`
variants, `SectionHeader`, `SheetFrame` and `LeadParagraph`.

### Nothing raycasts except a HitTarget

The rule (UIPLAN 7.4) is that every tappable thing carries a dedicated, stationary, invisible
`HitTarget` of at least 44 units, and every decorative graphic sets `raycastTarget = false`. Two
things fall out of it: a touch area stops being an accident of how big the art happens to be, and a
pressed control can animate freely, because the thing being hit never moves. `Assembler > Shell >
Check Raycast Rule` walks the prefabs and reports every breach — worth running after adding one,
because a graphic left raycasting fails silently and the symptom ("the button sometimes doesn't
work") looks nothing like the cause.

`HitTarget` draws a fully transparent quad rather than nothing at all. A `CanvasRenderer` with no
geometry reports a depth of −1, and `GraphicRaycaster` skips those, so "draws nothing" and "is
hittable" are mutually exclusive in uGUI.

### The letterpress button

`Plate` is the ledge; `Face` is what moves; `Fill` sits inside the face inset by the outline width,
so painting face and fill different roles turns the button into an outlined one with no change of
structure — which is all the `Quiet` variant is. `Icon` drops the plate, and a button with no ledge
to consume sinks instead of travelling. All three are real Prefab Variants, so the base keeps driving
everything they do not override.

It subclasses `Selectable`, not `Button`: Button's transitions are the wrong shape for a press that
moves geometry, while `Selectable` still gives interactable gating and slide-off-cancel for free.
Unlike the theme binders it does **not** run under `ExecuteAlways` — the ledge inset is written from
the theme at enable, and doing that in edit mode would run DOTween outside play mode for no gain.

### Motion

Every shell tween goes through `SetShellDefaults(gameObject)`, which links it kill-on-disable and
runs it unscaled. The first matters because screens are cached rather than destroyed and so
deactivate constantly — an unlinked tween completing invisibly writes its end value over whatever the
next `OnEnter` just set up. The second matters because a paused game sets `timeScale = 0` and the
chrome drawn over it still has to move.

`tween.ToAwaitable(ct)` resolves on **kill**, never on complete. Kill is the one terminal event
DOTween guarantees: a tween cut short by that kill-on-disable link never completes at all, so
awaiting `OnComplete` would deadlock the moment a screen deactivated mid-fade.

**DOTween's uGUI shortcuts are invisible from this assembly.** `DOAnchorPos`, `DOFade` and the rest
ship as loose source under `Assets/Plugins`, so they compile into the default assembly. The two the
shell needs are rebuilt on `DOTween.To` in `TweenExtensions`; giving the plugin's Modules folder an
assembly definition would instead take those shortcuts away from everything that currently sees them.

### The drop cap

`DropCapFormatter` is the arithmetic and the two string edits; `DropCap` decides when to run them.

The cap's size is computed, never measured: its ink runs from the first line's cap line down to the
Nth line's baseline, and both are functions of the font's `FaceInfo` and the body's size and leading.
For the prototype's 15px/1.52 body over two lines that lands at 47.6 units — the prototype picked 47
by eye.

The indent takes exactly two passes. TextMeshPro's `<indent>` runs until it is closed, and the place
to close it is only known once the text has been laid out with the indent in force; but the closing
tag goes at the *start of the line below the cap*, so everything above the insertion is unchanged by
it and nothing needs re-measuring. Putting the tag anywhere else would make it a loop.

Two traps:

- **Write the paragraph through `DropCap.Text`, never `TMP_Text.SetText`.** `SetText` marks the
  label's input source as a pre-parsed buffer and TextMeshPro skips `ITextPreprocessor` for those, so
  a paragraph set that way silently loses its cap and its indent.
- **The cap carries no `TextStyleBinder`.** A binder would set its point size from the theme, and its
  size is computed — the two would fight, and which won would come down to component enable order.
  `DropCap` applies the named style itself and then overrides the size.

### Rects and `OnValidate`

Unity forbids resizing a `RectTransform` from inside `OnValidate`: the resize raises
`OnRectTransformDimensionsChange` through `SendMessage`, which is not allowed during validation, and
the console fills with a warning per rect instead. `Rule` and `GridCellSizeDriver` both re-lay-out on
validation, so both go through `Deferred.Run`.

## Regenerating

Five editor entry points, all under `Assembler > Shell` and all re-runnable:

| Menu item | What it does |
| --- | --- |
| `Build Shell Root` | Grows the shell into `Bootstrap.unity`. Additive — finds objects by name and configures them in place, never destroys |
| `Build Shell Prefabs` | Authors the primitives under `Prefabs/`. Opens an existing prefab and reconfigures it in place, so its GUID survives |
| `Create Shell Assets` | Creates the role and style members, the theme and the config if they are missing, tops the theme up with any table rows it lacks, and leaves existing ones alone |
| `Reset Shell Theme` | Rewrites the existing theme's palette and scale from the prototype, discarding hand-tuning |
| `Bake Newsreader Font Asset` | Re-bakes the static SDF atlas from the variable font |
| `Check Raycast Rule` | Reports every shell prefab graphic that raycasts and is not a `HitTarget` |

`Build Shell Prefabs` works in a scratch preview scene rather than in whatever the editor has open.
Building a new prefab needs a real GameObject, and every GameObject belongs to a scene — doing that
in the open scene would leave it marked dirty though nothing in it changed, and the editor would then
ask to save it at the next thing that cares, which in batch mode is a dialog nobody can answer.

They run headlessly too, which is how the scene was authored in the first place:

```bash
/Applications/Unity/Hub/Editor/6000.4.5f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -nographics -projectPath Assembler -executeMethod Assembler.Shell.Editor.ShellSceneBuilder.BuildShell
```

## Tests

`Assets/Tests/Shell` (EditMode) covers the drop cap's arithmetic and the feed grid's columns;
`Assets/Tests/ShellRuntime` (PlayMode) covers the tween-to-`Awaitable` bridge.

**The bridge's tests have to run in play mode.** DOTween only initialises once there is a running
player — in the editor it hands out tweens that can be created but never killed, so an edit-mode
version of those tests would assert against a library that is not actually working.

## Known gaps

- **Square corners on the sheet.** The prototype rounds the pause sheet's top corners at 16 units.
  uGUI needs a nine-sliced sprite for that and the shell carries no sprites yet, so `SheetFrame`
  ships square. Everything else in the letterpress look is a 2-unit radius, which at this scale is
  within noise.
- **The icon button's glyph is a placeholder.** The baked atlas carries Latin-1 and a newspaper's
  punctuation, so a real icon button either extends the character set or swaps the label for a
  graphic. That is the game strip's decision, in phase 6.
- **One font cut, not two.** UIPLAN 5.4 asks for Newsreader's display and text optical cuts. The repo
  carries only the variable font, and this TextMeshPro version cannot instance a variable font's axes —
  it bakes the default instance, and bold is TextMeshPro's synthesised bold. Importing the static
  `Display` and `Text` TTFs and baking one asset from each is the fix; nothing else changes, because
  every style names its own font asset.
- **The mono cut has not landed.** UIPLAN 5.7 gives in-game chrome a monospace voice. The PLAY stamp,
  which the prototype sets in mono, is currently serif.
- **The hosts are empty.** No paper ground, no chrome — screens (phase 5) bring their own. `Bootstrap`
  still runs `GameBootstrap`, so entering play mode there still builds a descriptor rather than the
  shell.
