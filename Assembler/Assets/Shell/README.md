# Assembler.Shell

The app shell — the newspaper the games are published in. Everything outside a running game lives
here: the canvas the app draws into, the theme it wears, and the composition root the rest hangs
from. The full design is [`Assets/docs/UIPLAN.md`](../docs/UIPLAN.md); this README covers what is
built so far (phase 1, foundations) and the traps in it.

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

## Regenerating

Three editor entry points, all under `Assembler > Shell` and all re-runnable:

| Menu item | What it does |
| --- | --- |
| `Build Shell Root` | Grows the shell into `Bootstrap.unity`. Additive — finds objects by name and configures them in place, never destroys |
| `Create Shell Assets` | Creates the role and style members, the theme and the config if they are missing, and leaves existing ones alone |
| `Reset Shell Theme` | Rewrites the existing theme's palette and scale from the prototype, discarding hand-tuning |
| `Bake Newsreader Font Asset` | Re-bakes the static SDF atlas from the variable font |

They run headlessly too, which is how the scene was authored in the first place:

```bash
/Applications/Unity/Hub/Editor/6000.4.5f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -nographics -projectPath Assembler -executeMethod Assembler.Shell.Editor.ShellSceneBuilder.BuildShell
```

## Known gaps

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
