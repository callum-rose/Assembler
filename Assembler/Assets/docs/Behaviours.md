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
| CentreOfMass | Vector3 | Local-space centre of mass offset. Overrides Unity's auto-computed centre so the body rotates and balances about this point (e.g. push it back to spin a vehicle about its rear axle). Omit to keep the automatic centre. |

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
Integrates Acceleration into a velocity each frame.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Acceleration | Vector3 | World-space acceleration in units per second squared. |
| Velocity | Vector3 | Optional shared velocity variable to integrate into (e.g. !var velocity). |

## `drag`
Exponentially decays a shared velocity variable each frame, modelling linear drag.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Velocity | Vector3 | Writable shared velocity variable to decay (required, e.g. !var velocity). |
| Coefficient | float | Drag rate per second; larger values bleed speed off faster. |

## `speed limit`
Clamps a shared velocity variable's magnitude to Max each frame.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Velocity | Vector3 | Writable shared velocity variable to clamp (required, e.g. !var velocity). |
| Max | float | Maximum allowed speed (magnitude) in units per second. |

## `move towards`
Moves the entity toward Target at a constant Speed, never overshooting.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Target | Vector3 | World-space position to move toward. |
| Speed | float | Movement speed in units per second; a step never passes the target. |

## `smooth move`
Eases the entity toward Target with a critically-damped spring (Vector3.SmoothDamp).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Target | Vector3 | World-space position to ease toward. |
| SmoothTime | float | Approximate time (seconds) to reach the target; larger is slower and softer. |

## `clamp position`
Constrains the entity's position to the axis-aligned box between Min and Max each frame.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Min | Vector3 | Lower per-axis bound of the allowed region. |
| Max | Vector3 | Upper per-axis bound of the allowed region. |

## `wrap position`
Wraps the entity's position around the box between Min and Max each frame (toroidal screen-wrap).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Min | Vector3 | Lower per-axis bound; crossing it teleports the entity to the matching Max edge. |
| Max | Vector3 | Upper per-axis bound; crossing it teleports the entity to the matching Min edge. |

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
| Easing | Easing | Which ease to apply — one of linear, inSine/outSine/inOutSine, inQuad/outQuad/inOutQuad, |

## `scale animation`
Tweens the entity's local scale from Start to End over Duration. See TransformAnimation.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Start | Vector3 | Value to animate from. Falls back to the current transform value when unset. |
| End | Vector3 | Value to animate to. |
| Duration | float | Animation length in seconds (clamped to a minimum of 0). |
| Easing | Easing | Which ease to apply — one of linear, inSine/outSine/inOutSine, inQuad/outQuad/inOutQuad, |

## `rotate animation`
Tweens the entity's euler angles from Start to End over Duration. See TransformAnimation.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Start | Vector3 | Value to animate from. Falls back to the current transform value when unset. |
| End | Vector3 | Value to animate to. |
| Duration | float | Animation length in seconds (clamped to a minimum of 0). |
| Easing | Easing | Which ease to apply — one of linear, inSine/outSine/inOutSine, inQuad/outQuad/inOutQuad, |

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
| Phase | ButtonPhase | When to fire — "down" (press only), "up" (release only), or "hold" (every frame held). Defaults to "down". |

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
| scroll_delta | Vector3 | Scroll wheel delta for this frame (y is the common vertical scroll; z is 0). |

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
| axis | Vector3 | Combined (x, y, 0) axis value; y is 0 when YAxis is unset. |
| x | float | Current XAxis value. |
| y | float | Current YAxis value, or 0 when YAxis is unset. |

## `input action`
Relays an abstract input action (declared in the descriptor's Controls section and bound to a
            physical input per platform) to listeners. A drop-in replacement for the raw key triggers: a button action
            behaves like the key hold/down/up triggers depending on its phase, and a value action behaves like the axis
            trigger, emitting axis/x/y every frame.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Action | string | Name of the abstract action to listen for (must be declared under Controls.Actions). |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| axis | Vector3 | For value actions, the current (x, y, 0) value of the action each frame. |
| x | float | For value actions, the current x component. |
| y | float | For value actions, the current y component. |

## `gamepad button trigger`
Fires on a gamepad / joystick button event (press, release, or hold).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Button | string | Unity key string for the gamepad button (e.g. "joystick button 0", "joystick 1 button 1"). |
| Mode | ButtonPhase | When to fire — "down" (press only), "up" (release only), or "hold" (every frame held). Defaults to "down". |

## `tap trigger`
Fires once when the pointer is pressed and released quickly without moving (a tap).

### Properties

| Name | Type | Description |
|------|------|-------------|
| MaxDuration | float | Longest press, in seconds, that still counts as a tap. Defaults to 0.3. |
| MaxMovement | float | Largest screen-space drift, in pixels, allowed during the press. Defaults to 25. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| position | Vector3 | Screen-space position where the tap was released (z is 0). |

## `double tap trigger`
Fires when two quick taps land close together within a short interval (a double-tap).

### Properties

| Name | Type | Description |
|------|------|-------------|
| MaxInterval | float | Longest gap, in seconds, allowed between the two taps. Defaults to 0.3. |
| MaxMovement | float | Largest screen-space drift, in pixels, allowed both during a tap and between the two taps. Defaults to 25. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| position | Vector3 | Screen-space position where the second tap was released (z is 0). |

## `long press trigger`
Fires once when the pointer is held still for a threshold time (a long press).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Duration | float | Seconds the pointer must be held before the trigger fires. Defaults to 0.5. |
| MaxMovement | float | Largest screen-space drift, in pixels, allowed while holding; moving further cancels the press. Defaults to 25. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| position | Vector3 | Screen-space position of the press when the threshold was reached (z is 0). |
| hold_duration | float | Seconds the pointer had been held when the trigger fired (at least Duration). |

## `swipe trigger`
Fires when the pointer is dragged far enough, fast enough, and then released (a swipe).

### Properties

| Name | Type | Description |
|------|------|-------------|
| MinDistance | float | Minimum screen-space travel, in pixels, required to count as a swipe. Defaults to 75. |
| MaxDuration | float | Longest press, in seconds, that still counts as a swipe rather than a slow drag. Defaults to 0.5. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| start | Vector3 | Screen-space position where the swipe began (z is 0). |
| position | Vector3 | Screen-space position where the swipe ended (z is 0). |
| delta | Vector3 | Vector from start to end (z is 0). |
| distance | float | Length of the swipe in pixels. |
| direction | Vector3 | Normalised swipe direction (z is 0). |

## `drag trigger`
Fires every frame the pointer moves while held down (a drag), reporting the per-frame movement.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Threshold | float | Screen-space distance, in pixels, the pointer must travel from the press point before drag events start firing. Defaults to 25. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| start | Vector3 | Screen-space position where the drag began (z is 0). |
| position | Vector3 | Current screen-space pointer position (z is 0). |
| delta | Vector3 | Screen-space movement since the previous frame (z is 0). |

## `pinch and rotate trigger`
Fires every frame two fingers change their separation or orientation (a pinch / zoom and twist).

No properties.

### Outputs

| Name | Type | Description |
|------|------|-------------|
| center | Vector3 | Screen-space midpoint between the two fingers (z is 0). |
| distance | float | Current distance between the two fingers, in pixels. |
| delta | float | Change in finger distance since the previous frame (positive = spreading apart). |
| scale | float | Ratio of the current distance to the previous frame's (greater than 1 = zooming in). |
| angle | float | Current angle of the line between the two fingers, in degrees. |
| angle_delta | float | Signed change in that angle since the previous frame, in degrees (positive = counter-clockwise). |

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
| TagsToDetect | IReadOnlyList<string> | Only fire when the other entity has at least one of these tags. Leave empty to fire on any entity. |

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
| TagsToDetect | IReadOnlyList<string> | Only fire when the other entity has at least one of these tags. Leave empty to fire on any entity. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| other_position | Vector3 | Other entity's world position at the moment of entry. |

## `trigger exit trigger`
Fires when an entity matching TagsToDetect exits this entity's trigger collider.

### Properties

| Name | Type | Description |
|------|------|-------------|
| TagsToDetect | IReadOnlyList<string> | Only fire when the other entity has at least one of these tags. Leave empty to fire on any entity. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| other_position | Vector3 | Other entity's world position at the moment of exit. |

## `collision exit trigger`
Fires when a non-trigger collision ends with another entity matching TagsToDetect.

### Properties

| Name | Type | Description |
|------|------|-------------|
| TagsToDetect | IReadOnlyList<string> | Only fire when the other entity has at least one of these tags. Leave empty to fire on any entity. |

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
| TagsToDetect | IReadOnlyList<string> | Only fire when the other entity has at least one of these tags. Leave empty to fire on any entity. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| contact_point | Vector3 | World-space point of contact for this frame. |
| contact_normal | Vector3 | Surface normal at the contact point. |
| other_velocity | Vector3 | Other body's linear velocity (zero if no Rigidbody). |
| other_position | Vector3 | Other entity's world position. |

## `spawner`
Spawns an instance of a template at a position when Executed.

### Properties

| Name | Type | Description |
|------|------|-------------|
| TemplateId | string | Id of the template to instantiate. Used as a fallback when Templates is empty. |
| Templates | IReadOnlyList<SpawnTemplateInfo> | Optional list of template ids (or { Template, Weight } maps); one is chosen per spawn. Takes precedence over TemplateId when non-empty. |
| Selection | string | How Templates is sampled: 'random' (weighted, the default) or 'sequential' (round-robin in list order; weights ignored). |
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
Adds the output Unity Camera plus a Cinemachine brain, so virtual cameras (e.g. camera follow)
            can drive and blend this camera. Also adds an impulse listener so camera shake is visible.

### Properties

| Name | Type | Description |
|------|------|-------------|
| View | CameraProjection | "orthographic" for a 2D-style camera, or "perspective" (default) for a 3D projection. |
| Size | float | Orthographic size in world units (only used when View = "orthographic"). |
| DefaultBlend | float | Default blend time in seconds when the brain cuts between virtual cameras (0 = instant cut). |

## `camera follow`
Adds a Cinemachine virtual camera that follows and/or looks at a target entity, blended by the brain on
            the output camera. Mode picks a 2D screen-space rig or a 3D world-offset rig; omit Target
            for a pure look-at camera.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Target | Tag/Id | Entity to follow, as { Tag: <entity-tag> } or { Id: <entity-id> }. Omit for look-at only. |
| LookAt | Tag/Id | Entity to aim at, as { Tag: … } or { Id: … }. Adds an aim composer. |
| Mode | CameraFollowMode | "2d" (screen-space framing, default) or "3d" (world-space follow offset + aim). |
| Priority | int | Virtual-camera priority; the brain shows the highest-priority live vcam. |
| Lens | float | Orthographic size (2D) or field of view in degrees (3D), depending on the output camera projection. |
| Damping | float | How softly the camera follows (seconds-ish); 0 is instant. Applies to body and aim. |
| DeadZone | float | 2D only — size (0..1 of the screen) of the region the target can move in without the camera reacting. |
| CameraDistance | float | 2D only — distance the camera keeps in front of the target along its view axis (default 10). Must be > 0 or an orthographic camera sits on the target's plane and sees nothing. |
| ScreenOffset | Vector3 | 2D only — where on screen the target sits, as an offset from centre (-0.5..0.5); z is ignored. |
| FollowOffset | Vector3 | 3D only — world-space offset the camera maintains from the target. |

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

## `state machine`
Finite state machine for entity AI. Holds the current state in an entity string-variable and
            transitions between declared states when a transition's condition becomes true. Transitions are
            evaluated every frame in declared order, first match wins (one transition per frame), so behaviour
            is deterministic. On a transition it fires the old state's OnExit hooks then the new state's
            OnEnter hooks; the initial state's OnEnter fires once on start.

### Properties

| Name | Type | Description |
|------|------|-------------|
| StateVariable | string | Id of the string entity variable holding the current state. Auto-declared (seeded to Initial) if not already present, so it shows up in the debug console and save snapshots. |
| Initial | string | The starting state. Must be one of States. |
| States | IReadOnlyList<StateInfo> | Map of state name to optional { OnEnter, OnExit } hooks. Each hook list uses the same shape as a behaviour's top-level Listeners (EntityId + BehaviourId, EntityTag, BehaviourTag, or !gameover). |
| Transitions | IReadOnlyList<TransitionInfo> | Ordered list of { from, to, when }. The first transition whose `from` equals the current state and whose `when` condition is true is taken. |

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

## `int variable changed trigger`
Fires when an int variable changes. See VariableChangedTrigger.

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | int | The variable to watch. Must be a writable `!var` reference of the matching type. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| value | T | The variable's new value. |
| previous | T | The variable's value immediately before the change. |

## `float variable changed trigger`
Fires when a float variable changes. See VariableChangedTrigger.

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | float | The variable to watch. Must be a writable `!var` reference of the matching type. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| value | T | The variable's new value. |
| previous | T | The variable's value immediately before the change. |

## `bool variable changed trigger`
Fires when a bool variable changes. See VariableChangedTrigger.

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | bool | The variable to watch. Must be a writable `!var` reference of the matching type. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| value | T | The variable's new value. |
| previous | T | The variable's value immediately before the change. |

## `string variable changed trigger`
Fires when a string variable changes. See VariableChangedTrigger.

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | string | The variable to watch. Must be a writable `!var` reference of the matching type. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| value | T | The variable's new value. |
| previous | T | The variable's value immediately before the change. |

## `vector variable changed trigger`
Fires when a vector variable changes. See VariableChangedTrigger.

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | Vector3 | The variable to watch. Must be a writable `!var` reference of the matching type. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| value | T | The variable's new value. |
| previous | T | The variable's value immediately before the change. |

## `colour variable changed trigger`
Fires when a colour variable changes. See VariableChangedTrigger.

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | Color | The variable to watch. Must be a writable `!var` reference of the matching type. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| value | T | The variable's new value. |
| previous | T | The variable's value immediately before the change. |

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
| Size | Vector3 | Target world-space size in units; the sprite is scaled to fit. |

## `voxel mesh`
Renders a voxel mesh asset as a child of the entity.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Mesh | Mesh | Asset reference to the Mesh to display. |
| Scale | Vector3 | Optional local-space scale multiplier applied to the child renderer. |

## `primitive`
Adds a 3D primitive mesh (chosen by Shape) as a child of the entity.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Shape | PrimitiveType | Which primitive to create — one of "cube", "sphere", "capsule", "cylinder", "plane", "quad" (defaults to "cube"). |
| Colour | Color | Optional tint applied to the primitive's material. |
| Size | Vector3 | Optional local scale of the primitive child. |

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

## `ui canvas`
Roots a UI tree: adds a screen-space Canvas that scales with screen size. Place child UI
            entities (containers, labels, buttons) under this entity to compose the interface.

### Properties

| Name | Type | Description |
|------|------|-------------|
| MatchWidthOrHeight | float | CanvasScaler match (0 = match width, 1 = match height, 0.5 = balanced). |
| ReferenceResolution | Vector3 | Design resolution the UI scales from, as a vector (X = width, Y = height). |

## `ui container`
Groups child UI entities. By default it arranges them in a vertical or horizontal stack
            using a uGUI layout group so UIs reflow responsively without hand-placed coordinates; with
            Direction "none" it adds no layout group and children are positioned manually.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Direction | LayoutDirection | "vertical" (default), "horizontal", or "none"/"manual"/"free" (no layout group — manual placement). |
| Spacing | float | Gap between children, in reference pixels (layout directions only). |
| Padding | float | Inner padding on all sides, in reference pixels (layout directions only). |
| ChildAlignment | TextAnchor | e.g. "middle-center", "upper-left" (see TextAnchor names; layout directions only). |
| FitContent | bool | When true, the container shrinks to fit its children (adds a ContentSizeFitter). |

## `text label`
Displays a line of text via a uGUI/TextMeshPro label. The text is re-read every frame, so
            binding it to a variable or expression shows live values (scores, timers, etc.).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Text | string | Body text (re-read each frame; bind to a variable/expression for live values). |
| FontSize | int | Font size in reference pixels. |
| PreferredWidth | float | Preferred width for the parent layout (omit for a sensible default). |
| PreferredHeight | float | Preferred height for the parent layout (omit for a sensible default). |

## `ui button`
A clickable uGUI button. Acts as a trigger: notifies its listeners each time it is
            clicked. The caption is re-read every frame, so it can be bound to a variable/expression.

### Properties

| Name | Type | Description |
|------|------|-------------|
| Label | string | Button caption (re-read each frame). |
| PreferredWidth | float | Preferred width for the parent layout (omit for a sensible default). |
| PreferredHeight | float | Preferred height for the parent layout (omit for a sensible default). |

## `ui slider`
A uGUI slider. Acts as a trigger: notifies its listeners whenever the value changes.

### Properties

| Name | Type | Description |
|------|------|-------------|
| InitialValue | float | Starting value. |
| MinValue | float | Minimum value the slider can produce. |
| MaxValue | float | Maximum value the slider can produce. |
| PreferredWidth | float | Preferred width for the parent layout (omit for a sensible default). |
| PreferredHeight | float | Preferred height for the parent layout (omit for a sensible default). |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| value | float | The new slider value after the change. |

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
