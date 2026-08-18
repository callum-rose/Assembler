# UI System (composable uGUI blocks)

UI is built from regular behaviours in this directory (`Assembler.Behaviours.UI`), composed via the entity hierarchy — there is no separate UI assembly, and the old IMGUI/`ScreenRect` widgets have been removed.

The blocks:

- **`ui canvas`** — roots a UI tree with a screen-space `Canvas` + `CanvasScaler` (ScaleWithScreenSize) + `GraphicRaycaster`. Child UI entities compose the interface.
- **`ui container`** — groups child UI entities, auto-laying them out with a vertical/horizontal uGUI layout group (or `Direction: none` for manual positioning), driven by `PreferredWidth`/`PreferredHeight`.
- **`text label`** — a TextMeshPro label; `Text` is re-read every frame, so binding it to a variable/expression shows live values.
- **`ui button`** — a clickable button that acts as a trigger (`NotifyListeners` on click); `Label` is re-read each frame.
- **`ui slider`** — a slider that acts as a trigger, emitting a `value` output whenever it changes.

Prefabs come from a `UiPrefabLibrary` ScriptableObject loaded from `Resources/UI/UiPrefabLibrary`, with typed view components (`UiButtonView`/`UiLabelView`/`UiSliderView` in `Views/`). The `Assembler > UI > Generate UI Prefabs` editor menu regenerates baseline prefabs; restyle them without code changes.

`Builder` bootstraps a single `EventSystem` with `InputSystemUIInputModule` and threads the loaded library through `BehaviourBuildContext`. `GameEntityFactory` pins child sibling order to descriptor order for deterministic layout.

All input goes through the new Input System: `input action` reads `Controls` bindings, and the touch-gesture triggers read it via `Triggers/Input/Touch/Pointer`. No runtime code uses the legacy `UnityEngine.Input` manager anymore, so `activeInputHandler` could be switched from `2` (both) to `1` (Input System only) — left as a deliberate project-setting toggle.

See `Assets/ExampleGameDescriptors/UiDemo.yaml` and `UiShowcase.yaml` for usage.
