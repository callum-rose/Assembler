# Behaviours

Generated from `Assembler.Behaviours` XML doc comments. Each behaviour's description, property meanings, and trigger outputs are authored on the corresponding `GameBehaviour` MonoBehaviour; property names and types are reflected from the matching `*Info` record.

## `box collider`
_No summary — add `<summary>` on AutoAddBoxColliderBehaviour._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Size | Vector3 |  |
| IsTrigger | bool |  |

## `sphere collider`
_No summary — add `<summary>` on AutoAddSphereColliderBehaviour._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Radius | float |  |
| IsTrigger | bool |  |

## `capsule collider`
_No summary — add `<summary>` on AutoAddCapsuleColliderBehaviour._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Radius | float |  |
| Height | float |  |
| Direction | int |  |
| IsTrigger | bool |  |

## `mesh collider`
_No summary — add `<summary>` on AutoAddMeshColliderBehaviour._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Convex | bool |  |
| IsTrigger | bool |  |

## `rigidbody`
_No summary — add `<summary>` on RigidbodyBehaviour._

### Properties

| Name | Type | Description |
|------|------|-------------|
| UseGravity | bool |  |
| IsKinematic | bool |  |
| Mass | float |  |
| LinearDamping | float |  |
| AngularDamping | float |  |
| FreezePosition | Vector3 |  |
| FreezeRotation | Vector3 |  |

## `add force`
_No summary — add `<summary>` on AddForceBehaviour._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Force | Vector3 |  |

## `add impulse`
_No summary — add `<summary>` on AddImpulseBehaviour._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Impulse | Vector3 |  |

## `add torque`
_No summary — add `<summary>` on AddTorqueBehaviour._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Torque | Vector3 |  |

## `set velocity`
_No summary — add `<summary>` on SetVelocityBehaviour._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Velocity | Vector3 |  |

## `set angular velocity`
_No summary — add `<summary>` on SetAngularVelocityBehaviour._

### Properties

| Name | Type | Description |
|------|------|-------------|
| AngularVelocity | Vector3 |  |

## `velocity`
_No summary — add `<summary>` on Velocity._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Velocity | Vector3 |  |

## `acceleration`
_No summary — add `<summary>` on Acceleration._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Acceleration | Vector3 |  |

## `translate`
_No summary — add `<summary>` on Translate._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Displacement | Vector3 |  |

## `angular velocity`
_No summary — add `<summary>` on AngularVelocity._

### Properties

| Name | Type | Description |
|------|------|-------------|
| AngularVelocity | Vector3 |  |

## `rotate`
_No summary — add `<summary>` on Rotate._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Displacement | Vector3 |  |

## `rotation setter`
_No summary — add `<summary>` on SetRotation._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Rotation | Vector3 |  |

## `move animation`
_No summary — add `<summary>` on MoveAnimation._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Start | Vector3 |  |
| End | Vector3 |  |
| Duration | float |  |
| Easing | string |  |

## `scale animation`
_No summary — add `<summary>` on ScaleAnimation._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Start | Vector3 |  |
| End | Vector3 |  |
| Duration | float |  |
| Easing | string |  |

## `rotate animation`
_No summary — add `<summary>` on RotateAnimation._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Start | Vector3 |  |
| End | Vector3 |  |
| Duration | float |  |
| Easing | string |  |

## `key hold trigger`
_No summary — add `<summary>` on KeyHoldTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Key | string |  |

## `key down trigger`
_No summary — add `<summary>` on KeyDownTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Key | string |  |

## `key up trigger`
_No summary — add `<summary>` on KeyUpTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Key | string |  |

## `mouse button trigger`
_No summary — add `<summary>` on MouseButtonTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Button | int |  |
| Phase | string |  |

## `mouse position trigger`
_No summary — add `<summary>` on MousePositionTrigger._

No properties.

## `scroll wheel trigger`
_No summary — add `<summary>` on ScrollWheelTrigger._

No properties.

## `axis trigger`
_No summary — add `<summary>` on AxisTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| XAxis | string |  |
| YAxis | string |  |

## `gamepad button trigger`
_No summary — add `<summary>` on GamepadButtonTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Button | string |  |
| Mode | string |  |

## `tap trigger`
_No summary — add `<summary>` on Tap._

No properties.

## `double tap trigger`
_No summary — add `<summary>` on DoubleTap._

No properties.

## `long press trigger`
_No summary — add `<summary>` on LongPress._

No properties.

## `swipe trigger`
_No summary — add `<summary>` on Swipe._

No properties.

## `drag trigger`
_No summary — add `<summary>` on Drag._

No properties.

## `pinch trigger`
_No summary — add `<summary>` on Pinch._

No properties.

## `rotate trigger`
_No summary — add `<summary>` on Rotate._

No properties.

## `timer trigger`
_No summary — add `<summary>` on TimerTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Delay | float |  |

## `deferred trigger`
_No summary — add `<summary>` on DeferredTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Delay | float |  |

## `debounced trigger`
_No summary — add `<summary>` on DebouncedTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Interval | float |  |

## `throttled trigger`
_No summary — add `<summary>` on ThrottledTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Rate | float |  |

## `on start trigger`
_No summary — add `<summary>` on OnStartTrigger._

No properties.

## `interval trigger`
_No summary — add `<summary>` on IntervalTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Interval | float |  |
| Count | int |  |
| AutoStart | bool |  |

## `every frame trigger`
_No summary — add `<summary>` on EveryFrameTrigger._

No properties.

## `collision enter trigger`
_No summary — add `<summary>` on CollisionEnter._

### Properties

| Name | Type | Description |
|------|------|-------------|
| TagsToDetect | IReadOnlyList<string> |  |

## `trigger enter trigger`
_No summary — add `<summary>` on TriggerEnter._

### Properties

| Name | Type | Description |
|------|------|-------------|
| TagsToDetect | IReadOnlyList<string> |  |

## `trigger exit trigger`
_No summary — add `<summary>` on TriggerExit._

### Properties

| Name | Type | Description |
|------|------|-------------|
| TagsToDetect | IReadOnlyList<string> |  |

## `collision exit trigger`
_No summary — add `<summary>` on CollisionExit._

### Properties

| Name | Type | Description |
|------|------|-------------|
| TagsToDetect | IReadOnlyList<string> |  |

## `collision stay trigger`
_No summary — add `<summary>` on CollisionStay._

### Properties

| Name | Type | Description |
|------|------|-------------|
| TagsToDetect | IReadOnlyList<string> |  |

## `spawner`
_No summary — add `<summary>` on SpawnerBehaviour._

### Properties

| Name | Type | Description |
|------|------|-------------|
| TemplateId | string |  |
| Position | Vector3 |  |
| Rotation | Vector3 |  |
| Parameters | IReadOnlyDictionary<string, ValueSource<object>> |  |

## `destroy`
_No summary — add `<summary>` on DestroyBehaviour._

No properties.

## `position setter`
_No summary — add `<summary>` on SetPosition._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Position | Vector3 |  |

## `camera`
_No summary — add `<summary>` on CameraBehaviour._

### Properties

| Name | Type | Description |
|------|------|-------------|
| View | string |  |
| Size | float |  |

## `condition gate`
_No summary — add `<summary>` on ConditionGate._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Condition | bool |  |

## `inverse condition gate`
_No summary — add `<summary>` on InverseConditionGate._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Condition | bool |  |

## `exclusive trigger`
_No summary — add `<summary>` on ExclusiveTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Group | string |  |

## `vector variable setter`
_No summary — add `<summary>` on Vector3Setter._

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | Vector3 |  |
| Value | Vector3 |  |

## `int variable setter`
_No summary — add `<summary>` on IntSetter._

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | int |  |
| Value | int |  |

## `float variable setter`
_No summary — add `<summary>` on FloatSetter._

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | float |  |
| Value | float |  |

## `bool variable setter`
_No summary — add `<summary>` on BoolSetter._

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | bool |  |
| Value | bool |  |

## `string variable setter`
_No summary — add `<summary>` on StringSetter._

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | string |  |
| Value | string |  |

## `colour variable setter`
_No summary — add `<summary>` on ColourSetter._

### Properties

| Name | Type | Description |
|------|------|-------------|
| VariableId | Color |  |
| Value | Color |  |

## `vector list add`
_No summary — add `<summary>` on Vector3ListAdd._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> |  |
| Value | Vector3 |  |

## `vector list insert`
_No summary — add `<summary>` on Vector3ListInsert._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> |  |
| Index | int |  |
| Value | Vector3 |  |

## `vector list remove at`
_No summary — add `<summary>` on Vector3ListRemoveAt._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> |  |
| Index | int |  |

## `vector list remove`
_No summary — add `<summary>` on Vector3ListRemove._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> |  |
| Value | Vector3 |  |

## `vector list set at`
_No summary — add `<summary>` on Vector3ListSetAt._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> |  |
| Index | int |  |
| Value | Vector3 |  |

## `vector list set`
_No summary — add `<summary>` on Vector3ListSet._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> |  |
| Value | List<Vector3> |  |

## `vector list add range`
_No summary — add `<summary>` on Vector3ListAddRange._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> |  |
| Other | List<Vector3> |  |

## `vector list clear`
_No summary — add `<summary>` on Vector3ListClear._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> |  |

## `vector list loop trigger`
_No summary — add `<summary>` on Vector3ListLoopTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Vector3> |  |

## `int list add`
_No summary — add `<summary>` on IntListAdd._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> |  |
| Value | int |  |

## `int list insert`
_No summary — add `<summary>` on IntListInsert._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> |  |
| Index | int |  |
| Value | int |  |

## `int list remove at`
_No summary — add `<summary>` on IntListRemoveAt._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> |  |
| Index | int |  |

## `int list remove`
_No summary — add `<summary>` on IntListRemove._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> |  |
| Value | int |  |

## `int list set at`
_No summary — add `<summary>` on IntListSetAt._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> |  |
| Index | int |  |
| Value | int |  |

## `int list set`
_No summary — add `<summary>` on IntListSet._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> |  |
| Value | List<int> |  |

## `int list add range`
_No summary — add `<summary>` on IntListAddRange._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> |  |
| Other | List<int> |  |

## `int list clear`
_No summary — add `<summary>` on IntListClear._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> |  |

## `int list loop trigger`
_No summary — add `<summary>` on IntListLoopTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<int> |  |

## `float list add`
_No summary — add `<summary>` on FloatListAdd._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> |  |
| Value | float |  |

## `float list insert`
_No summary — add `<summary>` on FloatListInsert._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> |  |
| Index | int |  |
| Value | float |  |

## `float list remove at`
_No summary — add `<summary>` on FloatListRemoveAt._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> |  |
| Index | int |  |

## `float list remove`
_No summary — add `<summary>` on FloatListRemove._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> |  |
| Value | float |  |

## `float list set at`
_No summary — add `<summary>` on FloatListSetAt._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> |  |
| Index | int |  |
| Value | float |  |

## `float list set`
_No summary — add `<summary>` on FloatListSet._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> |  |
| Value | List<float> |  |

## `float list add range`
_No summary — add `<summary>` on FloatListAddRange._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> |  |
| Other | List<float> |  |

## `float list clear`
_No summary — add `<summary>` on FloatListClear._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> |  |

## `float list loop trigger`
_No summary — add `<summary>` on FloatListLoopTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<float> |  |

## `bool list add`
_No summary — add `<summary>` on BoolListAdd._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> |  |
| Value | bool |  |

## `bool list insert`
_No summary — add `<summary>` on BoolListInsert._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> |  |
| Index | int |  |
| Value | bool |  |

## `bool list remove at`
_No summary — add `<summary>` on BoolListRemoveAt._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> |  |
| Index | int |  |

## `bool list remove`
_No summary — add `<summary>` on BoolListRemove._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> |  |
| Value | bool |  |

## `bool list set at`
_No summary — add `<summary>` on BoolListSetAt._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> |  |
| Index | int |  |
| Value | bool |  |

## `bool list set`
_No summary — add `<summary>` on BoolListSet._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> |  |
| Value | List<bool> |  |

## `bool list add range`
_No summary — add `<summary>` on BoolListAddRange._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> |  |
| Other | List<bool> |  |

## `bool list clear`
_No summary — add `<summary>` on BoolListClear._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> |  |

## `bool list loop trigger`
_No summary — add `<summary>` on BoolListLoopTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<bool> |  |

## `string list add`
_No summary — add `<summary>` on StringListAdd._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> |  |
| Value | string |  |

## `string list insert`
_No summary — add `<summary>` on StringListInsert._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> |  |
| Index | int |  |
| Value | string |  |

## `string list remove at`
_No summary — add `<summary>` on StringListRemoveAt._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> |  |
| Index | int |  |

## `string list remove`
_No summary — add `<summary>` on StringListRemove._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> |  |
| Value | string |  |

## `string list set at`
_No summary — add `<summary>` on StringListSetAt._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> |  |
| Index | int |  |
| Value | string |  |

## `string list set`
_No summary — add `<summary>` on StringListSet._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> |  |
| Value | List<string> |  |

## `string list add range`
_No summary — add `<summary>` on StringListAddRange._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> |  |
| Other | List<string> |  |

## `string list clear`
_No summary — add `<summary>` on StringListClear._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> |  |

## `string list loop trigger`
_No summary — add `<summary>` on StringListLoopTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<string> |  |

## `colour list add`
_No summary — add `<summary>` on ColourListAdd._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> |  |
| Value | Color |  |

## `colour list insert`
_No summary — add `<summary>` on ColourListInsert._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> |  |
| Index | int |  |
| Value | Color |  |

## `colour list remove at`
_No summary — add `<summary>` on ColourListRemoveAt._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> |  |
| Index | int |  |

## `colour list remove`
_No summary — add `<summary>` on ColourListRemove._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> |  |
| Value | Color |  |

## `colour list set at`
_No summary — add `<summary>` on ColourListSetAt._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> |  |
| Index | int |  |
| Value | Color |  |

## `colour list set`
_No summary — add `<summary>` on ColourListSet._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> |  |
| Value | List<Color> |  |

## `colour list add range`
_No summary — add `<summary>` on ColourListAddRange._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> |  |
| Other | List<Color> |  |

## `colour list clear`
_No summary — add `<summary>` on ColourListClear._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> |  |

## `colour list loop trigger`
_No summary — add `<summary>` on ColourListLoopTrigger._

### Properties

| Name | Type | Description |
|------|------|-------------|
| List | List<Color> |  |

## `active poll`
_No summary — add `<summary>` on ActivePoll._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Active | bool |  |

## `set active`
_No summary — add `<summary>` on SetActive._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Active | bool |  |

## `toggle active`
_No summary — add `<summary>` on ToggleActive._

No properties.

## `sprite`
_No summary — add `<summary>` on SpriteBehaviour._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Sprite | Sprite |  |
| Size | Vector2 |  |

## `voxel mesh`
_No summary — add `<summary>` on VoxelMesh._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Mesh | Mesh |  |
| Scale | Vector3 |  |

## `audio source`
_No summary — add `<summary>` on AudioSourceBehaviour._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Clip | AudioClip |  |
| PlayOnStart | bool |  |
| Loop | bool |  |

## `sphere gizmo`
_No summary — add `<summary>` on SphereGizmoBehaviour._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Radius | float |  |
| IsWire | bool |  |
| Colour | Color |  |

## `cube gizmo`
_No summary — add `<summary>` on CubeGizmoBehaviour._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Size | Vector3 |  |
| IsWire | bool |  |
| Colour | Color |  |

## `line gizmo`
_No summary — add `<summary>` on LineGizmoBehaviour._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Start | Vector3 |  |
| End | Vector3 |  |
| Colour | Color |  |

## `text label`
_No summary — add `<summary>` on TextLabel._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Text | string |  |
| Label | string |  |
| FontSize | int |  |
| Rect | ScreenRect |  |

## `progress bar`
_No summary — add `<summary>` on ProgressBar._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Value | float |  |
| Rect | ScreenRect |  |

## `ui image`
_No summary — add `<summary>` on UIImage._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Colour | Color |  |
| Rect | ScreenRect |  |

## `ui button`
_No summary — add `<summary>` on UIButton._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Label | string |  |
| Rect | ScreenRect |  |

## `ui toggle`
_No summary — add `<summary>` on UIToggle._

### Properties

| Name | Type | Description |
|------|------|-------------|
| InitialValue | bool |  |
| Label | string |  |
| Rect | ScreenRect |  |

## `ui slider`
_No summary — add `<summary>` on UISlider._

### Properties

| Name | Type | Description |
|------|------|-------------|
| InitialValue | float |  |
| MinValue | float |  |
| MaxValue | float |  |
| Rect | ScreenRect |  |

## `ui input field`
_No summary — add `<summary>` on UIInputField._

### Properties

| Name | Type | Description |
|------|------|-------------|
| Rect | ScreenRect |  |

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

- `box collider`: property `Size` on `BoxColliderInfo` is missing from `AutoAddBoxColliderBehaviour`'s `Properties:` block.
- `box collider`: property `IsTrigger` on `BoxColliderInfo` is missing from `AutoAddBoxColliderBehaviour`'s `Properties:` block.
- `sphere collider`: property `Radius` on `SphereColliderInfo` is missing from `AutoAddSphereColliderBehaviour`'s `Properties:` block.
- `sphere collider`: property `IsTrigger` on `SphereColliderInfo` is missing from `AutoAddSphereColliderBehaviour`'s `Properties:` block.
- `capsule collider`: property `Radius` on `CapsuleColliderInfo` is missing from `AutoAddCapsuleColliderBehaviour`'s `Properties:` block.
- `capsule collider`: property `Height` on `CapsuleColliderInfo` is missing from `AutoAddCapsuleColliderBehaviour`'s `Properties:` block.
- `capsule collider`: property `Direction` on `CapsuleColliderInfo` is missing from `AutoAddCapsuleColliderBehaviour`'s `Properties:` block.
- `capsule collider`: property `IsTrigger` on `CapsuleColliderInfo` is missing from `AutoAddCapsuleColliderBehaviour`'s `Properties:` block.
- `mesh collider`: property `Convex` on `MeshColliderInfo` is missing from `AutoAddMeshColliderBehaviour`'s `Properties:` block.
- `mesh collider`: property `IsTrigger` on `MeshColliderInfo` is missing from `AutoAddMeshColliderBehaviour`'s `Properties:` block.
- `rigidbody`: property `UseGravity` on `RigidbodyInfo` is missing from `RigidbodyBehaviour`'s `Properties:` block.
- `rigidbody`: property `IsKinematic` on `RigidbodyInfo` is missing from `RigidbodyBehaviour`'s `Properties:` block.
- `rigidbody`: property `Mass` on `RigidbodyInfo` is missing from `RigidbodyBehaviour`'s `Properties:` block.
- `rigidbody`: property `LinearDamping` on `RigidbodyInfo` is missing from `RigidbodyBehaviour`'s `Properties:` block.
- `rigidbody`: property `AngularDamping` on `RigidbodyInfo` is missing from `RigidbodyBehaviour`'s `Properties:` block.
- `rigidbody`: property `FreezePosition` on `RigidbodyInfo` is missing from `RigidbodyBehaviour`'s `Properties:` block.
- `rigidbody`: property `FreezeRotation` on `RigidbodyInfo` is missing from `RigidbodyBehaviour`'s `Properties:` block.
- `add force`: property `Force` on `AddForceInfo` is missing from `AddForceBehaviour`'s `Properties:` block.
- `add impulse`: property `Impulse` on `AddImpulseInfo` is missing from `AddImpulseBehaviour`'s `Properties:` block.
- `add torque`: property `Torque` on `AddTorqueInfo` is missing from `AddTorqueBehaviour`'s `Properties:` block.
- `set velocity`: property `Velocity` on `SetVelocityInfo` is missing from `SetVelocityBehaviour`'s `Properties:` block.
- `set angular velocity`: property `AngularVelocity` on `SetAngularVelocityInfo` is missing from `SetAngularVelocityBehaviour`'s `Properties:` block.
- `velocity`: property `Velocity` on `VelocityInfo` is missing from `Velocity`'s `Properties:` block.
- `acceleration`: property `Acceleration` on `AccelerationInfo` is missing from `Acceleration`'s `Properties:` block.
- `translate`: property `Displacement` on `TranslateInfo` is missing from `Translate`'s `Properties:` block.
- `angular velocity`: property `AngularVelocity` on `AngularVelocityInfo` is missing from `AngularVelocity`'s `Properties:` block.
- `rotate`: property `Displacement` on `RotateInfo` is missing from `Rotate`'s `Properties:` block.
- `rotation setter`: property `Rotation` on `SetRotationInfo` is missing from `SetRotation`'s `Properties:` block.
- `move animation`: property `Start` on `MoveAnimationInfo` is missing from `MoveAnimation`'s `Properties:` block.
- `move animation`: property `End` on `MoveAnimationInfo` is missing from `MoveAnimation`'s `Properties:` block.
- `move animation`: property `Duration` on `MoveAnimationInfo` is missing from `MoveAnimation`'s `Properties:` block.
- `move animation`: property `Easing` on `MoveAnimationInfo` is missing from `MoveAnimation`'s `Properties:` block.
- `scale animation`: property `Start` on `ScaleAnimationInfo` is missing from `ScaleAnimation`'s `Properties:` block.
- `scale animation`: property `End` on `ScaleAnimationInfo` is missing from `ScaleAnimation`'s `Properties:` block.
- `scale animation`: property `Duration` on `ScaleAnimationInfo` is missing from `ScaleAnimation`'s `Properties:` block.
- `scale animation`: property `Easing` on `ScaleAnimationInfo` is missing from `ScaleAnimation`'s `Properties:` block.
- `rotate animation`: property `Start` on `RotateAnimationInfo` is missing from `RotateAnimation`'s `Properties:` block.
- `rotate animation`: property `End` on `RotateAnimationInfo` is missing from `RotateAnimation`'s `Properties:` block.
- `rotate animation`: property `Duration` on `RotateAnimationInfo` is missing from `RotateAnimation`'s `Properties:` block.
- `rotate animation`: property `Easing` on `RotateAnimationInfo` is missing from `RotateAnimation`'s `Properties:` block.
- `key hold trigger`: property `Key` on `KeyHoldTriggerInfo` is missing from `KeyHoldTrigger`'s `Properties:` block.
- `key down trigger`: property `Key` on `KeyDownTriggerInfo` is missing from `KeyDownTrigger`'s `Properties:` block.
- `key up trigger`: property `Key` on `KeyUpTriggerInfo` is missing from `KeyUpTrigger`'s `Properties:` block.
- `mouse button trigger`: property `Button` on `MouseButtonTriggerInfo` is missing from `MouseButtonTrigger`'s `Properties:` block.
- `mouse button trigger`: property `Phase` on `MouseButtonTriggerInfo` is missing from `MouseButtonTrigger`'s `Properties:` block.
- `axis trigger`: property `XAxis` on `AxisTriggerInfo` is missing from `AxisTrigger`'s `Properties:` block.
- `axis trigger`: property `YAxis` on `AxisTriggerInfo` is missing from `AxisTrigger`'s `Properties:` block.
- `gamepad button trigger`: property `Button` on `GamepadButtonTriggerInfo` is missing from `GamepadButtonTrigger`'s `Properties:` block.
- `gamepad button trigger`: property `Mode` on `GamepadButtonTriggerInfo` is missing from `GamepadButtonTrigger`'s `Properties:` block.
- `timer trigger`: property `Delay` on `TimerTriggerInfo` is missing from `TimerTrigger`'s `Properties:` block.
- `deferred trigger`: property `Delay` on `DeferredTriggerInfo` is missing from `DeferredTrigger`'s `Properties:` block.
- `debounced trigger`: property `Interval` on `DebouncedTriggerInfo` is missing from `DebouncedTrigger`'s `Properties:` block.
- `throttled trigger`: property `Rate` on `ThrottledTriggerInfo` is missing from `ThrottledTrigger`'s `Properties:` block.
- `interval trigger`: property `Interval` on `IntervalTriggerInfo` is missing from `IntervalTrigger`'s `Properties:` block.
- `interval trigger`: property `Count` on `IntervalTriggerInfo` is missing from `IntervalTrigger`'s `Properties:` block.
- `interval trigger`: property `AutoStart` on `IntervalTriggerInfo` is missing from `IntervalTrigger`'s `Properties:` block.
- `collision enter trigger`: property `TagsToDetect` on `CollisionEnterTriggerInfo` is missing from `CollisionEnter`'s `Properties:` block.
- `trigger enter trigger`: property `TagsToDetect` on `TriggerEnterTriggerInfo` is missing from `TriggerEnter`'s `Properties:` block.
- `trigger exit trigger`: property `TagsToDetect` on `TriggerExitTriggerInfo` is missing from `TriggerExit`'s `Properties:` block.
- `collision exit trigger`: property `TagsToDetect` on `CollisionExitTriggerInfo` is missing from `CollisionExit`'s `Properties:` block.
- `collision stay trigger`: property `TagsToDetect` on `CollisionStayTriggerInfo` is missing from `CollisionStay`'s `Properties:` block.
- `spawner`: property `TemplateId` on `SpawnerInfo` is missing from `SpawnerBehaviour`'s `Properties:` block.
- `spawner`: property `Position` on `SpawnerInfo` is missing from `SpawnerBehaviour`'s `Properties:` block.
- `spawner`: property `Rotation` on `SpawnerInfo` is missing from `SpawnerBehaviour`'s `Properties:` block.
- `spawner`: property `Parameters` on `SpawnerInfo` is missing from `SpawnerBehaviour`'s `Properties:` block.
- `position setter`: property `Position` on `SetPositionInfo` is missing from `SetPosition`'s `Properties:` block.
- `camera`: property `View` on `CameraInfo` is missing from `CameraBehaviour`'s `Properties:` block.
- `camera`: property `Size` on `CameraInfo` is missing from `CameraBehaviour`'s `Properties:` block.
- `condition gate`: property `Condition` on `ConditionGateInfo` is missing from `ConditionGate`'s `Properties:` block.
- `inverse condition gate`: property `Condition` on `InverseConditionGateInfo` is missing from `InverseConditionGate`'s `Properties:` block.
- `exclusive trigger`: property `Group` on `ExclusiveTriggerInfo` is missing from `ExclusiveTrigger`'s `Properties:` block.
- `vector variable setter`: property `VariableId` on `VariableSetterInfo`1` is missing from `Vector3Setter`'s `Properties:` block.
- `vector variable setter`: property `Value` on `VariableSetterInfo`1` is missing from `Vector3Setter`'s `Properties:` block.
- `int variable setter`: property `VariableId` on `VariableSetterInfo`1` is missing from `IntSetter`'s `Properties:` block.
- `int variable setter`: property `Value` on `VariableSetterInfo`1` is missing from `IntSetter`'s `Properties:` block.
- `float variable setter`: property `VariableId` on `VariableSetterInfo`1` is missing from `FloatSetter`'s `Properties:` block.
- `float variable setter`: property `Value` on `VariableSetterInfo`1` is missing from `FloatSetter`'s `Properties:` block.
- `bool variable setter`: property `VariableId` on `VariableSetterInfo`1` is missing from `BoolSetter`'s `Properties:` block.
- `bool variable setter`: property `Value` on `VariableSetterInfo`1` is missing from `BoolSetter`'s `Properties:` block.
- `string variable setter`: property `VariableId` on `VariableSetterInfo`1` is missing from `StringSetter`'s `Properties:` block.
- `string variable setter`: property `Value` on `VariableSetterInfo`1` is missing from `StringSetter`'s `Properties:` block.
- `colour variable setter`: property `VariableId` on `VariableSetterInfo`1` is missing from `ColourSetter`'s `Properties:` block.
- `colour variable setter`: property `Value` on `VariableSetterInfo`1` is missing from `ColourSetter`'s `Properties:` block.
- `vector list add`: property `List` on `ListAddInfo`1` is missing from `Vector3ListAdd`'s `Properties:` block.
- `vector list add`: property `Value` on `ListAddInfo`1` is missing from `Vector3ListAdd`'s `Properties:` block.
- `vector list insert`: property `List` on `ListInsertInfo`1` is missing from `Vector3ListInsert`'s `Properties:` block.
- `vector list insert`: property `Index` on `ListInsertInfo`1` is missing from `Vector3ListInsert`'s `Properties:` block.
- `vector list insert`: property `Value` on `ListInsertInfo`1` is missing from `Vector3ListInsert`'s `Properties:` block.
- `vector list remove at`: property `List` on `ListRemoveAtInfo`1` is missing from `Vector3ListRemoveAt`'s `Properties:` block.
- `vector list remove at`: property `Index` on `ListRemoveAtInfo`1` is missing from `Vector3ListRemoveAt`'s `Properties:` block.
- `vector list remove`: property `List` on `ListRemoveInfo`1` is missing from `Vector3ListRemove`'s `Properties:` block.
- `vector list remove`: property `Value` on `ListRemoveInfo`1` is missing from `Vector3ListRemove`'s `Properties:` block.
- `vector list set at`: property `List` on `ListSetAtInfo`1` is missing from `Vector3ListSetAt`'s `Properties:` block.
- `vector list set at`: property `Index` on `ListSetAtInfo`1` is missing from `Vector3ListSetAt`'s `Properties:` block.
- `vector list set at`: property `Value` on `ListSetAtInfo`1` is missing from `Vector3ListSetAt`'s `Properties:` block.
- `vector list set`: property `List` on `ListSetInfo`1` is missing from `Vector3ListSet`'s `Properties:` block.
- `vector list set`: property `Value` on `ListSetInfo`1` is missing from `Vector3ListSet`'s `Properties:` block.
- `vector list add range`: property `List` on `ListAddRangeInfo`1` is missing from `Vector3ListAddRange`'s `Properties:` block.
- `vector list add range`: property `Other` on `ListAddRangeInfo`1` is missing from `Vector3ListAddRange`'s `Properties:` block.
- `vector list clear`: property `List` on `ListClearInfo`1` is missing from `Vector3ListClear`'s `Properties:` block.
- `vector list loop trigger`: property `List` on `ListLoopTriggerInfo`1` is missing from `Vector3ListLoopTrigger`'s `Properties:` block.
- `int list add`: property `List` on `ListAddInfo`1` is missing from `IntListAdd`'s `Properties:` block.
- `int list add`: property `Value` on `ListAddInfo`1` is missing from `IntListAdd`'s `Properties:` block.
- `int list insert`: property `List` on `ListInsertInfo`1` is missing from `IntListInsert`'s `Properties:` block.
- `int list insert`: property `Index` on `ListInsertInfo`1` is missing from `IntListInsert`'s `Properties:` block.
- `int list insert`: property `Value` on `ListInsertInfo`1` is missing from `IntListInsert`'s `Properties:` block.
- `int list remove at`: property `List` on `ListRemoveAtInfo`1` is missing from `IntListRemoveAt`'s `Properties:` block.
- `int list remove at`: property `Index` on `ListRemoveAtInfo`1` is missing from `IntListRemoveAt`'s `Properties:` block.
- `int list remove`: property `List` on `ListRemoveInfo`1` is missing from `IntListRemove`'s `Properties:` block.
- `int list remove`: property `Value` on `ListRemoveInfo`1` is missing from `IntListRemove`'s `Properties:` block.
- `int list set at`: property `List` on `ListSetAtInfo`1` is missing from `IntListSetAt`'s `Properties:` block.
- `int list set at`: property `Index` on `ListSetAtInfo`1` is missing from `IntListSetAt`'s `Properties:` block.
- `int list set at`: property `Value` on `ListSetAtInfo`1` is missing from `IntListSetAt`'s `Properties:` block.
- `int list set`: property `List` on `ListSetInfo`1` is missing from `IntListSet`'s `Properties:` block.
- `int list set`: property `Value` on `ListSetInfo`1` is missing from `IntListSet`'s `Properties:` block.
- `int list add range`: property `List` on `ListAddRangeInfo`1` is missing from `IntListAddRange`'s `Properties:` block.
- `int list add range`: property `Other` on `ListAddRangeInfo`1` is missing from `IntListAddRange`'s `Properties:` block.
- `int list clear`: property `List` on `ListClearInfo`1` is missing from `IntListClear`'s `Properties:` block.
- `int list loop trigger`: property `List` on `ListLoopTriggerInfo`1` is missing from `IntListLoopTrigger`'s `Properties:` block.
- `float list add`: property `List` on `ListAddInfo`1` is missing from `FloatListAdd`'s `Properties:` block.
- `float list add`: property `Value` on `ListAddInfo`1` is missing from `FloatListAdd`'s `Properties:` block.
- `float list insert`: property `List` on `ListInsertInfo`1` is missing from `FloatListInsert`'s `Properties:` block.
- `float list insert`: property `Index` on `ListInsertInfo`1` is missing from `FloatListInsert`'s `Properties:` block.
- `float list insert`: property `Value` on `ListInsertInfo`1` is missing from `FloatListInsert`'s `Properties:` block.
- `float list remove at`: property `List` on `ListRemoveAtInfo`1` is missing from `FloatListRemoveAt`'s `Properties:` block.
- `float list remove at`: property `Index` on `ListRemoveAtInfo`1` is missing from `FloatListRemoveAt`'s `Properties:` block.
- `float list remove`: property `List` on `ListRemoveInfo`1` is missing from `FloatListRemove`'s `Properties:` block.
- `float list remove`: property `Value` on `ListRemoveInfo`1` is missing from `FloatListRemove`'s `Properties:` block.
- `float list set at`: property `List` on `ListSetAtInfo`1` is missing from `FloatListSetAt`'s `Properties:` block.
- `float list set at`: property `Index` on `ListSetAtInfo`1` is missing from `FloatListSetAt`'s `Properties:` block.
- `float list set at`: property `Value` on `ListSetAtInfo`1` is missing from `FloatListSetAt`'s `Properties:` block.
- `float list set`: property `List` on `ListSetInfo`1` is missing from `FloatListSet`'s `Properties:` block.
- `float list set`: property `Value` on `ListSetInfo`1` is missing from `FloatListSet`'s `Properties:` block.
- `float list add range`: property `List` on `ListAddRangeInfo`1` is missing from `FloatListAddRange`'s `Properties:` block.
- `float list add range`: property `Other` on `ListAddRangeInfo`1` is missing from `FloatListAddRange`'s `Properties:` block.
- `float list clear`: property `List` on `ListClearInfo`1` is missing from `FloatListClear`'s `Properties:` block.
- `float list loop trigger`: property `List` on `ListLoopTriggerInfo`1` is missing from `FloatListLoopTrigger`'s `Properties:` block.
- `bool list add`: property `List` on `ListAddInfo`1` is missing from `BoolListAdd`'s `Properties:` block.
- `bool list add`: property `Value` on `ListAddInfo`1` is missing from `BoolListAdd`'s `Properties:` block.
- `bool list insert`: property `List` on `ListInsertInfo`1` is missing from `BoolListInsert`'s `Properties:` block.
- `bool list insert`: property `Index` on `ListInsertInfo`1` is missing from `BoolListInsert`'s `Properties:` block.
- `bool list insert`: property `Value` on `ListInsertInfo`1` is missing from `BoolListInsert`'s `Properties:` block.
- `bool list remove at`: property `List` on `ListRemoveAtInfo`1` is missing from `BoolListRemoveAt`'s `Properties:` block.
- `bool list remove at`: property `Index` on `ListRemoveAtInfo`1` is missing from `BoolListRemoveAt`'s `Properties:` block.
- `bool list remove`: property `List` on `ListRemoveInfo`1` is missing from `BoolListRemove`'s `Properties:` block.
- `bool list remove`: property `Value` on `ListRemoveInfo`1` is missing from `BoolListRemove`'s `Properties:` block.
- `bool list set at`: property `List` on `ListSetAtInfo`1` is missing from `BoolListSetAt`'s `Properties:` block.
- `bool list set at`: property `Index` on `ListSetAtInfo`1` is missing from `BoolListSetAt`'s `Properties:` block.
- `bool list set at`: property `Value` on `ListSetAtInfo`1` is missing from `BoolListSetAt`'s `Properties:` block.
- `bool list set`: property `List` on `ListSetInfo`1` is missing from `BoolListSet`'s `Properties:` block.
- `bool list set`: property `Value` on `ListSetInfo`1` is missing from `BoolListSet`'s `Properties:` block.
- `bool list add range`: property `List` on `ListAddRangeInfo`1` is missing from `BoolListAddRange`'s `Properties:` block.
- `bool list add range`: property `Other` on `ListAddRangeInfo`1` is missing from `BoolListAddRange`'s `Properties:` block.
- `bool list clear`: property `List` on `ListClearInfo`1` is missing from `BoolListClear`'s `Properties:` block.
- `bool list loop trigger`: property `List` on `ListLoopTriggerInfo`1` is missing from `BoolListLoopTrigger`'s `Properties:` block.
- `string list add`: property `List` on `ListAddInfo`1` is missing from `StringListAdd`'s `Properties:` block.
- `string list add`: property `Value` on `ListAddInfo`1` is missing from `StringListAdd`'s `Properties:` block.
- `string list insert`: property `List` on `ListInsertInfo`1` is missing from `StringListInsert`'s `Properties:` block.
- `string list insert`: property `Index` on `ListInsertInfo`1` is missing from `StringListInsert`'s `Properties:` block.
- `string list insert`: property `Value` on `ListInsertInfo`1` is missing from `StringListInsert`'s `Properties:` block.
- `string list remove at`: property `List` on `ListRemoveAtInfo`1` is missing from `StringListRemoveAt`'s `Properties:` block.
- `string list remove at`: property `Index` on `ListRemoveAtInfo`1` is missing from `StringListRemoveAt`'s `Properties:` block.
- `string list remove`: property `List` on `ListRemoveInfo`1` is missing from `StringListRemove`'s `Properties:` block.
- `string list remove`: property `Value` on `ListRemoveInfo`1` is missing from `StringListRemove`'s `Properties:` block.
- `string list set at`: property `List` on `ListSetAtInfo`1` is missing from `StringListSetAt`'s `Properties:` block.
- `string list set at`: property `Index` on `ListSetAtInfo`1` is missing from `StringListSetAt`'s `Properties:` block.
- `string list set at`: property `Value` on `ListSetAtInfo`1` is missing from `StringListSetAt`'s `Properties:` block.
- `string list set`: property `List` on `ListSetInfo`1` is missing from `StringListSet`'s `Properties:` block.
- `string list set`: property `Value` on `ListSetInfo`1` is missing from `StringListSet`'s `Properties:` block.
- `string list add range`: property `List` on `ListAddRangeInfo`1` is missing from `StringListAddRange`'s `Properties:` block.
- `string list add range`: property `Other` on `ListAddRangeInfo`1` is missing from `StringListAddRange`'s `Properties:` block.
- `string list clear`: property `List` on `ListClearInfo`1` is missing from `StringListClear`'s `Properties:` block.
- `string list loop trigger`: property `List` on `ListLoopTriggerInfo`1` is missing from `StringListLoopTrigger`'s `Properties:` block.
- `colour list add`: property `List` on `ListAddInfo`1` is missing from `ColourListAdd`'s `Properties:` block.
- `colour list add`: property `Value` on `ListAddInfo`1` is missing from `ColourListAdd`'s `Properties:` block.
- `colour list insert`: property `List` on `ListInsertInfo`1` is missing from `ColourListInsert`'s `Properties:` block.
- `colour list insert`: property `Index` on `ListInsertInfo`1` is missing from `ColourListInsert`'s `Properties:` block.
- `colour list insert`: property `Value` on `ListInsertInfo`1` is missing from `ColourListInsert`'s `Properties:` block.
- `colour list remove at`: property `List` on `ListRemoveAtInfo`1` is missing from `ColourListRemoveAt`'s `Properties:` block.
- `colour list remove at`: property `Index` on `ListRemoveAtInfo`1` is missing from `ColourListRemoveAt`'s `Properties:` block.
- `colour list remove`: property `List` on `ListRemoveInfo`1` is missing from `ColourListRemove`'s `Properties:` block.
- `colour list remove`: property `Value` on `ListRemoveInfo`1` is missing from `ColourListRemove`'s `Properties:` block.
- `colour list set at`: property `List` on `ListSetAtInfo`1` is missing from `ColourListSetAt`'s `Properties:` block.
- `colour list set at`: property `Index` on `ListSetAtInfo`1` is missing from `ColourListSetAt`'s `Properties:` block.
- `colour list set at`: property `Value` on `ListSetAtInfo`1` is missing from `ColourListSetAt`'s `Properties:` block.
- `colour list set`: property `List` on `ListSetInfo`1` is missing from `ColourListSet`'s `Properties:` block.
- `colour list set`: property `Value` on `ListSetInfo`1` is missing from `ColourListSet`'s `Properties:` block.
- `colour list add range`: property `List` on `ListAddRangeInfo`1` is missing from `ColourListAddRange`'s `Properties:` block.
- `colour list add range`: property `Other` on `ListAddRangeInfo`1` is missing from `ColourListAddRange`'s `Properties:` block.
- `colour list clear`: property `List` on `ListClearInfo`1` is missing from `ColourListClear`'s `Properties:` block.
- `colour list loop trigger`: property `List` on `ListLoopTriggerInfo`1` is missing from `ColourListLoopTrigger`'s `Properties:` block.
- `active poll`: property `Active` on `ActivePollInfo` is missing from `ActivePoll`'s `Properties:` block.
- `set active`: property `Active` on `SetActiveInfo` is missing from `SetActive`'s `Properties:` block.
- `sprite`: property `Sprite` on `SpriteInfo` is missing from `SpriteBehaviour`'s `Properties:` block.
- `sprite`: property `Size` on `SpriteInfo` is missing from `SpriteBehaviour`'s `Properties:` block.
- `voxel mesh`: property `Mesh` on `VoxelMeshInfo` is missing from `VoxelMesh`'s `Properties:` block.
- `voxel mesh`: property `Scale` on `VoxelMeshInfo` is missing from `VoxelMesh`'s `Properties:` block.
- `audio source`: property `Clip` on `AudioSourceInfo` is missing from `AudioSourceBehaviour`'s `Properties:` block.
- `audio source`: property `PlayOnStart` on `AudioSourceInfo` is missing from `AudioSourceBehaviour`'s `Properties:` block.
- `audio source`: property `Loop` on `AudioSourceInfo` is missing from `AudioSourceBehaviour`'s `Properties:` block.
- `sphere gizmo`: property `Radius` on `SphereGizmoInfo` is missing from `SphereGizmoBehaviour`'s `Properties:` block.
- `sphere gizmo`: property `IsWire` on `SphereGizmoInfo` is missing from `SphereGizmoBehaviour`'s `Properties:` block.
- `sphere gizmo`: property `Colour` on `SphereGizmoInfo` is missing from `SphereGizmoBehaviour`'s `Properties:` block.
- `cube gizmo`: property `Size` on `CubeGizmoInfo` is missing from `CubeGizmoBehaviour`'s `Properties:` block.
- `cube gizmo`: property `IsWire` on `CubeGizmoInfo` is missing from `CubeGizmoBehaviour`'s `Properties:` block.
- `cube gizmo`: property `Colour` on `CubeGizmoInfo` is missing from `CubeGizmoBehaviour`'s `Properties:` block.
- `line gizmo`: property `Start` on `LineGizmoInfo` is missing from `LineGizmoBehaviour`'s `Properties:` block.
- `line gizmo`: property `End` on `LineGizmoInfo` is missing from `LineGizmoBehaviour`'s `Properties:` block.
- `line gizmo`: property `Colour` on `LineGizmoInfo` is missing from `LineGizmoBehaviour`'s `Properties:` block.
- `text label`: property `Text` on `TextLabelInfo` is missing from `TextLabel`'s `Properties:` block.
- `text label`: property `Label` on `TextLabelInfo` is missing from `TextLabel`'s `Properties:` block.
- `text label`: property `FontSize` on `TextLabelInfo` is missing from `TextLabel`'s `Properties:` block.
- `text label`: property `Rect` on `TextLabelInfo` is missing from `TextLabel`'s `Properties:` block.
- `progress bar`: property `Value` on `ProgressBarInfo` is missing from `ProgressBar`'s `Properties:` block.
- `progress bar`: property `Rect` on `ProgressBarInfo` is missing from `ProgressBar`'s `Properties:` block.
- `ui image`: property `Colour` on `UIImageInfo` is missing from `UIImage`'s `Properties:` block.
- `ui image`: property `Rect` on `UIImageInfo` is missing from `UIImage`'s `Properties:` block.
- `ui button`: property `Label` on `UIButtonInfo` is missing from `UIButton`'s `Properties:` block.
- `ui button`: property `Rect` on `UIButtonInfo` is missing from `UIButton`'s `Properties:` block.
- `ui toggle`: property `InitialValue` on `UIToggleInfo` is missing from `UIToggle`'s `Properties:` block.
- `ui toggle`: property `Label` on `UIToggleInfo` is missing from `UIToggle`'s `Properties:` block.
- `ui toggle`: property `Rect` on `UIToggleInfo` is missing from `UIToggle`'s `Properties:` block.
- `ui slider`: property `InitialValue` on `UISliderInfo` is missing from `UISlider`'s `Properties:` block.
- `ui slider`: property `MinValue` on `UISliderInfo` is missing from `UISlider`'s `Properties:` block.
- `ui slider`: property `MaxValue` on `UISliderInfo` is missing from `UISlider`'s `Properties:` block.
- `ui slider`: property `Rect` on `UISliderInfo` is missing from `UISlider`'s `Properties:` block.
- `ui input field`: property `Rect` on `UIInputFieldInfo` is missing from `UIInputField`'s `Properties:` block.
