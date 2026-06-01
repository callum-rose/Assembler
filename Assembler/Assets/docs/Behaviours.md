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

## `capsule collider`
Adds a Unity CapsuleCollider to the entity. Required for collision/trigger physics events.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Radius | float | Local-space radius of the capsule hemispheres. |
| Height | float | Local-space total height of the capsule along its Direction axis. |
| Direction | int | Axis the capsule is aligned to — 0 = X, 1 = Y, 2 = Z. |
| IsTrigger | bool | When true the collider fires trigger events instead of acting as a solid collider. |

## `mesh collider`
Adds a Unity MeshCollider to the entity using the mesh from the entity's MeshFilter. Required for collision/trigger physics events on arbitrary meshes (e.g. voxel meshes).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Convex | bool | When true the collider uses a convex hull (required for non-kinematic Rigidbodies and trigger volumes). |
| IsTrigger | bool | When true the collider fires trigger events instead of acting as a solid collider (requires Convex = true). |

## `rigidbody`
Adds a Unity Rigidbody to the entity so it participates in physics simulation.

### Properties

| Name | Type | Description |
|------|------|-------------|
| UseGravity | bool | When true the rigidbody is affected by gravity. |
| IsKinematic | bool | When true the rigidbody ignores forces and is moved only by transform writes. |
| Mass | float | Mass of the rigidbody in kilograms. |
| LinearDamping | float | Damping applied to linear velocity (Unity's Rigidbody.linearDamping / drag). |
| AngularDamping | float | Damping applied to angular velocity (Unity's Rigidbody.angularDamping). |
| FreezePosition | Vector3 | Per-axis position freeze (any non-zero component locks that axis, e.g. (1, 0, 1) freezes X and Z). |
| FreezeRotation | Vector3 | Per-axis rotation freeze (any non-zero component locks that axis). |

## `add force`
Adds a continuous world-space force to the entity's Rigidbody when Executed (typically via a trigger).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Force | Vector3 | World-space force vector applied with ForceMode.Force (mass-dependent, frame-rate independent acceleration). |

## `add impulse`
Adds an instantaneous world-space impulse to the entity's Rigidbody when Executed (typically via a trigger).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Impulse | Vector3 | World-space impulse applied with ForceMode.Impulse (mass-dependent, instantaneous velocity change). |

## `add torque`
Adds a continuous world-space torque to the entity's Rigidbody when Executed (typically via a trigger).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Torque | Vector3 | World-space torque vector applied with ForceMode.Force (mass-dependent angular acceleration). |

## `set velocity`
Sets the entity's Rigidbody linear velocity to Velocity when Executed (typically via a trigger).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Velocity | Vector3 | World-space linear velocity in units per second. |

## `set angular velocity`
Sets the entity's Rigidbody angular velocity to AngularVelocity when Executed (typically via a trigger).

### Properties

| Name | Type | Description |
|------|------|-------------|
| AngularVelocity | Vector3 | World-space angular velocity in radians per second around each axis. |

## `velocity`
Moves the entity each frame by Velocity * deltaTime.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Velocity | Vector3 | World-space velocity in units per second. |

## `acceleration`
Integrates Acceleration into an internal velocity each frame, then moves the entity by that velocity.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Acceleration | Vector3 | World-space acceleration in units per second squared. |

## `translate`
Adds Displacement to the entity's world position each time it Executes (e.g. via a trigger).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Displacement | Vector3 | World-space offset to add on each execution. |

## `angular velocity`
Rotates the entity each frame by AngularVelocity * deltaTime (Euler degrees per second).

### Properties

| Name | Type | Description |
|------|------|-------------|
| AngularVelocity | Vector3 | World-space angular velocity in degrees per second (Euler per axis). |

## `rotate`
Adds Displacement (Euler degrees) to the entity's world rotation each time it Executes (e.g. via a trigger).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Displacement | Vector3 | World-space Euler angle offset (degrees) to add on each execution. |

## `rotation setter`
Sets the entity's world rotation to Rotation (Euler degrees) when Executed (typically via a trigger).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Rotation | Vector3 | World-space Euler angles (degrees) to set the entity's rotation to on each execution. |

## `move animation`
Tweens the entity's world position from Start to End over Duration. See TransformAnimation.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Start | Vector3 | Value to animate from. Falls back to the current transform value when unset. |
| End | Vector3 | Value to animate to. |
| Duration | float | Animation length in seconds (clamped to a minimum of 0). |
| Easing | string | Name of the DOTween ease to apply (e.g. "linear", "inOutSine"). Defaults to InOutSine. |

## `scale animation`
Tweens the entity's local scale from Start to End over Duration. See TransformAnimation.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Start | Vector3 | Value to animate from. Falls back to the current transform value when unset. |
| End | Vector3 | Value to animate to. |
| Duration | float | Animation length in seconds (clamped to a minimum of 0). |
| Easing | string | Name of the DOTween ease to apply (e.g. "linear", "inOutSine"). Defaults to InOutSine. |

## `rotate animation`
Tweens the entity's euler angles from Start to End over Duration. See TransformAnimation.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Start | Vector3 | Value to animate from. Falls back to the current transform value when unset. |
| End | Vector3 | Value to animate to. |
| Duration | float | Animation length in seconds (clamped to a minimum of 0). |
| Easing | string | Name of the DOTween ease to apply (e.g. "linear", "inOutSine"). Defaults to InOutSine. |

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

## `mouse button trigger`
Fires on a mouse button event during the selected phase (press, release, or hold).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Button | int | Mouse button index — 0 (left), 1 (right), 2 (middle). |
| Phase | string | When to fire — "down" (press only), "up" (release only), or "hold" (every frame held). Defaults to "down". |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| mouse_position | Vector3 | Screen-space mouse position when the trigger fires. |

## `mouse position trigger`
Fires every frame the mouse moves, publishing the current position and frame delta.

No properties.

### Outputs

| Name | Type | Description |
|------|------|-------------|
| mouse_position | Vector3 | Current screen-space mouse position. |
| mouse_delta | Vector3 | Screen-space movement since the previous frame. |

## `scroll wheel trigger`
Fires on frames where the mouse scroll wheel moved.

No properties.

### Outputs

| Name | Type | Description |
|------|------|-------------|
| scroll_delta | Vector2 | Scroll wheel delta for this frame (y is the common vertical scroll). |

## `axis trigger`
Fires every frame with the current value(s) of one or two Unity input axes (1D or 2D).

### Properties

| Name | Type | Description |
|------|------|-------------|
| XAxis | string | Name of the Unity input axis read into the x component (e.g. "Horizontal"). |
| YAxis | string | Optional. Name of the Unity input axis read into the y component (e.g. "Vertical"). Leave unset for a 1D axis. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| axis | Vector2 | Combined (x, y) axis value; y is 0 when YAxis is unset. |
| x | float | Current XAxis value. |
| y | float | Current YAxis value, or 0 when YAxis is unset. |

## `input action`
Relays an abstract input action (declared in the descriptor's Controls section and bound to a physical input per platform) to listeners. A drop-in replacement for the raw key triggers: a button action behaves like the key hold/down/up triggers depending on its phase, and a value action behaves like the axis trigger, emitting axis/x/y every frame.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Action | string | Name of the abstract action to listen for (must be declared under Controls.Actions). |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| axis | Vector2 | For value actions, the current (x, y) value of the action each frame. |
| x | float | For value actions, the current x component. |
| y | float | For value actions, the current y component. |

## `gamepad button trigger`
Fires on a gamepad / joystick button event (press, release, or hold).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Button | string | Unity key string for the gamepad button (e.g. "joystick button 0", "joystick 1 button 1"). |
| Mode | string | When to fire — "down" (press only), "up" (release only), or "hold" (every frame held). Defaults to "down". |

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

## `debounced trigger`
Forwards a trigger event only when no prior trigger has been received within the last Interval seconds. Use to suppress rapid repeat triggers.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Interval | float | Seconds that must elapse since the previous incoming trigger before another one is forwarded. |

## `throttled trigger`
Forwards at most Rate trigger events per second. Incoming triggers that arrive sooner than 1/Rate seconds after the previous forwarded one are dropped.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Rate | float | Maximum number of forwarded triggers per second. |

## `on start trigger`
Fires once when the entity is first started.

No properties.

## `interval trigger`
Fires repeatedly at an interval. Optionally limited to a number of repetitions.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Interval | float | Seconds between fires. Re-read before each wait, so binding it to a variable that other |
| Count | int | Number of times to fire; 0 means fire forever. Re-read each iteration, so a variable-bound |
| AutoStart | bool | When true the timer starts on entity start; when false it waits for an Execute call from upstream. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| iteration_index | int | Zero-based index of the current fire (0 on the first fire, 1 on the second, etc.). |
| iteration_count | int | Total number of fires configured by Count; 0 when the trigger is unbounded. |

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

## `inverse condition gate`
Forwards an upstream trigger to listeners only when Condition evaluates to false at that moment.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Condition | bool | Boolean expression checked on each Execute call; listeners fire when it is false. |

## `exclusive trigger`
Forwards an upstream trigger to listeners only if no other trigger sharing the same Group has already fired this frame.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Group | string | Name identifying the exclusion group; only the first trigger in this group to fire each frame propagates. |

## `vector variable setter`
Writes a Vector3 value into the referenced variable when Executed. See VariableSetterBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | Vector3 | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | Vector3 | Source value to assign. Can be a constant, expression, or another variable reference. |

## `int variable setter`
Writes a int value into the referenced variable when Executed. See VariableSetterBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | int | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | int | Source value to assign. Can be a constant, expression, or another variable reference. |

## `float variable setter`
Writes a float value into the referenced variable when Executed. See VariableSetterBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | float | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | float | Source value to assign. Can be a constant, expression, or another variable reference. |

## `bool variable setter`
Writes a bool value into the referenced variable when Executed. See VariableSetterBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | bool | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | bool | Source value to assign. Can be a constant, expression, or another variable reference. |

## `string variable setter`
Writes a string value into the referenced variable when Executed. See VariableSetterBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | string | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | string | Source value to assign. Can be a constant, expression, or another variable reference. |

## `colour variable setter`
Writes a Color value into the referenced variable when Executed. See VariableSetterBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | Color | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | Color | Source value to assign. Can be a constant, expression, or another variable reference. |

## `vector list add`
Appends a Vector3 value to the end of the target list when Executed. See ListAddBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> | Reference to the target list variable. |
| Value | Vector3 | Item to append. |

## `vector list insert`
Inserts a Vector3 value into the target list at a given index when Executed. See ListInsertBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> | Reference to the target list variable. |
| Index | int | Zero-based position to insert at. Valid range is [0, Count]. |
| Value | Vector3 | Item to insert. |

## `vector list remove at`
Removes the Vector3 item at a given index from the target list when Executed. See ListRemoveAtBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `vector list remove`
Removes the first occurrence of a Vector3 value from the target list when Executed. See ListRemoveBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> | Reference to the target list variable. |
| Value | Vector3 | Item to remove. |

## `vector list set at`
Overwrites the Vector3 item at a given index in the target list when Executed. See ListSetAtBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | Vector3 | New item. |

## `vector list set`
Replaces the entire contents of the target Vector3 list with another list when Executed. See ListSetBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> | Reference to the target list variable. |
| Value | List<Vector3> | List whose items replace List's contents (typically an expression returning a list). |

## `vector list add range`
Appends every item from another Vector3 list to the target list when Executed. See ListAddRangeBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> | Reference to the target list variable. |
| Other | List<Vector3> | List whose items will be appended to List. |

## `vector list clear`
Removes all items from the target Vector3 list when Executed. See ListClearBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> | Reference to the target list variable. |

## `vector list loop trigger`
Iterates a Vector3 list when Executed, firing listeners once per element. See ListLoopTrigger.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> | Reference to the list to iterate over. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| item | T | The current element of the list. |
| index | int | Zero-based position of the current element. |

## `int list add`
Appends a int value to the end of the target list when Executed. See ListAddBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> | Reference to the target list variable. |
| Value | int | Item to append. |

## `int list insert`
Inserts a int value into the target list at a given index when Executed. See ListInsertBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> | Reference to the target list variable. |
| Index | int | Zero-based position to insert at. Valid range is [0, Count]. |
| Value | int | Item to insert. |

## `int list remove at`
Removes the int item at a given index from the target list when Executed. See ListRemoveAtBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `int list remove`
Removes the first occurrence of a int value from the target list when Executed. See ListRemoveBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> | Reference to the target list variable. |
| Value | int | Item to remove. |

## `int list set at`
Overwrites the int item at a given index in the target list when Executed. See ListSetAtBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | int | New item. |

## `int list set`
Replaces the entire contents of the target int list with another list when Executed. See ListSetBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> | Reference to the target list variable. |
| Value | List<int> | List whose items replace List's contents (typically an expression returning a list). |

## `int list add range`
Appends every item from another int list to the target list when Executed. See ListAddRangeBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> | Reference to the target list variable. |
| Other | List<int> | List whose items will be appended to List. |

## `int list clear`
Removes all items from the target int list when Executed. See ListClearBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> | Reference to the target list variable. |

## `int list loop trigger`
Iterates a int list when Executed, firing listeners once per element. See ListLoopTrigger.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> | Reference to the list to iterate over. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| item | T | The current element of the list. |
| index | int | Zero-based position of the current element. |

## `float list add`
Appends a float value to the end of the target list when Executed. See ListAddBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> | Reference to the target list variable. |
| Value | float | Item to append. |

## `float list insert`
Inserts a float value into the target list at a given index when Executed. See ListInsertBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> | Reference to the target list variable. |
| Index | int | Zero-based position to insert at. Valid range is [0, Count]. |
| Value | float | Item to insert. |

## `float list remove at`
Removes the float item at a given index from the target list when Executed. See ListRemoveAtBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `float list remove`
Removes the first occurrence of a float value from the target list when Executed. See ListRemoveBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> | Reference to the target list variable. |
| Value | float | Item to remove. |

## `float list set at`
Overwrites the float item at a given index in the target list when Executed. See ListSetAtBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | float | New item. |

## `float list set`
Replaces the entire contents of the target float list with another list when Executed. See ListSetBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> | Reference to the target list variable. |
| Value | List<float> | List whose items replace List's contents (typically an expression returning a list). |

## `float list add range`
Appends every item from another float list to the target list when Executed. See ListAddRangeBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> | Reference to the target list variable. |
| Other | List<float> | List whose items will be appended to List. |

## `float list clear`
Removes all items from the target float list when Executed. See ListClearBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> | Reference to the target list variable. |

## `float list loop trigger`
Iterates a float list when Executed, firing listeners once per element. See ListLoopTrigger.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> | Reference to the list to iterate over. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| item | T | The current element of the list. |
| index | int | Zero-based position of the current element. |

## `bool list add`
Appends a bool value to the end of the target list when Executed. See ListAddBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> | Reference to the target list variable. |
| Value | bool | Item to append. |

## `bool list insert`
Inserts a bool value into the target list at a given index when Executed. See ListInsertBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> | Reference to the target list variable. |
| Index | int | Zero-based position to insert at. Valid range is [0, Count]. |
| Value | bool | Item to insert. |

## `bool list remove at`
Removes the bool item at a given index from the target list when Executed. See ListRemoveAtBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `bool list remove`
Removes the first occurrence of a bool value from the target list when Executed. See ListRemoveBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> | Reference to the target list variable. |
| Value | bool | Item to remove. |

## `bool list set at`
Overwrites the bool item at a given index in the target list when Executed. See ListSetAtBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | bool | New item. |

## `bool list set`
Replaces the entire contents of the target bool list with another list when Executed. See ListSetBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> | Reference to the target list variable. |
| Value | List<bool> | List whose items replace List's contents (typically an expression returning a list). |

## `bool list add range`
Appends every item from another bool list to the target list when Executed. See ListAddRangeBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> | Reference to the target list variable. |
| Other | List<bool> | List whose items will be appended to List. |

## `bool list clear`
Removes all items from the target bool list when Executed. See ListClearBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> | Reference to the target list variable. |

## `bool list loop trigger`
Iterates a bool list when Executed, firing listeners once per element. See ListLoopTrigger.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> | Reference to the list to iterate over. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| item | T | The current element of the list. |
| index | int | Zero-based position of the current element. |

## `string list add`
Appends a string value to the end of the target list when Executed. See ListAddBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> | Reference to the target list variable. |
| Value | string | Item to append. |

## `string list insert`
Inserts a string value into the target list at a given index when Executed. See ListInsertBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> | Reference to the target list variable. |
| Index | int | Zero-based position to insert at. Valid range is [0, Count]. |
| Value | string | Item to insert. |

## `string list remove at`
Removes the string item at a given index from the target list when Executed. See ListRemoveAtBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `string list remove`
Removes the first occurrence of a string value from the target list when Executed. See ListRemoveBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> | Reference to the target list variable. |
| Value | string | Item to remove. |

## `string list set at`
Overwrites the string item at a given index in the target list when Executed. See ListSetAtBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | string | New item. |

## `string list set`
Replaces the entire contents of the target string list with another list when Executed. See ListSetBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> | Reference to the target list variable. |
| Value | List<string> | List whose items replace List's contents (typically an expression returning a list). |

## `string list add range`
Appends every item from another string list to the target list when Executed. See ListAddRangeBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> | Reference to the target list variable. |
| Other | List<string> | List whose items will be appended to List. |

## `string list clear`
Removes all items from the target string list when Executed. See ListClearBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> | Reference to the target list variable. |

## `string list loop trigger`
Iterates a string list when Executed, firing listeners once per element. See ListLoopTrigger.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> | Reference to the list to iterate over. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| item | T | The current element of the list. |
| index | int | Zero-based position of the current element. |

## `colour list add`
Appends a Color value to the end of the target list when Executed. See ListAddBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the target list variable. |
| Value | Color | Item to append. |

## `colour list insert`
Inserts a Color value into the target list at a given index when Executed. See ListInsertBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the target list variable. |
| Index | int | Zero-based position to insert at. Valid range is [0, Count]. |
| Value | Color | Item to insert. |

## `colour list remove at`
Removes the Color item at a given index from the target list when Executed. See ListRemoveAtBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `colour list remove`
Removes the first occurrence of a Color value from the target list when Executed. See ListRemoveBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the target list variable. |
| Value | Color | Item to remove. |

## `colour list set at`
Overwrites the Color item at a given index in the target list when Executed. See ListSetAtBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | Color | New item. |

## `colour list set`
Replaces the entire contents of the target Color list with another list when Executed. See ListSetBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the target list variable. |
| Value | List<Color> | List whose items replace List's contents (typically an expression returning a list). |

## `colour list add range`
Appends every item from another Color list to the target list when Executed. See ListAddRangeBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the target list variable. |
| Other | List<Color> | List whose items will be appended to List. |

## `colour list clear`
Removes all items from the target Color list when Executed. See ListClearBehaviour.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the target list variable. |

## `colour list loop trigger`
Iterates a Color list when Executed, firing listeners once per element. See ListLoopTrigger.

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the list to iterate over. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| item | T | The current element of the list. |
| index | int | Zero-based position of the current element. |

## `active poll`
Polls a boolean value every frame and sets the entity GameObject's active state to match it.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Active | bool | Boolean (usually a variable or expression) re-read each frame; true keeps the entity active, false deactivates it. |

## `set active`
Sets the entity GameObject's active state to the Active value when Executed by an upstream trigger.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Active | bool | Boolean applied to the entity's active state on each Execute; true activates, false deactivates. |

## `set timescale`
Sets the game clock's time scale when Executed by an upstream trigger. A scale of 0 pauses gameplay, 0.5 is slow-motion, 1 is normal speed.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Scale | float | Playback rate applied to the shared game clock; 0 pauses, 0.5 halves speed, 1 is normal. Negative values are clamped to 0. |

## `toggle active`
Flips the entity GameObject's active state each time it is Executed by an upstream trigger.

No properties.

## `sprite`
Renders a 2D sprite as a child of the entity, optionally rescaled to Size.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Sprite | Sprite | Asset reference to the sprite to display. |
| Size | Vector2 | Target world-space size in units; the sprite is scaled to fit. |

## `voxel mesh`
Renders a voxel mesh asset as a child of the entity.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Mesh | Mesh | Asset reference to the Mesh to display. |
| Scale | Vector3 | Optional local-space scale multiplier applied to the child renderer. |

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

## Parse-only behaviours (not yet runnable)

These behaviours are registered in the parse catalogue and accept the properties below, but have no runtime `GameBehaviour` implementation — they parse from YAML yet will not execute. Treat them as unsupported until a MonoBehaviour mapping is added in `GameBehaviourFactory`.

### `condition`

| Name | Type |
|------|------|
| ExpressionId | string |
| Arguments | IReadOnlyList<IValueSourceArg> |

### `trigger stay trigger`

| Name | Type |
|------|------|
| TagsToDetect | IReadOnlyList<string> |

### `when all`

| Name | Type |
|------|------|
| TriggerIds | IReadOnlyList<string> |

### `when any`

| Name | Type |
|------|------|
| TriggerIds | IReadOnlyList<string> |

---

## Doc-gen warnings

- `active poll`: `ActivePoll` documents `Note` in its `Properties:` block but `ActivePollInfo` has no such property.
