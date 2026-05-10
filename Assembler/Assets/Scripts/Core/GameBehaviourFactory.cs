using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using AssemblerAlpha.Behaviours.Movement;
using AssemblerAlpha.Behaviours.Physics;
using AssemblerAlpha.Behaviours.Spawners;
using AssemblerAlpha.Behaviours.Triggers.Composite;
using AssemblerAlpha.Behaviours.Triggers.Conditionals;
using AssemblerAlpha.Behaviours.Triggers.Input;
using AssemblerAlpha.Behaviours.Triggers.Physical;
using AssemblerAlpha.Behaviours.Triggers.Timing;
using AssemblerAlpha.Behaviours.VariableUpdaters;
using UnityEngine;

namespace AssemblerAlpha.Core
{
	public static class GameBehaviourFactory
	{
		public static Component AddComponent(GameObject gameObject, BehaviourInfo behaviour)
		{
			return behaviour switch
			{
				BoxColliderInfo => gameObject.AddComponent<BoxCollider>(),
				SphereColliderInfo => gameObject.AddComponent<SphereCollider>(),
				RigidbodyInfo => gameObject.AddComponent<Rigidbody>(),
				VelocityInfo => gameObject.AddComponent<AssemblerAlpha.Behaviours.Movement.Velocity>(),
				TranslateInfo => gameObject.AddComponent<Translate>(),
				SetPositionInfo => gameObject.AddComponent<SetPosition>(),
				KeyHoldTriggerInfo => gameObject.AddComponent<KeyHoldTrigger>(),
				KeyDownTriggerInfo => gameObject.AddComponent<KeyDownTrigger>(),
				KeyUpTriggerInfo => gameObject.AddComponent<KeyUpTrigger>(),
				TapTriggerInfo => gameObject.AddComponent<Tap>(),
				DoubleTapTriggerInfo => gameObject.AddComponent<DoubleTap>(),
				LongPressTriggerInfo => gameObject.AddComponent<LongPress>(),
				SwipeTriggerInfo => gameObject.AddComponent<Swipe>(),
				DragTriggerInfo => gameObject.AddComponent<Drag>(),
				PinchTriggerInfo => gameObject.AddComponent<Pinch>(),
				RotateTriggerInfo => gameObject.AddComponent<Rotate>(),
				ConditionInfo => gameObject.AddComponent<Condition>(),
				AfterInfo => gameObject.AddComponent<TimerTrigger>(),
				EveryInfo => gameObject.AddComponent<IntervalTrigger>(),
				EveryFrameInfo => gameObject.AddComponent<EveryFrameTrigger>(),
				CollisionEnterTriggerInfo => gameObject.AddComponent<CollisionEnter>(),
				CollisionExitTriggerInfo => gameObject.AddComponent<CollisionExit>(),
				CollisionStayTriggerInfo => gameObject.AddComponent<CollisionStay>(),
				TriggerEnterTriggerInfo => gameObject.AddComponent<TriggerEnter>(),
				TriggerExitTriggerInfo => gameObject.AddComponent<TriggerExit>(),
				WhenAllInfo => gameObject.AddComponent<WhenAll>(),
				WhenAnyInfo => gameObject.AddComponent<WhenAny>(),
				SpawnerInfo => gameObject.AddComponent<SpawnerBehaviour>(),
				IntVariableSetterInfo => gameObject.AddComponent<IntSetter>(),
				FloatVariableSetterInfo => gameObject.AddComponent<FloatSetter>(),
				StringVariableSetterInfo => gameObject.AddComponent<StringSetter>(),
				BoolVariableSetterInfo => gameObject.AddComponent<BoolSetter>(),
				_ => null
			};
		}

		public static void SetData(Component component, BehaviourInfo behaviour)
		{
			switch (behaviour)
			{
				case BoxColliderInfo boxColliderInfo:
					var boxCollider = (BoxCollider)component;
					boxCollider.size = boxColliderInfo.Size.ToUnity();
					boxCollider.isTrigger = boxColliderInfo.IsTrigger;
					break;

				case SphereColliderInfo sphereColliderInfo:
					var sphereCollider = (SphereCollider)component;
					sphereCollider.radius = sphereColliderInfo.Size;
					break;

				case RigidbodyInfo rigidbodyInfo:
					var rigidbody = (Rigidbody)component;
					rigidbody.useGravity = rigidbodyInfo.UseGravity;
					break;

				case VelocityInfo velocityInfo:
					((AssemblerAlpha.Behaviours.Movement.Velocity)component).Initialise(velocityInfo);
					break;

				case TranslateInfo translateInfo:
					((Translate)component).Initialise(translateInfo);
					break;

				case SetPositionInfo setPositionInfo:
					((SetPosition)component).Initialise(setPositionInfo);
					break;

				case KeyHoldTriggerInfo keyHoldInfo:
					((KeyHoldTrigger)component).Initialise(keyHoldInfo);
					break;

				case KeyDownTriggerInfo keyDownInfo:
					((KeyDownTrigger)component).Initialise(keyDownInfo);
					break;

				case KeyUpTriggerInfo keyUpInfo:
					((KeyUpTrigger)component).Initialise(keyUpInfo);
					break;

				case TapTriggerInfo tapInfo:
					((Tap)component).Initialise(tapInfo);
					break;

				case DoubleTapTriggerInfo doubleTapInfo:
					((DoubleTap)component).Initialise(doubleTapInfo);
					break;

				case LongPressTriggerInfo longPressInfo:
					((LongPress)component).Initialise(longPressInfo);
					break;

				case SwipeTriggerInfo swipeInfo:
					((Swipe)component).Initialise(swipeInfo);
					break;

				case DragTriggerInfo dragInfo:
					((Drag)component).Initialise(dragInfo);
					break;

				case PinchTriggerInfo pinchInfo:
					((Pinch)component).Initialise(pinchInfo);
					break;

				case RotateTriggerInfo rotateInfo:
					((Rotate)component).Initialise(rotateInfo);
					break;

				case ConditionInfo conditionInfo:
					((Condition)component).Initialise(conditionInfo);
					break;

				case AfterInfo afterInfo:
					((TimerTrigger)component).Initialise(afterInfo);
					break;

				case EveryInfo everyInfo:
					((IntervalTrigger)component).Initialise(everyInfo);
					break;

				case EveryFrameInfo everyFrameInfo:
					((EveryFrameTrigger)component).Initialise(everyFrameInfo);
					break;

				case CollisionEnterTriggerInfo collisionEnterInfo:
					((CollisionEnter)component).Initialise(collisionEnterInfo);
					break;

				case CollisionExitTriggerInfo collisionExitInfo:
					((CollisionExit)component).Initialise(collisionExitInfo);
					break;

				case CollisionStayTriggerInfo collisionStayInfo:
					((CollisionStay)component).Initialise(collisionStayInfo);
					break;

				case TriggerEnterTriggerInfo triggerEnterInfo:
					((TriggerEnter)component).Initialise(triggerEnterInfo);
					break;

				case TriggerExitTriggerInfo triggerExitInfo:
					((TriggerExit)component).Initialise(triggerExitInfo);
					break;


				case WhenAllInfo whenAllInfo:
					((WhenAll)component).Initialise(whenAllInfo);
					break;

				case WhenAnyInfo whenAnyInfo:
					((WhenAny)component).Initialise(whenAnyInfo);
					break;

				case SpawnerInfo spawnerInfo:
					((SpawnerBehaviour)component).Initialise(spawnerInfo);
					break;

				case IntVariableSetterInfo intSetterInfo:
					((IntSetter)component).Initialise(intSetterInfo);
					break;

				case FloatVariableSetterInfo floatSetterInfo:
					((FloatSetter)component).Initialise(floatSetterInfo);
					break;

				case StringVariableSetterInfo stringSetterInfo:
					((StringSetter)component).Initialise(stringSetterInfo);
					break;

				case BoolVariableSetterInfo boolSetterInfo:
					((BoolSetter)component).Initialise(boolSetterInfo);
					break;
			}
		}
	}
}