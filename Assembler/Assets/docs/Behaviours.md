# Behaviours

Generated from `Assembler.Behaviours` XML doc comments. Each behaviour's description, property meanings, and trigger outputs are authored on the corresponding `GameBehaviour` MonoBehaviour; property names and types are reflected from the matching `*Info` record.

## `box collider`
Adds a Unity BoxCollider to the entity, sized to Size. Required for collision/trigger physics events.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Size | Vector3 | Local-space dimensions of the box (x, y, z). |
| IsTrigger | bool | When true the collider fires trigger events (no physical collision response) instead of acting as a solid collider. |
| Bounciness | float | Physics-material bounciness 0–1; when set (with any friction property) a PhysicsMaterial is created and assigned. |
| DynamicFriction | float | Physics-material friction 0–1 applied while the surfaces are sliding. |
| StaticFriction | float | Physics-material friction 0–1 applied while the surfaces are at rest. |

## `sphere collider`
Adds a Unity SphereCollider to the entity. Required for collision/trigger physics events.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Radius | float | Local-space radius of the sphere. |
| IsTrigger | bool | When true the collider fires trigger events (no physical collision response) instead of acting as a solid collider. |
| Bounciness | float | Physics-material bounciness 0–1; when set (with any friction property) a PhysicsMaterial is created and assigned. |
| DynamicFriction | float | Physics-material friction 0–1 applied while the surfaces are sliding. |
| StaticFriction | float | Physics-material friction 0–1 applied while the surfaces are at rest. |

## `capsule collider`
Adds a Unity CapsuleCollider to the entity. Required for collision/trigger physics events.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Radius | float | Local-space radius of the capsule hemispheres. |
| Height | float | Local-space total height of the capsule along its Direction axis. |
| Direction | int | Axis the capsule is aligned to — 0 = X, 1 = Y, 2 = Z. |
| IsTrigger | bool | When true the collider fires trigger events instead of acting as a solid collider. |
| Bounciness | float | Physics-material bounciness 0–1; when set (with any friction property) a PhysicsMaterial is created and assigned. |
| DynamicFriction | float | Physics-material friction 0–1 applied while the surfaces are sliding. |
| StaticFriction | float | Physics-material friction 0–1 applied while the surfaces are at rest. |

## `mesh collider`
Adds a Unity MeshCollider to the entity using the mesh from the entity's MeshFilter. Required for collision/trigger physics events on arbitrary meshes (e.g. voxel meshes).

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Convex | bool | When true the collider uses a convex hull (required for non-kinematic Rigidbodies and trigger volumes). |
| IsTrigger | bool | When true the collider fires trigger events instead of acting as a solid collider (requires Convex = true). |
| Bounciness | float | Physics-material bounciness 0–1; when set (with any friction property) a PhysicsMaterial is created and assigned. |
| DynamicFriction | float | Physics-material friction 0–1 applied while the surfaces are sliding. |
| StaticFriction | float | Physics-material friction 0–1 applied while the surfaces are at rest. |

## `rigidbody`
Adds a Unity Rigidbody to the entity so it participates in physics simulation.

**Role:** Continuous / passive (runs itself; not a listener target).

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

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Force | Vector3 | World-space force vector applied with ForceMode.Force (mass-dependent, frame-rate independent acceleration). |

## `add impulse`
Adds an instantaneous world-space impulse to the entity's Rigidbody when Executed (typically via a trigger).

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Impulse | Vector3 | World-space impulse applied with ForceMode.Impulse (mass-dependent, instantaneous velocity change). |

## `add torque`
Adds a continuous world-space torque to the entity's Rigidbody when Executed (typically via a trigger).

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Torque | Vector3 | World-space torque vector applied with ForceMode.Force (mass-dependent angular acceleration). |

## `set velocity`
Sets the entity's Rigidbody linear velocity to Velocity when Executed (typically via a trigger).

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Velocity | Vector3 | World-space linear velocity in units per second. |

## `set angular velocity`
Sets the entity's Rigidbody angular velocity to AngularVelocity when Executed (typically via a trigger).

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| AngularVelocity | Vector3 | World-space angular velocity in radians per second around each axis. |

## `velocity`
Moves the entity each frame by Velocity * deltaTime.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Velocity | Vector3 | World-space velocity in units per second. |

## `acceleration`
Integrates Acceleration into a velocity each frame.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Acceleration | Vector3 | World-space acceleration in units per second squared. |
| Velocity | Vector3 | Optional shared velocity variable to integrate into (e.g. !var velocity). |

## `drag`
Exponentially decays a shared velocity variable each frame, modelling linear drag.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Velocity | Vector3 | Writable shared velocity variable to decay (required, e.g. !var velocity). |
| Coefficient | float | Drag rate per second; larger values bleed speed off faster. |

## `speed limit`
Clamps a shared velocity variable's magnitude to Max each frame.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Velocity | Vector3 | Writable shared velocity variable to clamp (required, e.g. !var velocity). |
| Max | float | Maximum allowed speed (magnitude) in units per second. |

## `move towards`
Moves the entity toward Target at a constant Speed, never overshooting.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Target | Vector3 | World-space position to move toward. |
| Speed | float | Movement speed in units per second; a step never passes the target. |

## `smooth move`
Eases the entity toward Target with a critically-damped spring (Vector3.SmoothDamp).

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Target | Vector3 | World-space position to ease toward. |
| SmoothTime | float | Approximate time (seconds) to reach the target; larger is slower and softer. |

## `clamp position`
Constrains the entity's position to the axis-aligned box between Min and Max each frame.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Min | Vector3 | Lower per-axis bound of the allowed region. |
| Max | Vector3 | Upper per-axis bound of the allowed region. |

## `wrap position`
Wraps the entity's position around the box between Min and Max each frame (toroidal screen-wrap).

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Min | Vector3 | Lower per-axis bound; crossing it teleports the entity to the matching Max edge. |
| Max | Vector3 | Upper per-axis bound; crossing it teleports the entity to the matching Min edge. |

## `translate`
Adds Displacement to the entity's world position each time it Executes (e.g. via a trigger).

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Displacement | Vector3 | World-space offset to add on each execution. |

## `angular velocity`
Rotates the entity each frame by AngularVelocity * deltaTime (Euler degrees per second).

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| AngularVelocity | Vector3 | World-space angular velocity in degrees per second (Euler per axis). |

## `rotate`
Adds Displacement (Euler degrees) to the entity's world rotation each time it Executes (e.g. via a trigger).

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Displacement | Vector3 | World-space Euler angle offset (degrees) to add on each execution. |

## `rotation setter`
Sets the entity's world rotation to Rotation (Euler degrees) when Executed (typically via a trigger).

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Rotation | Vector3 | World-space Euler angles (degrees) to set the entity's rotation to on each execution. |

## `look at`
Turns the entity each frame to face Target in the XZ ground plane (a yaw about +Y).

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Target | Vector3 | World-space point to face. |
| TurnRate | float | Maximum turn speed in degrees/sec; 0 (the default) snaps instantly to face the target. |

## `move animation`
Tweens the entity's world position from Start to End over Duration. See TransformAnimation.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Start | Vector3 | Value to animate from. Falls back to the current transform value when unset. |
| End | Vector3 | Value to animate to. |
| Duration | float | Animation length in seconds (clamped to a minimum of 0). |
| Easing | Easing | Which ease to apply — one of linear, inSine/outSine/inOutSine, inQuad/outQuad/inOutQuad, |

## `scale animation`
Tweens the entity's local scale from Start to End over Duration. See TransformAnimation.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Start | Vector3 | Value to animate from. Falls back to the current transform value when unset. |
| End | Vector3 | Value to animate to. |
| Duration | float | Animation length in seconds (clamped to a minimum of 0). |
| Easing | Easing | Which ease to apply — one of linear, inSine/outSine/inOutSine, inQuad/outQuad/inOutQuad, |

## `rotate animation`
Tweens the entity's euler angles from Start to End over Duration. See TransformAnimation.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Start | Vector3 | Value to animate from. Falls back to the current transform value when unset. |
| End | Vector3 | Value to animate to. |
| Duration | float | Animation length in seconds (clamped to a minimum of 0). |
| Easing | Easing | Which ease to apply — one of linear, inSine/outSine/inOutSine, inQuad/outQuad/inOutQuad, |

## `input action`
Relays an abstract input action (declared in the descriptor's Controls section and bound to a
            physical input per platform) to listeners. This is the single input-event source for gameplay: a button
            action fires on its phase (hold ⇒ every frame held, down ⇒ on press, up ⇒ on release), and a value action
            emits axis/x/y every frame. Physical keys, mouse buttons, mouse position/scroll, and gamepad controls are
            all expressed as bindings on the action rather than as separate trigger types.

**Role:** Trigger (event source — emits to listeners; not a listener target).

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

## `cursor lock`
Locks (and optionally hides) the hardware cursor so relative mouse-look deltas keep flowing past the screen edges; applied on start and on Execute, and restored when the entity is destroyed (e.g. on game-over).

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Locked | bool | Whether to lock the cursor to the window centre (default true). |
| Visible | bool | Whether the cursor stays visible while locked (default false). |

## `tap trigger`
Fires once when the pointer is pressed and released quickly without moving (a tap).

**Role:** Trigger (event source — emits to listeners; not a listener target).

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

**Role:** Trigger (event source — emits to listeners; not a listener target).

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

**Role:** Trigger (event source — emits to listeners; not a listener target).

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

**Role:** Trigger (event source — emits to listeners; not a listener target).

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

**Role:** Trigger (event source — emits to listeners; not a listener target).

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

**Role:** Trigger (event source — emits to listeners; not a listener target).

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
Fires once after a delay.

**Role:** Executable (valid `Listeners:` target; also a trigger — emits to its own listeners).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Delay | float | Seconds to wait before notifying listeners. |
| AutoStart | bool | When true the countdown starts on entity start; when false it waits for an Execute call from upstream. |

## `deferred trigger`
Forwards a trigger event to listeners after a delay. Insert between an upstream trigger and downstream behaviours to defer execution.

**Role:** Executable (valid `Listeners:` target; also a trigger — emits to its own listeners).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Delay | float | Seconds to wait between Execute and notifying listeners. |

## `debounced trigger`
Forwards a trigger event only when no prior trigger has been received within the last Interval seconds. Use to suppress rapid repeat triggers.

**Role:** Executable (valid `Listeners:` target; also a trigger — emits to its own listeners).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Interval | float | Seconds that must elapse since the previous incoming trigger before another one is forwarded. |

## `throttled trigger`
Forwards at most Rate trigger events per second. Incoming triggers that arrive sooner than 1/Rate seconds after the previous forwarded one are dropped.

**Role:** Executable (valid `Listeners:` target; also a trigger — emits to its own listeners).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Rate | float | Maximum number of forwarded triggers per second. |

## `on start trigger`
Fires once when the entity is first started.

**Role:** Trigger (event source — emits to listeners; not a listener target).

No properties.

## `interval trigger`
Fires repeatedly at an interval. Optionally limited to a number of repetitions.

**Role:** Executable (valid `Listeners:` target; also a trigger — emits to its own listeners).

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

**Role:** Trigger (event source — emits to listeners; not a listener target).

No properties.

## `fixed update trigger`
Fires every Unity FixedUpdate step. Use for physics-step-aligned, fixed-timestep logic.

**Role:** Trigger (event source — emits to listeners; not a listener target).

No properties.

## `collision enter trigger`
Fires when a non-trigger collision begins with another entity matching TagsToDetect. Requires colliders + a Rigidbody.

**Role:** Trigger (event source — emits to listeners; not a listener target).

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

**Role:** Trigger (event source — emits to listeners; not a listener target).

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

**Role:** Trigger (event source — emits to listeners; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| TagsToDetect | IReadOnlyList<string> | Only fire when the other entity has at least one of these tags. Leave empty to fire on any entity. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| other_position | Vector3 | Other entity's world position at the moment of exit. |

## `trigger stay trigger`
Fires every physics frame while an entity matching TagsToDetect stays inside this entity's trigger collider.

**Role:** Trigger (event source — emits to listeners; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| TagsToDetect | IReadOnlyList<string> | Only fire while the other entity has at least one of these tags. Leave empty to fire on any entity. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| other_position | Vector3 | Other entity's world position this frame. |

## `collision exit trigger`
Fires when a non-trigger collision ends with another entity matching TagsToDetect.

**Role:** Trigger (event source — emits to listeners; not a listener target).

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

**Role:** Trigger (event source — emits to listeners; not a listener target).

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

**Role:** Executable (valid `Listeners:` target).

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

**Role:** Executable (valid `Listeners:` target).

No properties.

## `position setter`
Sets the entity's world position to Position when Executed (typically via a trigger).

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Position | Vector3 | World-space position to teleport the entity to on each execution. |

## `camera`
Adds the output Unity Camera plus a Cinemachine brain, so virtual cameras (e.g. camera follow)
            can drive and blend this camera. Also adds an impulse listener so camera shake is visible.

**Role:** Continuous / passive (runs itself; not a listener target).

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

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Target | Tag/Id | Entity to follow, as { Tag: <entity-tag> } or { Id: <entity-id> }. Omit for look-at only. |
| LookAt | Tag/Id | Entity to aim at, as { Tag: … } or { Id: … }. Adds an aim composer. |
| Mode | CameraFollowMode | "2d" (screen-space framing, default) or "3d" (world-space follow offset + aim). |
| Priority | int | Virtual-camera priority; the brain shows the highest-priority live vcam. Re-read every frame, so binding it to a variable/expression dynamically switches which camera is live (Cinemachine blends across). |
| Lens | float | Orthographic size (2D) or field of view in degrees (3D), depending on the output camera projection. |
| Damping | float | How softly the camera follows (seconds-ish); 0 is instant. Applies to body and aim. |
| DeadZone | float | 2D only — size (0..1 of the screen) of the region the target can move in without the camera reacting. |
| CameraDistance | float | 2D only — distance the camera keeps in front of the target along its view axis (default 10). Must be > 0 or an orthographic camera sits on the target's plane and sees nothing. |
| ScreenOffset | Vector3 | 2D only — where on screen the target sits, as an offset from centre (-0.5..0.5); z is ignored. |
| FollowOffset | Vector3 | 3D only — world-space offset the camera maintains from the target. |

## `camera shake`
Emits a one-shot Cinemachine impulse when Executed (typically from a collision or other trigger),
            shaking every camera in range. Lives on any entity — no virtual camera required — and pairs with the
            CinemachineImpulseListener the camera behaviour already adds.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Force | float | Impulse amplitude scalar (default 1). Read on every fire, so it may be an expression/variable. |
| Duration | float | How long the shake lasts in seconds (default Cinemachine's 0.2). Applied at build. |
| Velocity | Vector3 | Direction and base magnitude of the kick (default (0,-1,0), i.e. downward). Scaled by Force. |

## `camera noise`
Adds constant handheld/ambient camera shake to a virtual camera via a Cinemachine
            BasicMultiChannelPerlin noise component. Pick a bundled noise profile by name and scale its
            amplitude/frequency.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Profile | string | Bundled noise profile name (default "Handheld_normal_mild"). e.g. "Handheld_normal_strong", "6D Shake". |
| Amplitude | float | Multiplier on the profile's positional/rotational shake (default 1 = the profile's own amount). |
| Frequency | float | Multiplier on how fast the shake oscillates (default 1 = the profile's own rate). |

## `camera zoom`
Adds a Cinemachine FollowZoom extension that auto-adjusts the camera's field of view to hold
            the follow target at a constant on-screen width (dolly-zoom style framing).

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Width | float | Target's desired on-screen width in world units (default Cinemachine's 2). Smaller = more zoomed in. |
| Damping | float | How softly the FOV adjusts, in seconds (default 1); 0 is instant. |
| MinFOV | float | Lower bound on the field of view in degrees (default 3) — the most it will zoom in. |
| MaxFOV | float | Upper bound on the field of view in degrees (default 60) — the most it will zoom out. |

## `camera orbit`
Adds a Cinemachine virtual camera that orbits a target entity at a fixed radius and height
            (third-person / orbital framing), blended by the brain on the output camera. Mirrors
            camera follow but uses an orbital body rather than a screen/offset rig.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Target | Tag/Id | Entity to orbit, as { Tag: <entity-tag> } or { Id: <entity-id> }. |
| Radius | float | Orbit distance from the target in world units (default Cinemachine's 10). |
| Height | float | Vertical offset above the target the camera orbits around (default 0). |
| OrbitSpeed | float | Auto-orbit rate in degrees per second around the target (default 0 = hold a fixed angle). |
| Damping | float | How softly the camera tracks the target (seconds-ish); 0 is instant. |
| Priority | int | Virtual-camera priority; the brain shows the highest-priority live vcam. |
| Lens | float | Orthographic size or field of view in degrees, depending on the output camera projection. |

## `camera confiner`
Adds a Cinemachine confiner extension that clamps the virtual camera so it never leaves a bounding
            volume defined by another entity's collider. Mode picks a 2D Collider2D boundary or a 3D
            Collider volume.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Bounds | Tag/Id | Entity whose collider defines the boundary, as { Tag: … } or { Id: … }. 2D mode reads its Collider2D, 3D mode its Collider. |
| Mode | CameraConfinerMode | "2d" (clamp to a Collider2D, default) or "3d" (clamp to a Collider volume). |
| Damping | float | 2D only — how softly the camera is pushed back inside the bounds, in seconds (default 0 = instant). |
| Padding | float | Distance from the edge at which the camera starts slowing before the hard boundary (default 0). |

## `camera group`
Adds a Cinemachine virtual camera that frames a whole group of entities at once: it builds a
            TargetGroup from every entity carrying Tag and uses GroupFraming to auto-zoom so they
            all stay on screen. The membership is rebuilt each frame, so the group tracks spawns and deaths.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Tag | string | Entity tag whose members the camera frames; re-queried every frame so spawned/destroyed entities update the group. |
| Priority | int | Virtual-camera priority; the brain shows the highest-priority live vcam. |
| Damping | float | How softly the framing reacts as members move, in seconds (default Cinemachine's 2); 0 is instant. |
| FramingSize | float | How much of the screen the group should fill, 0..1 (default Cinemachine's 0.8). |
| Lens | float | Orthographic size or field of view in degrees, depending on the output camera projection. |

## `condition gate`
Forwards an upstream trigger to listeners only when Condition evaluates to true at that moment.

**Role:** Executable (valid `Listeners:` target; also a trigger — emits to its own listeners).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Condition | bool | Boolean expression checked on each Execute call. |

## `inverse condition gate`
Forwards an upstream trigger to listeners only when Condition evaluates to false at that moment.

**Role:** Executable (valid `Listeners:` target; also a trigger — emits to its own listeners).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Condition | bool | Boolean expression checked on each Execute call; listeners fire when it is false. |

## `exclusive trigger`
Forwards an upstream trigger to listeners only if no other trigger sharing the same Group has already fired this frame.

**Role:** Executable (valid `Listeners:` target; also a trigger — emits to its own listeners).

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

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| StateVariable | string | Id of the string entity variable holding the current state. Auto-declared (seeded to Initial) if not already present, so it shows up in the debug console and save snapshots. |
| Initial | string | The starting state. Must be one of States. |
| States | IReadOnlyList<StateInfo> | Map of state name to optional { OnEnter, OnExit } hooks. Each hook list uses the same shape as a behaviour's top-level Listeners (EntityId + BehaviourId, EntityTag, BehaviourTag, or !gameover). |
| Transitions | IReadOnlyList<TransitionInfo> | Ordered list of { from, to, when }. The first transition whose `from` equals the current state and whose `when` condition is true is taken. |

## `perceive`
Sensor that scans for the nearest tagged entity and writes the result into blackboard variables.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Tag | string | Entity tag to look for. |
| Radius | float | Detection range in world units. |
| ConeAngle | float | Optional full cone angle in degrees; omit for an omnidirectional scan. Needs Forward. |
| Forward | Vector3 | Optional facing direction for the cone (a direction vector). |
| RequireLineOfSight | bool | When true, a candidate is only detected if no obstacle blocks the line to it. |
| Obstacles | string | Entity tag that blocks line of sight (empty means nothing blocks). |
| Interval | float | Seconds between scans; 0 scans every frame. Trades responsiveness for cost. |
| TargetId | string | !var reference to the string variable that receives the detected entity id. |
| TargetPosition | Vector3 | !var reference to the vector variable that receives the detected entity position. |
| HasTarget | bool | !var reference to the bool variable set true while a target is visible, false otherwise. |
| LastKnownPosition | Vector3 | !var reference to the vector variable updated ONLY while visible (memory of last sighting). |

## `perceive all`
Sensor that scans for every tagged entity in range and writes them into blackboard list variables.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Tag | string | Entity tag to look for. |
| Radius | float | Detection range in world units. |
| ConeAngle | float | Optional full cone angle in degrees; omit for an omnidirectional scan. Needs Forward. |
| Forward | Vector3 | Optional facing direction for the cone (a direction vector). |
| RequireLineOfSight | bool | When true, a candidate is only detected if no obstacle blocks the line to it. |
| Obstacles | string | Entity tag that blocks line of sight (empty means nothing blocks). |
| Interval | float | Seconds between scans; 0 scans every frame. Trades responsiveness for cost. |
| Positions | List<Vector3> | !var reference to the vector-list variable cleared and filled with each detected entity's position. |
| Ids | List<string> | !var reference to the string-list variable cleared and filled with each detected entity's id. |
| Velocities | List<Vector3> | !var reference to the vector-list variable cleared and filled with each detected entity's velocity (finite-differenced between scans). |
| Count | int | !var reference to the int variable set to the number of entities detected this scan. |

## `steering`
Blends a weighted list of steering forces into one velocity each frame.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Forces | IReadOnlyList<SteeringForceInfo> | List of { Force, Weight } entries; Force is a Vector3 (e.g. !expr Seek(...)), Weight a float. |
| MaxSpeed | float | Upper bound on the blended velocity's magnitude. |
| Output | Vector3 | Name of the vector variable to write the blended velocity into (omit to move the entity directly). |

## `navigate`
Moves an entity to a target along a grid path, recomputed on a cadence.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Target | Vector3 | World point to navigate to. |
| Speed | float | Movement speed in units per second. |
| SlowingRadius | float | Distance from the goal at which to begin easing to a stop. |
| Recompute | float | Seconds between route recomputes (0 recomputes every frame). |
| Mode | string | "astar" (per-agent path) or "flowfield" (shared-goal field). |
| AgentRadius | float | Clearance kept from obstacles for this agent's route, in world units; omit to inherit the game-wide Navigation DefaultAgentRadius. A larger agent routes around obstacles more widely than a smaller one, so they can take different paths. |
| Output | Vector3 | Name of the vector variable to write the desired velocity into (omit to move the entity directly). |

## `patrol`
Walks an entity through an ordered list of waypoints, advancing on arrival.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Waypoints | List<Vector3> | Ordered world points to patrol (a vector-list !var, an inline list, or an !expr PositionList). |
| Loop | bool | Wrap back to the first waypoint after the last (patrol loop) instead of stopping. |
| PingPong | bool | Reverse direction at each end instead of wrapping (overrides Loop). |
| ArriveRadius | float | Distance at which the current waypoint counts as reached and the index advances. |
| Speed | float | Movement speed in units per second. |
| Output | Vector3 | Name of the vector variable to write the desired velocity into (omit to move the entity directly). |
| CurrentIndex | int | Name of an int variable to publish the current waypoint index into (omit to skip; for FSM/debug). |

## `grid mover`
Moves the entity tile-to-tile along the shared nav grid: it heads to the centre of the next cell, and
            only re-decides direction once it arrives there, so motion is always grid-aligned and never diagonal
            (classic maze movement). At each cell it turns onto the requested Direction if that neighbour is
            walkable, else continues its current heading, else stops. Walkability and cell geometry come from the
            NavGridService, so a player driven by this and the AI driven by navigate share one
            maze. Robust to external teleports (a wrap position tunnel): a large jump re-anchors to the new
            cell instead of dragging the entity back.
            Properties:
              Direction: Requested heading, re-read each frame (bind to a variable an input trigger writes); snapped to a cardinal.
              Speed: Movement speed in units per second.
              AgentRadius: Clearance used for walkability checks, in world units; omit to inherit the game-wide Navigation DefaultAgentRadius. Tile-locked movers usually leave this 0 (a one-cell agent).

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Direction | Vector3 |  |
| Speed | float |  |
| AgentRadius | float |  |

## `vector variable setter`
Writes a Vector3 value into the referenced variable when Executed. See VariableSetterBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | Vector3 | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | Vector3 | Source value to assign. Can be a constant, expression, or another variable reference. |

## `int variable setter`
Writes a int value into the referenced variable when Executed. See VariableSetterBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | int | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | int | Source value to assign. Can be a constant, expression, or another variable reference. |

## `float variable setter`
Writes a float value into the referenced variable when Executed. See VariableSetterBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | float | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | float | Source value to assign. Can be a constant, expression, or another variable reference. |

## `bool variable setter`
Writes a bool value into the referenced variable when Executed. See VariableSetterBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | bool | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | bool | Source value to assign. Can be a constant, expression, or another variable reference. |

## `string variable setter`
Writes a string value into the referenced variable when Executed. See VariableSetterBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | string | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | string | Source value to assign. Can be a constant, expression, or another variable reference. |

## `colour variable setter`
Writes a Color value into the referenced variable when Executed. See VariableSetterBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | Color | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | Color | Source value to assign. Can be a constant, expression, or another variable reference. |

## `record variable setter`
Writes a record value into the referenced variable when Executed. See VariableSetterBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | Record | Reference to the destination variable (typed). Typically a `!ref` to a variable declared on the entity or game. |
| Value | Record | Source value to assign. Can be a constant, expression, or another variable reference. |

## `int variable changed trigger`
Fires when an int variable changes. See VariableChangedTrigger.

**Role:** Trigger (event source — emits to listeners; not a listener target).

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

**Role:** Trigger (event source — emits to listeners; not a listener target).

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

**Role:** Trigger (event source — emits to listeners; not a listener target).

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

**Role:** Trigger (event source — emits to listeners; not a listener target).

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

**Role:** Trigger (event source — emits to listeners; not a listener target).

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

**Role:** Trigger (event source — emits to listeners; not a listener target).

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

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> | Reference to the target list variable. |
| Value | Vector3 | Item to append. |

## `vector list insert`
Inserts a Vector3 value into the target list at a given index when Executed. See ListInsertBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> | Reference to the target list variable. |
| Index | int | Zero-based position to insert at. Valid range is [0, Count]. |
| Value | Vector3 | Item to insert. |

## `vector list remove at`
Removes the Vector3 item at a given index from the target list when Executed. See ListRemoveAtBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `vector list remove`
Removes the first occurrence of a Vector3 value from the target list when Executed. See ListRemoveBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> | Reference to the target list variable. |
| Value | Vector3 | Item to remove. |

## `vector list set at`
Overwrites the Vector3 item at a given index in the target list when Executed. See ListSetAtBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | Vector3 | New item. |

## `vector list set`
Replaces the entire contents of the target Vector3 list with another list when Executed. See ListSetBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> | Reference to the target list variable. |
| Value | List<Vector3> | List whose items replace List's contents (typically an expression returning a list). |

## `vector list add range`
Appends every item from another Vector3 list to the target list when Executed. See ListAddRangeBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> | Reference to the target list variable. |
| Other | List<Vector3> | List whose items will be appended to List. |

## `vector list clear`
Removes all items from the target Vector3 list when Executed. See ListClearBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> | Reference to the target list variable. |

## `vector list loop trigger`
Iterates a Vector3 list when Executed, firing listeners once per element. See ListLoopTrigger.

**Role:** Executable (valid `Listeners:` target; also a trigger — emits to its own listeners).

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

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> | Reference to the target list variable. |
| Value | int | Item to append. |

## `int list insert`
Inserts a int value into the target list at a given index when Executed. See ListInsertBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> | Reference to the target list variable. |
| Index | int | Zero-based position to insert at. Valid range is [0, Count]. |
| Value | int | Item to insert. |

## `int list remove at`
Removes the int item at a given index from the target list when Executed. See ListRemoveAtBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `int list remove`
Removes the first occurrence of a int value from the target list when Executed. See ListRemoveBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> | Reference to the target list variable. |
| Value | int | Item to remove. |

## `int list set at`
Overwrites the int item at a given index in the target list when Executed. See ListSetAtBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | int | New item. |

## `int list set`
Replaces the entire contents of the target int list with another list when Executed. See ListSetBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> | Reference to the target list variable. |
| Value | List<int> | List whose items replace List's contents (typically an expression returning a list). |

## `int list add range`
Appends every item from another int list to the target list when Executed. See ListAddRangeBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> | Reference to the target list variable. |
| Other | List<int> | List whose items will be appended to List. |

## `int list clear`
Removes all items from the target int list when Executed. See ListClearBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> | Reference to the target list variable. |

## `int list loop trigger`
Iterates a int list when Executed, firing listeners once per element. See ListLoopTrigger.

**Role:** Executable (valid `Listeners:` target; also a trigger — emits to its own listeners).

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

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> | Reference to the target list variable. |
| Value | float | Item to append. |

## `float list insert`
Inserts a float value into the target list at a given index when Executed. See ListInsertBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> | Reference to the target list variable. |
| Index | int | Zero-based position to insert at. Valid range is [0, Count]. |
| Value | float | Item to insert. |

## `float list remove at`
Removes the float item at a given index from the target list when Executed. See ListRemoveAtBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `float list remove`
Removes the first occurrence of a float value from the target list when Executed. See ListRemoveBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> | Reference to the target list variable. |
| Value | float | Item to remove. |

## `float list set at`
Overwrites the float item at a given index in the target list when Executed. See ListSetAtBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | float | New item. |

## `float list set`
Replaces the entire contents of the target float list with another list when Executed. See ListSetBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> | Reference to the target list variable. |
| Value | List<float> | List whose items replace List's contents (typically an expression returning a list). |

## `float list add range`
Appends every item from another float list to the target list when Executed. See ListAddRangeBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> | Reference to the target list variable. |
| Other | List<float> | List whose items will be appended to List. |

## `float list clear`
Removes all items from the target float list when Executed. See ListClearBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> | Reference to the target list variable. |

## `float list loop trigger`
Iterates a float list when Executed, firing listeners once per element. See ListLoopTrigger.

**Role:** Executable (valid `Listeners:` target; also a trigger — emits to its own listeners).

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

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> | Reference to the target list variable. |
| Value | bool | Item to append. |

## `bool list insert`
Inserts a bool value into the target list at a given index when Executed. See ListInsertBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> | Reference to the target list variable. |
| Index | int | Zero-based position to insert at. Valid range is [0, Count]. |
| Value | bool | Item to insert. |

## `bool list remove at`
Removes the bool item at a given index from the target list when Executed. See ListRemoveAtBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `bool list remove`
Removes the first occurrence of a bool value from the target list when Executed. See ListRemoveBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> | Reference to the target list variable. |
| Value | bool | Item to remove. |

## `bool list set at`
Overwrites the bool item at a given index in the target list when Executed. See ListSetAtBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | bool | New item. |

## `bool list set`
Replaces the entire contents of the target bool list with another list when Executed. See ListSetBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> | Reference to the target list variable. |
| Value | List<bool> | List whose items replace List's contents (typically an expression returning a list). |

## `bool list add range`
Appends every item from another bool list to the target list when Executed. See ListAddRangeBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> | Reference to the target list variable. |
| Other | List<bool> | List whose items will be appended to List. |

## `bool list clear`
Removes all items from the target bool list when Executed. See ListClearBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> | Reference to the target list variable. |

## `bool list loop trigger`
Iterates a bool list when Executed, firing listeners once per element. See ListLoopTrigger.

**Role:** Executable (valid `Listeners:` target; also a trigger — emits to its own listeners).

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

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> | Reference to the target list variable. |
| Value | string | Item to append. |

## `string list insert`
Inserts a string value into the target list at a given index when Executed. See ListInsertBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> | Reference to the target list variable. |
| Index | int | Zero-based position to insert at. Valid range is [0, Count]. |
| Value | string | Item to insert. |

## `string list remove at`
Removes the string item at a given index from the target list when Executed. See ListRemoveAtBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `string list remove`
Removes the first occurrence of a string value from the target list when Executed. See ListRemoveBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> | Reference to the target list variable. |
| Value | string | Item to remove. |

## `string list set at`
Overwrites the string item at a given index in the target list when Executed. See ListSetAtBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | string | New item. |

## `string list set`
Replaces the entire contents of the target string list with another list when Executed. See ListSetBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> | Reference to the target list variable. |
| Value | List<string> | List whose items replace List's contents (typically an expression returning a list). |

## `string list add range`
Appends every item from another string list to the target list when Executed. See ListAddRangeBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> | Reference to the target list variable. |
| Other | List<string> | List whose items will be appended to List. |

## `string list clear`
Removes all items from the target string list when Executed. See ListClearBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> | Reference to the target list variable. |

## `string list loop trigger`
Iterates a string list when Executed, firing listeners once per element. See ListLoopTrigger.

**Role:** Executable (valid `Listeners:` target; also a trigger — emits to its own listeners).

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

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the target list variable. |
| Value | Color | Item to append. |

## `colour list insert`
Inserts a Color value into the target list at a given index when Executed. See ListInsertBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the target list variable. |
| Index | int | Zero-based position to insert at. Valid range is [0, Count]. |
| Value | Color | Item to insert. |

## `colour list remove at`
Removes the Color item at a given index from the target list when Executed. See ListRemoveAtBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `colour list remove`
Removes the first occurrence of a Color value from the target list when Executed. See ListRemoveBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the target list variable. |
| Value | Color | Item to remove. |

## `colour list set at`
Overwrites the Color item at a given index in the target list when Executed. See ListSetAtBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | Color | New item. |

## `colour list set`
Replaces the entire contents of the target Color list with another list when Executed. See ListSetBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the target list variable. |
| Value | List<Color> | List whose items replace List's contents (typically an expression returning a list). |

## `colour list add range`
Appends every item from another Color list to the target list when Executed. See ListAddRangeBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the target list variable. |
| Other | List<Color> | List whose items will be appended to List. |

## `colour list clear`
Removes all items from the target Color list when Executed. See ListClearBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the target list variable. |

## `colour list loop trigger`
Iterates a Color list when Executed, firing listeners once per element. See ListLoopTrigger.

**Role:** Executable (valid `Listeners:` target; also a trigger — emits to its own listeners).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> | Reference to the list to iterate over. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| item | T | The current element of the list. |
| index | int | Zero-based position of the current element. |

## `record list add`
Appends a record value to the end of the target list when Executed. See ListAddBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Record> | Reference to the target list variable. |
| Value | Record | Item to append. |

## `record list insert`
Inserts a record value into the target list at a given index when Executed. See ListInsertBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Record> | Reference to the target list variable. |
| Index | int | Zero-based position to insert at. Valid range is [0, Count]. |
| Value | Record | Item to insert. |

## `record list remove at`
Removes the record item at a given index from the target list when Executed. See ListRemoveAtBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Record> | Reference to the target list variable. |
| Index | int | Zero-based position to remove from. |

## `record list remove`
Removes the first occurrence (by reference identity) of a record from the target list when Executed. See ListRemoveBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Record> | Reference to the target list variable. |
| Value | Record | Item to remove. |

## `record list set at`
Overwrites the record item at a given index in the target list when Executed. See ListSetAtBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Record> | Reference to the target list variable. |
| Index | int | Zero-based position to overwrite. |
| Value | Record | New item. |

## `record list set`
Replaces the entire contents of the target record list with another list when Executed. See ListSetBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Record> | Reference to the target list variable. |
| Value | List<Record> | List whose items replace List's contents (typically an expression returning a list). |

## `record list add range`
Appends every item from another record list to the target list when Executed. See ListAddRangeBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Record> | Reference to the target list variable. |
| Other | List<Record> | List whose items will be appended to List. |

## `record list clear`
Removes all items from the target record list when Executed. See ListClearBehaviour.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Record> | Reference to the target list variable. |

## `record list loop trigger`
Iterates a record list when Executed, firing listeners once per element. See ListLoopTrigger.

**Role:** Executable (valid `Listeners:` target; also a trigger — emits to its own listeners).

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Record> | Reference to the list to iterate over. |

### Outputs

| Name | Type | Description |
|------|------|-------------|
| item | T | The current element of the list. |
| index | int | Zero-based position of the current element. |

## `set active`
Sets the entity GameObject's active state to the Active value when Executed by an upstream trigger.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Active | bool | Boolean applied to the entity's active state on each Execute; true activates, false deactivates. |

## `set behaviour enabled`
Sets the enabled state of one or more target behaviours to the Enabled value when Executed by an upstream trigger.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Targets | IReadOnlyList<ListenerInfo> | The behaviour(s) to enable/disable — a list of listener-style references (EntityId + BehaviourId, EntityTag, or BehaviourTag). Tag references re-query live state on each Execute, so they pick up matching behaviours added after build. Targets need not be executable, so self-driven behaviours (e.g. velocity) can be toggled. |
| Enabled | bool | Boolean applied to each target's enabled state on every Execute; true enables, false disables. Disabling stops a behaviour's Unity callbacks (Update etc.), so it halts self-driven behaviours but does not block one from being invoked by a listener. |

## `set timescale`
Sets the game clock's time scale when Executed by an upstream trigger. A scale of 0 pauses gameplay, 0.5 is slow-motion, 1 is normal speed.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Scale | float | Playback rate applied to the shared game clock; 0 pauses, 0.5 halves speed, 1 is normal. Negative values are clamped to 0. |

## `toggle active`
Flips the entity GameObject's active state each time it is Executed by an upstream trigger.

**Role:** Executable (valid `Listeners:` target).

No properties.

## `toggle behaviour enabled`
Flips the enabled state of one or more target behaviours each time it is Executed by an upstream trigger.

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Targets | IReadOnlyList<ListenerInfo> | The behaviour(s) to toggle — a list of listener-style references (EntityId + BehaviourId, EntityTag, or BehaviourTag). Tag references re-query live state on each Execute, so they pick up matching behaviours added after build. Targets need not be executable, so self-driven behaviours (e.g. velocity) can be toggled. Each target is flipped relative to its own current state. |

## `sprite`
Renders a 2D sprite as a child of the entity, optionally rescaled to Size.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Sprite | Sprite | Asset reference to the sprite to display. |
| Size | Vector3 | Target world-space size in units; the sprite is scaled to fit. |

## `voxel mesh`
Renders a voxel mesh asset as a child of the entity.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Mesh | Mesh | Asset reference to the Mesh to display. |
| Scale | Vector3 | Optional local-space scale multiplier applied to the child renderer. |

## `primitive`
Adds a 3D primitive mesh (chosen by Shape) as a child of the entity.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Shape | PrimitiveType | Which primitive to create — one of "cube", "sphere", "capsule", "cylinder", "plane", "quad" (defaults to "cube"). |
| Colour | Color | Optional tint applied to the primitive's material. |
| Size | Vector3 | Optional local scale of the primitive child. |

## `light`
Adds a realtime UnityEngine.Light to the entity so a 3D scene is lit
            (without one, primitive meshes render near-black under URP's Lit shader).

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Type | LightKind | One of "directional", "point", "spot" (defaults to "directional"). |
| Colour | Color | Optional light colour (defaults to white). |
| Intensity | float | Optional brightness multiplier (defaults to 1). |
| Range | float | Optional reach in world units for point/spot lights (defaults to 10). |
| SpotAngle | float | Optional cone angle in degrees for spot lights (defaults to 30). |

## `audio source`
Plays an audio clip when Executed (or on start, if configured).

**Role:** Executable (valid `Listeners:` target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Clip | AudioClip | Asset reference to the audio clip to play. |
| PlayOnStart | bool | When true the clip plays automatically when the entity is created. |
| Loop | bool | When true the clip loops once started. |

## `sphere gizmo`
Debug-draws a sphere gizmo at the entity's position. Editor-only: gizmos render in the Scene view (or the Game view with Gizmos enabled), never in a built player or the default Game view — use `primitive` for geometry that renders in-game.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Radius | float | Sphere radius in world units. |
| IsWire | bool | When true draws an outline; when false draws a filled sphere. |
| Colour | Color | Gizmo colour. |

## `cube gizmo`
Debug-draws a cube gizmo at the entity's position. Editor-only: gizmos render in the Scene view (or the Game view with Gizmos enabled), never in a built player or the default Game view — use `primitive` for geometry that renders in-game.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Size | Vector3 | Cube dimensions in world units. |
| IsWire | bool | When true draws an outline; when false draws a filled cube. |
| Colour | Color | Gizmo colour. |

## `line gizmo`
Debug-draws a line gizmo between two points in the entity's local transform space. Editor-only: gizmos render in the Scene view (or the Game view with Gizmos enabled), never in a built player or the default Game view — use `primitive` for geometry that renders in-game.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Start | Vector3 | Line start point in local transform space. |
| End | Vector3 | Line end point in local transform space. |
| Colour | Color | Gizmo colour. |

## `ui canvas`
Roots a UI tree: adds a screen-space Canvas that scales with screen size. Place child UI
            entities (containers, labels, buttons) under this entity to compose the interface.

**Role:** Continuous / passive (runs itself; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| MatchWidthOrHeight | float | CanvasScaler match (0 = match width, 1 = match height, 0.5 = balanced). |
| ReferenceResolution | Vector3 | Design resolution the UI scales from, as a vector (X = width, Y = height). |

## `ui container`
Groups child UI entities. By default it arranges them in a vertical or horizontal stack
            using a uGUI layout group so UIs reflow responsively without hand-placed coordinates; with
            Direction "none" it adds no layout group and children are positioned manually.

**Role:** Continuous / passive (runs itself; not a listener target).

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

**Role:** Continuous / passive (runs itself; not a listener target).

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

**Role:** Trigger (event source — emits to listeners; not a listener target).

### Properties

| Name | Type | Description |
|------|------|-------------|
| Label | string | Button caption (re-read each frame). |
| PreferredWidth | float | Preferred width for the parent layout (omit for a sensible default). |
| PreferredHeight | float | Preferred height for the parent layout (omit for a sensible default). |

## `ui slider`
A uGUI slider. Acts as a trigger: notifies its listeners whenever the value changes.

**Role:** Trigger (event source — emits to listeners; not a listener target).

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

## Doc-gen warnings

- `grid mover`: property `Direction` on `GridMoverInfo` is missing from `GridMover`'s `Properties:` block.
- `grid mover`: property `Speed` on `GridMoverInfo` is missing from `GridMover`'s `Properties:` block.
- `grid mover`: property `AgentRadius` on `GridMoverInfo` is missing from `GridMover`'s `Properties:` block.
