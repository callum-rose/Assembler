# Behaviours

Generated from `Assembler.Behaviours` XML doc comments. Each behaviour's description, property meanings, and trigger outputs are authored on the corresponding `GameBehaviour` MonoBehaviour; property names and types are reflected from the matching `*Info` record.

## `box collider`
Adds a Unity BoxCollider to the entity, sized to Size. Required for collision/trigger physics events.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Size | Vector3 | Local-space dimensions of the box (x, y, z). |
| IsTrigger | bool | When true the collider fires trigger events (no physical collision response) instead of acting as a solid collider. |

## `sphere collider`
Adds a Unity SphereCollider to the entity. Required for collision/trigger physics events.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Radius | float | Local-space radius of the sphere. |
| IsTrigger | bool | When true the collider fires trigger events (no physical collision response) instead of acting as a solid collider. |

## `rigidbody`
Adds a Unity Rigidbody to the entity so it participates in physics simulation.

### Properties

| Name | Type | Description |
|------|------|-------------|
| UseGravity | bool | When true the rigidbody is affected by gravity. |

## `velocity`
Moves the entity each frame by Velocity * deltaTime.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Velocity | Vector3 | World-space velocity in units per second. |

## `translate`
Adds Displacement to the entity's world position each time it Executes (e.g. via a trigger).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Displacement | Vector3 | World-space offset to add on each execution. |

## `key hold trigger`
Fires every frame while the named key is held down.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Key | string | One of "w", "a", "s", "d", "up", "down", "left", "right". |

## `key down trigger`
Fires on the frame the named key is pressed down.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Key | string | KeyCode name to listen for (e.g. "Space", "W", "Mouse0"). |

## `key up trigger`
Fires on the frame the named key is released.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Key | string | KeyCode name to listen for (e.g. "Space", "W", "Mouse0"). |

## `tap trigger`
Fires once on a quick screen tap gesture (touch or click).

No properties.

## `double tap trigger`
Fires on a double-tap gesture (two quick taps in succession).

No properties.

## `long press trigger`
Fires on a long-press gesture (touch held without movement for a threshold time).

No properties.

## `swipe trigger`
Fires on a swipe gesture (touch dragged across the screen).

No properties.

## `drag trigger`
Fires while a drag gesture is active (touch held and moved).

No properties.

## `pinch trigger`
Fires on a two-finger pinch gesture (zoom in/out).

No properties.

## `rotate trigger`
Fires on a two-finger rotate gesture.

No properties.

## `timer trigger`
Fires once after a delay (starts the countdown on entity start, or on Execute).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Delay | float | Seconds to wait before notifying listeners. |

## `deferred trigger`
Forwards a trigger event to listeners after a delay. Insert between an upstream trigger and downstream behaviours to defer execution.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Delay | float | Seconds to wait between Execute and notifying listeners. |

## `on start trigger`
Fires once when the entity is first started.

No properties.

## `interval trigger`
Fires repeatedly at a fixed interval. Optionally limited to a number of repetitions.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Interval | float | Seconds between fires. |
| Count | int | Number of times to fire; 0 means fire forever. |
| AutoStart | bool | When true the timer starts on entity start; when false it waits for an Execute call from upstream. |

## `every frame trigger`
Fires every Unity Update frame. Use for behaviours that must run continuously.

No properties.

## `collision enter trigger`
Fires when a non-trigger collision begins with another entity matching TagsToDetect. Requires colliders + a Rigidbody.

### Properties

| Name | Type | Description |
|------|------|-------------|
| TagsToDetect | IReadOnlyList<string> | Only fire when the other entity has at least one of these tags. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| contact_point | Vector3 | World-space point of first contact. |
| contact_normal | Vector3 | Surface normal at the contact point. |
| other_velocity | Vector3 | Other body's linear velocity (zero if it has no Rigidbody). |
| other_position | Vector3 | Other entity's world position at the moment of collision. |

## `trigger enter trigger`
Fires when an entity matching TagsToDetect enters this entity's trigger collider.

### Properties

| Name | Type | Description |
|------|------|-------------|
| TagsToDetect | IReadOnlyList<string> | Only fire when the other entity has at least one of these tags. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| other_position | Vector3 | Other entity's world position at the moment of entry. |

## `trigger exit trigger`
Fires when an entity matching TagsToDetect exits this entity's trigger collider.

### Properties

| Name | Type | Description |
|------|------|-------------|
| TagsToDetect | IReadOnlyList<string> | Only fire when the other entity has at least one of these tags. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| other_position | Vector3 | Other entity's world position at the moment of exit. |

## `collision exit trigger`
Fires when a non-trigger collision ends with another entity matching TagsToDetect.

### Properties

| Name | Type | Description |
|------|------|-------------|
| TagsToDetect | IReadOnlyList<string> | Only fire when the other entity has at least one of these tags. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| other_velocity | Vector3 | Other body's linear velocity at separation (zero if no Rigidbody). |
| other_position | Vector3 | Other entity's world position at separation. |

## `collision stay trigger`
Fires every physics frame while colliding with another entity matching TagsToDetect.

### Properties

| Name | Type | Description |
|------|------|-------------|
| TagsToDetect | IReadOnlyList<string> | Only fire when the other entity has at least one of these tags. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| contact_point | Vector3 | World-space point of contact for this frame. |
| contact_normal | Vector3 | Surface normal at the contact point. |
| other_velocity | Vector3 | Other body's linear velocity (zero if no Rigidbody). |
| other_position | Vector3 | Other entity's world position. |

## `spawner`
Spawns an instance of a named template at a position when Executed.

### Properties

| Name | Type | Description |
|------|------|-------------|
| TemplateId | string | Id of the template to instantiate. |
| Position | Vector3 | World-space position for the spawned entity. |
| Rotation | Vector3 | Euler rotation in degrees for the spawned entity. |
| Parameters | IReadOnlyDictionary<string, ValueSource<object>> | Optional name→value overrides forwarded to the template's parameter slots. |

## `destroy`
Destroys the entity's GameObject when Executed and notifies any listeners.

No properties.

## `position setter`
Sets the entity's world position to Position when Executed (typically via a trigger).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Position | Vector3 | World-space position to teleport the entity to on each execution. |

## `camera`
Adds a Unity Camera component to the entity; chooses orthographic vs perspective and sets size.

### Properties

| Name | Type | Description |
|------|------|-------------|
| View | string | "orthographic" for a 2D-style camera; any other value uses a perspective projection. |
| Size | float | Orthographic size in world units (only used when View = "orthographic"). |

## `condition gate`
Forwards an upstream trigger to listeners only when Condition evaluates to true at that moment.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Condition | bool | Boolean expression checked on each Execute call. |

## `vector variable setter`
Writes Value into the variable referenced by VariableId when Executed. For conditional assignment ("set X to A if cond else B"), use a single setter whose Value is an `!expr` with a ternary body (`cond ? A : B;`); the expression compiler supports ternaries (including nested) on every supported variable type, so there is no need to gate two setters behind a `condition gate`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | Vector3 | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | Vector3 | Source value to assign. Can be a constant, an `!expr` (including a ternary `cond ? A : B;` for conditional assignment), or another variable reference. |

## `int variable setter`
Writes Value into the variable referenced by VariableId when Executed. For conditional assignment ("set X to A if cond else B"), use a single setter whose Value is an `!expr` with a ternary body (`cond ? A : B;`); the expression compiler supports ternaries (including nested) on every supported variable type, so there is no need to gate two setters behind a `condition gate`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | int | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | int | Source value to assign. Can be a constant, an `!expr` (including a ternary `cond ? A : B;` for conditional assignment), or another variable reference. |

## `float variable setter`
Writes Value into the variable referenced by VariableId when Executed. For conditional assignment ("set X to A if cond else B"), use a single setter whose Value is an `!expr` with a ternary body (`cond ? A : B;`); the expression compiler supports ternaries (including nested) on every supported variable type, so there is no need to gate two setters behind a `condition gate`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | float | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | float | Source value to assign. Can be a constant, an `!expr` (including a ternary `cond ? A : B;` for conditional assignment), or another variable reference. |

## `bool variable setter`
Writes Value into the variable referenced by VariableId when Executed. For conditional assignment ("set X to A if cond else B"), use a single setter whose Value is an `!expr` with a ternary body (`cond ? A : B;`); the expression compiler supports ternaries (including nested) on every supported variable type, so there is no need to gate two setters behind a `condition gate`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | bool | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | bool | Source value to assign. Can be a constant, an `!expr` (including a ternary `cond ? A : B;` for conditional assignment), or another variable reference. |

## `string variable setter`
Writes Value into the variable referenced by VariableId when Executed. For conditional assignment ("set X to A if cond else B"), use a single setter whose Value is an `!expr` with a ternary body (`cond ? A : B;`); the expression compiler supports ternaries (including nested) on every supported variable type, so there is no need to gate two setters behind a `condition gate`.

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | string | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | string | Source value to assign. Can be a constant, an `!expr` (including a ternary `cond ? A : B;` for conditional assignment), or another variable reference. |

## `vector list add`
Appends Value to the end of List when Executed.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<Vector3> | Reference to the target list variable. |
| Value | Vector3 | Item to append. |

## `vector list remove at`
Removes the item at Index from List when Executed. No-op if Index is out of range.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<Vector3> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `vector list set at`
Overwrites the item at Index in List with Value when Executed. No-op if Index is out of range.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<Vector3> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | Vector3 | New item. |

## `vector list clear`
Removes all items from List when Executed.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<Vector3> | Reference to the target list variable. |

## `int list add`
Appends Value to the end of List when Executed.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<int> | Reference to the target list variable. |
| Value | int | Item to append. |

## `int list remove at`
Removes the item at Index from List when Executed. No-op if Index is out of range.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<int> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `int list set at`
Overwrites the item at Index in List with Value when Executed. No-op if Index is out of range.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<int> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | int | New item. |

## `int list clear`
Removes all items from List when Executed.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<int> | Reference to the target list variable. |

## `float list add`
Appends Value to the end of List when Executed.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<float> | Reference to the target list variable. |
| Value | float | Item to append. |

## `float list remove at`
Removes the item at Index from List when Executed. No-op if Index is out of range.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<float> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `float list set at`
Overwrites the item at Index in List with Value when Executed. No-op if Index is out of range.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<float> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | float | New item. |

## `float list clear`
Removes all items from List when Executed.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<float> | Reference to the target list variable. |

## `bool list add`
Appends Value to the end of List when Executed.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<bool> | Reference to the target list variable. |
| Value | bool | Item to append. |

## `bool list remove at`
Removes the item at Index from List when Executed. No-op if Index is out of range.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<bool> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `bool list set at`
Overwrites the item at Index in List with Value when Executed. No-op if Index is out of range.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<bool> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | bool | New item. |

## `bool list clear`
Removes all items from List when Executed.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<bool> | Reference to the target list variable. |

## `string list add`
Appends Value to the end of List when Executed.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<string> | Reference to the target list variable. |
| Value | string | Item to append. |

## `string list remove at`
Removes the item at Index from List when Executed. No-op if Index is out of range.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<string> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `string list set at`
Overwrites the item at Index in List with Value when Executed. No-op if Index is out of range.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<string> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | string | New item. |

## `string list clear`
Removes all items from List when Executed.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<string> | Reference to the target list variable. |

## `colour list add`
Appends Value to the end of List when Executed.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<Color> | Reference to the target list variable. |
| Value | Color | Item to append. |

## `colour list remove at`
Removes the item at Index from List when Executed. No-op if Index is out of range.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<Color> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `colour list set at`
Overwrites the item at Index in List with Value when Executed. No-op if Index is out of range.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<Color> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | Color | New item. |

## `colour list clear`
Removes all items from List when Executed.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | IList<Color> | Reference to the target list variable. |

## `sprite`
Renders a 2D sprite as a child of the entity, optionally rescaled to Size.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Sprite | Sprite | Asset reference to the sprite to display. |
| Size | Vector2 | Target world-space size in units; the sprite is scaled to fit. |

## `audio source`
Plays an audio clip when Executed (or on start, if configured).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Clip | AudioClip | Asset reference to the audio clip to play. |
| PlayOnStart | bool | When true the clip plays automatically when the entity is created. |
| Loop | bool | When true the clip loops once started. |

## `sphere gizmo`
Debug-draws a sphere gizmo at the entity's position in the Scene view.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Radius | float | Sphere radius in world units. |
| IsWire | bool | When true draws an outline; when false draws a filled sphere. |
| Colour | Color | Gizmo colour. |

## `cube gizmo`
Debug-draws a cube gizmo at the entity's position in the Scene view.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Size | Vector3 | Cube dimensions in world units. |
| IsWire | bool | When true draws an outline; when false draws a filled cube. |
| Colour | Color | Gizmo colour. |

## `line gizmo`
Debug-draws a line gizmo between two points in the entity's local transform space.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Start | Vector3 | Line start point in local transform space. |
| End | Vector3 | Line end point in local transform space. |
| Colour | Color | Gizmo colour. |

## `text label`
Draws a text label on-screen using IMGUI. Useful for debug HUD and scoreboards.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Text | string | Dynamic body text (re-read each frame; bind to a variable to display live values). |
| Label | string | Optional static prefix shown before Text (e.g. "Score: "). |
| FontSize | int | Font size in pixels. |
| Rect | ScreenRect | Screen-space rectangle (see ScreenRect format). |

## `progress bar`
Draws a horizontal fill bar; the filled fraction is Value clamped to [0, 1].

### Properties

| Name | Type | Description |
|------|------|-------------|
| Value | float | Progress in [0, 1]. Re-read each frame. |
| Rect | ScreenRect | Screen-space rectangle. |

## `ui image`
Draws a solid-coloured rectangle on-screen. Useful as a simple HUD backdrop or indicator.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Colour | Color | Fill colour. |
| Rect | ScreenRect | Screen-space rectangle. |

## `ui button`
Draws a button. Acts as a trigger: notifies listeners each time the button is clicked.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Label | string | Button caption. |
| Rect | ScreenRect | Screen-space rectangle. |

## `ui toggle`
Draws a toggle (checkbox). Fires listeners whenever the toggle's state changes.

### Properties

| Name | Type | Description |
|------|------|-------------|
| InitialValue | bool | Starting checked/unchecked state. |
| Label | string | Caption shown next to the toggle. |
| Rect | ScreenRect | Screen-space rectangle. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| value | bool | The new toggle state after the change. |

## `ui slider`
Draws a horizontal slider. Fires listeners whenever the slider value changes.

### Properties

| Name | Type | Description |
|------|------|-------------|
| InitialValue | float | Starting slider value. |
| MinValue | float | Minimum value the slider can produce. |
| MaxValue | float | Maximum value the slider can produce. |
| Rect | ScreenRect | Screen-space rectangle. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| value | float | The new slider value after the change. |

## `ui input field`
Draws a text input field. Fires listeners when the user presses Enter to submit the typed text.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Rect | ScreenRect | Screen-space rectangle. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| text | string | The submitted text. The field is cleared after submission. |

---

## Doc-gen warnings

- `move animation`: no MonoBehaviour mapping for `MoveAnimationInfo` (skipped).
- `scale animation`: no MonoBehaviour mapping for `ScaleAnimationInfo` (skipped).
- `rotate animation`: no MonoBehaviour mapping for `RotateAnimationInfo` (skipped).
- `condition`: no MonoBehaviour mapping for `ConditionInfo` (skipped).
- `trigger stay trigger`: no MonoBehaviour mapping for `TriggerStayTriggerInfo` (skipped).
- `when all`: no MonoBehaviour mapping for `WhenAllInfo` (skipped).
- `when any`: no MonoBehaviour mapping for `WhenAnyInfo` (skipped).
