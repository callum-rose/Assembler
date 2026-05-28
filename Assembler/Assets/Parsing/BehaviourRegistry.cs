using System.Collections.Generic;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using UnityEngine;
using SphereGizmoInfo = Assembler.Parsing.Info.Behaviours.SphereGizmoInfo;
using CubeGizmoInfo = Assembler.Parsing.Info.Behaviours.CubeGizmoInfo;

namespace Assembler.Parsing
{
	public delegate BehaviourInfo BehaviourFactory(
		string id,
		IReadOnlyList<ListenerInfo> listeners,
		IReadOnlyDictionary<string, AssemblerValue> props,
		TransformContext ctx);

	public static class BehaviourRegistry
	{
		public readonly static IReadOnlyDictionary<string, BehaviourFactory> All =
			new Dictionary<string, BehaviourFactory>
			{
				["box collider"] = BoxColliderInfo.Create,
				["sphere collider"] = SphereColliderInfo.Create,
				["rigidbody"] = RigidbodyInfo.Create,
				["velocity"] = VelocityInfo.Create,
				["translate"] = TranslateInfo.Create,
				["angular velocity"] = AngularVelocityInfo.Create,
				["rotate"] = RotateInfo.Create,
				["rotation setter"] = SetRotationInfo.Create,
				["move animation"] = MoveAnimationInfo.Create,
				["scale animation"] = ScaleAnimationInfo.Create,
				["rotate animation"] = RotateAnimationInfo.Create,
				["key hold trigger"] = KeyHoldTriggerInfo.Create,
				["key down trigger"] = KeyDownTriggerInfo.Create,
				["key up trigger"] = KeyUpTriggerInfo.Create,
				["tap trigger"] = TapTriggerInfo.Create,
				["double tap trigger"] = DoubleTapTriggerInfo.Create,
				["long press trigger"] = LongPressTriggerInfo.Create,
				["swipe trigger"] = SwipeTriggerInfo.Create,
				["drag trigger"] = DragTriggerInfo.Create,
				["pinch trigger"] = PinchTriggerInfo.Create,
				["rotate trigger"] = RotateTriggerInfo.Create,
				["condition"] = ConditionInfo.Create,
				["timer trigger"] = TimerTriggerInfo.Create,
				["deferred trigger"] = DeferredTriggerInfo.Create,
				["on start trigger"] = OnStartTriggerInfo.Create,
				["interval trigger"] = IntervalTriggerInfo.Create,
				["every frame trigger"] = EveryFrameTriggerInfo.Create,
				["collision enter trigger"] = CollisionEnterTriggerInfo.Create,
				["trigger enter trigger"] = TriggerEnterTriggerInfo.Create,
				["trigger exit trigger"] = TriggerExitTriggerInfo.Create,
				["trigger stay trigger"] = TriggerStayTriggerInfo.Create,
				["collision exit trigger"] = CollisionExitTriggerInfo.Create,
				["collision stay trigger"] = CollisionStayTriggerInfo.Create,
				["when all"] = WhenAllInfo.Create,
				["when any"] = WhenAnyInfo.Create,
				["spawner"] = SpawnerInfo.Create,
				["destroy"] = DestroyInfo.Create,
				["position setter"] = SetPositionInfo.Create,
				["camera"] = CameraInfo.Create,
				["condition gate"] = ConditionGateInfo.Create,
				["exclusive trigger"] = ExclusiveTriggerInfo.Create,
				["vector variable setter"] = VariableSetterInfo<Vector3>.Create,
				["int variable setter"] = VariableSetterInfo<int>.Create,
				["float variable setter"] = VariableSetterInfo<float>.Create,
				["bool variable setter"] = VariableSetterInfo<bool>.Create,
				["string variable setter"] = VariableSetterInfo<string>.Create,

				["vector list add"] = ListAddInfo<Vector3>.Create,
				["vector list remove at"] = ListRemoveAtInfo<Vector3>.Create,
				["vector list set at"] = ListSetAtInfo<Vector3>.Create,
				["vector list clear"] = ListClearInfo<Vector3>.Create,

				["int list add"] = ListAddInfo<int>.Create,
				["int list remove at"] = ListRemoveAtInfo<int>.Create,
				["int list set at"] = ListSetAtInfo<int>.Create,
				["int list clear"] = ListClearInfo<int>.Create,

				["float list add"] = ListAddInfo<float>.Create,
				["float list remove at"] = ListRemoveAtInfo<float>.Create,
				["float list set at"] = ListSetAtInfo<float>.Create,
				["float list clear"] = ListClearInfo<float>.Create,

				["bool list add"] = ListAddInfo<bool>.Create,
				["bool list remove at"] = ListRemoveAtInfo<bool>.Create,
				["bool list set at"] = ListSetAtInfo<bool>.Create,
				["bool list clear"] = ListClearInfo<bool>.Create,

				["string list add"] = ListAddInfo<string>.Create,
				["string list remove at"] = ListRemoveAtInfo<string>.Create,
				["string list set at"] = ListSetAtInfo<string>.Create,
				["string list clear"] = ListClearInfo<string>.Create,

				["colour list add"] = ListAddInfo<Color>.Create,
				["colour list remove at"] = ListRemoveAtInfo<Color>.Create,
				["colour list set at"] = ListSetAtInfo<Color>.Create,
				["colour list clear"] = ListClearInfo<Color>.Create,

				["sprite"] = SpriteInfo.Create,
				["audio source"] = AudioSourceInfo.Create,
				["sphere gizmo"] = SphereGizmoInfo.Create,
				["cube gizmo"] = CubeGizmoInfo.Create,
				["line gizmo"] = LineGizmoInfo.Create,
				["text label"] = TextLabelInfo.Create,
				["progress bar"] = ProgressBarInfo.Create,
				["ui image"] = UIImageInfo.Create,
				["ui button"] = UIButtonInfo.Create,
				["ui toggle"] = UIToggleInfo.Create,
				["ui slider"] = UISliderInfo.Create,
				["ui input field"] = UIInputFieldInfo.Create,
			};
	}
}
