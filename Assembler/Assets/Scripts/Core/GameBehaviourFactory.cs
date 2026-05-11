using System;
using Assembler.Behaviours.Camera;
using Assembler.Behaviours.Movement;
using Assembler.Behaviours.Physics;
using Assembler.Behaviours.Triggers.Conditionals;
using Assembler.Behaviours.Triggers.Input;
using Assembler.Behaviours.Triggers.Physical;
using Assembler.Behaviours.Triggers.Timing;
using Assembler.Behaviours.VariableUpdaters;
using Assembler.Parsing.Phase2;
using Assembler.Parsing.Phase2.Info;
using Assembler.Parsing.Phase3;
using UnityEngine;

namespace Assembler.Core
{
	public static class GameBehaviourFactory
	{
		public static (GameBehaviour, Action) AddComponent(
			GameObject gameObject,
			BehaviourInfo behaviourInfo,
			VariableRegistry variableRegistry,
			CompiledExpressionsRegistry compiledExpressionRegistry)
		{
			var vr = variableRegistry;
			var cr = compiledExpressionRegistry;
			
			switch (behaviourInfo)
			{
				case BoxColliderInfo boxColliderInfo:
				{
					var gameBehaviour = gameObject.AddComponent<AutoAddBoxColliderBehaviour>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new BoxColliderData(boxColliderInfo.Id,
						boxColliderInfo.Size.Resolve(vr, cr),
						boxColliderInfo.IsTrigger.Resolve(vr, cr))));
				}

				case SphereColliderInfo sphereColliderInfo:
				{
					var gameBehaviour = gameObject.AddComponent<AutoAddSphereColliderBehaviour>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new SphereColliderData(sphereColliderInfo.Id,
						sphereColliderInfo.Radius.Resolve(vr, cr))));
				}

				case RigidbodyInfo rigidbodyInfo:
				{
					var gameBehaviour = gameObject.AddComponent<RigidbodyBehaviour>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new RigidbodyData(rigidbodyInfo.Id)
					{
						UseGravity = rigidbodyInfo.UseGravity.Resolve(vr, cr)
					}));
				}

				case VelocityInfo velocityInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Velocity>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new VelocityData(velocityInfo.Id,
						velocityInfo.Velocity.Resolve(vr, cr))));
				}

				case TranslateInfo translateInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Translate>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new TranslateData(translateInfo.Id,
						translateInfo.Displacement.Resolve(vr, cr))));
				}

				case SetPositionInfo setPositionInfo:
				{
					var gameBehaviour = gameObject.AddComponent<SetPosition>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new SetPositionData(setPositionInfo.Id,
						setPositionInfo.ValueExpression.Resolve(vr, cr))));
				}

				case KeyHoldTriggerInfo keyHoldTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<KeyHoldTrigger>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new KeyHoldTriggerData(keyHoldTriggerInfo.Id,
						keyHoldTriggerInfo.Key.Resolve(vr, cr),
						Array.Empty<Action>())));
				}

				case KeyDownTriggerInfo keyDownTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<KeyDownTrigger>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new KeyDownTriggerData(keyDownTriggerInfo.Id,
						keyDownTriggerInfo.Key.Resolve(vr, cr),
						Array.Empty<Action>())));
				}

				case KeyUpTriggerInfo keyUpTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<KeyUpTrigger>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new KeyUpTriggerData(keyUpTriggerInfo.Id,
						keyUpTriggerInfo.Key.Resolve(vr, cr),
						Array.Empty<Action>())));
				}

				case TapTriggerInfo tapTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Tap>();

					return (gameBehaviour,
						() => gameBehaviour.Initialise(new TapTriggerData(tapTriggerInfo.Id, Array.Empty<Action>())));
				}

				case DoubleTapTriggerInfo doubleTapTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<DoubleTap>();

					return (gameBehaviour,
						() => gameBehaviour.Initialise(new DoubleTapTriggerData(doubleTapTriggerInfo.Id,
							Array.Empty<Action>())));
				}

				case LongPressTriggerInfo longPressTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<LongPress>();

					return (gameBehaviour,
						() => gameBehaviour.Initialise(new LongPressTriggerData(longPressTriggerInfo.Id,
							Array.Empty<Action>())));
				}

				case SwipeTriggerInfo swipeTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Swipe>();

					return (gameBehaviour,
						() => gameBehaviour.Initialise(new SwipeTriggerData(swipeTriggerInfo.Id, Array.Empty<Action>())));
				}

				case DragTriggerInfo dragTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Drag>();

					return (gameBehaviour,
						() => gameBehaviour.Initialise(new DragTriggerData(dragTriggerInfo.Id, Array.Empty<Action>())));
				}

				case PinchTriggerInfo pinchTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Pinch>();

					return (gameBehaviour,
						() => gameBehaviour.Initialise(new PinchTriggerData(pinchTriggerInfo.Id, Array.Empty<Action>())));
				}

				case RotateTriggerInfo rotateTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Rotate>();

					return (gameBehaviour,
						() => gameBehaviour.Initialise(new RotateTriggerData(rotateTriggerInfo.Id,
							Array.Empty<Action>())));
				}

				// case ConditionInfo conditionInfo:
				// {
				// 	var gameBehaviour = gameObject.AddComponent<Condition>();
				//
				// 	return (gameBehaviour, () => gameBehaviour.Initialise(new ConditionData(conditionInfo.Id,
				// 		ResolveConditionExpression(conditionInfo.ExpressionId,
				// 			conditionInfo.Arguments,
				// 			vr,
				// 			cr),
				// 		Array.Empty<Action>())));
				// }

				case TimerTriggerInfo timerTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<TimerTrigger>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new TimerTriggerData(timerTriggerInfo.Id,
						timerTriggerInfo.Delay.Resolve(vr, cr),
						Array.Empty<Action>())));
				}

				case IntervalTriggerInfo intervalTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<IntervalTrigger>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new IntervalTriggerData(intervalTriggerInfo.Id,
						intervalTriggerInfo.Interval.Resolve(vr, cr),
						Array.Empty<Action>())));
				}

				case EveryFrameTriggerInfo everyFrameTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<EveryFrameTrigger>();

					return (gameBehaviour,
						() => gameBehaviour.Initialise(new EveryFrameTriggerData(everyFrameTriggerInfo.Id,
							Array.Empty<Action>())));
				}

				case CollisionEnterTriggerInfo collisionEnterTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<CollisionEnter>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new CollisionEnterTriggerData(
						collisionEnterTriggerInfo.Id,
						collisionEnterTriggerInfo.TagsToDetect,
						Array.Empty<Action>())));
				}

				case CollisionExitTriggerInfo collisionExitTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<CollisionExit>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new CollisionExitTriggerData(
						collisionExitTriggerInfo.Id,
						collisionExitTriggerInfo.TagsToDetect,
						Array.Empty<Action>())));
				}

				case CollisionStayTriggerInfo collisionStayTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<CollisionStay>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new CollisionStayTriggerData(
						collisionStayTriggerInfo.Id,
						collisionStayTriggerInfo.TagsToDetect,
						Array.Empty<Action>())));
				}

				case TriggerEnterTriggerInfo triggerEnterTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<TriggerEnter>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new TriggerEnterTriggerData(
						triggerEnterTriggerInfo.Id,
						triggerEnterTriggerInfo.TagsToDetect,
						Array.Empty<Action>())));
				}

				case TriggerExitTriggerInfo triggerExitTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<TriggerExit>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new TriggerExitTriggerData(
						triggerExitTriggerInfo.Id,
						triggerExitTriggerInfo.TagsToDetect,
						Array.Empty<Action>())));
				}

				case VariableSetterInfo<Vector3> variableSetterInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Vector3Setter>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new VariableSetterData<Vector3>(
						variableSetterInfo.Id,
						variableSetterInfo.ValueToSet.Resolve(vr, cr),
						variableSetterInfo.ValueToGet.Resolve(vr, cr)
					)));
				}

				case VariableSetterInfo<int> variableSetterInfo:
				{
					var gameBehaviour = gameObject.AddComponent<IntSetter>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new VariableSetterData<int>(
						variableSetterInfo.Id,
						variableSetterInfo.ValueToSet.Resolve(vr, cr),
						variableSetterInfo.ValueToGet.Resolve(vr, cr)
					)));
				}

				case VariableSetterInfo<float> variableSetterInfo:
				{
					var gameBehaviour = gameObject.AddComponent<FloatSetter>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new VariableSetterData<float>(
						variableSetterInfo.Id,
						variableSetterInfo.ValueToSet.Resolve(vr, cr),
						variableSetterInfo.ValueToGet.Resolve(vr, cr)
					)));
				}

				case VariableSetterInfo<bool> variableSetterInfo:
				{
					var gameBehaviour = gameObject.AddComponent<BoolSetter>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new VariableSetterData<bool>(
						variableSetterInfo.Id,
						variableSetterInfo.ValueToSet.Resolve(vr, cr),
						variableSetterInfo.ValueToGet.Resolve(vr, cr)
					)));
				}

				case VariableSetterInfo<string> variableSetterInfo:
				{
					var gameBehaviour = gameObject.AddComponent<StringSetter>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new VariableSetterData<string>(
						variableSetterInfo.Id,
						variableSetterInfo.ValueToSet.Resolve(vr, cr),
						variableSetterInfo.ValueToGet.Resolve(vr, cr)
					)));
				}

				case CameraInfo info:
				{
					var gameBehaviour = gameObject.AddComponent<CameraBehaviour>();

					return (gameBehaviour, () => gameBehaviour.Initialise(new CameraData(info.Id,
						info.View.Resolve(vr, cr),
						info.Size.Resolve(vr, cr))));
				}

				// case ConditionTriggerInfo info:
				// {
				// 	var gameBehaviour = gameObject.AddComponent<Condition>();
				// 	
				// 	return (gameBehaviour, () => gameBehaviour.Initialise(new ConditionData(info.Id, info.Condition.Resolve(vr, cr), info.Listeners, info.ExecuteOn.Resolve(vr, cr))));
				// }

				default:
					throw new ArgumentException($"Unsupported behaviour info type '{behaviourInfo.GetType()}'");
			}
		}

		private static IValueProvider<bool> ResolveConditionExpression(
			ValueSource<string> expressionIdSource,
			System.Collections.Generic.IReadOnlyList<ValueSource<object>> arguments,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions)
		{
			// Build a synthetic ExpressionRef<bool> and resolve it via the standard path.
			var idProvider = expressionIdSource.Resolve(variables, expressions);
			var exprRef = new ExpressionSource<bool>(idProvider.Value, arguments);
			return exprRef.Resolve(variables, expressions);
		}
	}
}