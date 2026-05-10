using System;
using Assembler.Behaviours.Movement;
using Assembler.Behaviours.Physics;
using Assembler.Behaviours.Triggers.Conditionals;
using Assembler.Behaviours.Triggers.Input;
using Assembler.Behaviours.Triggers.Physical;
using Assembler.Behaviours.Triggers.Timing;
using Assembler.Parsing.Phase2;
using Assembler.Parsing.Phase2.Info;
using Assembler.Parsing.Phase3;
using UnityEngine;

namespace Assembler.Core
{
	public static class GameBehaviourFactory
	{
		public static (GameBehaviour, Action<VariableRegistry, CompiledExpressionsRegistry>) AddComponent(
			GameObject gameObject,
			BehaviourInfo behaviourInfo)
		{
			switch (behaviourInfo)
			{
				case BoxColliderInfo boxColliderInfo:
				{
					var gameBehaviour = gameObject.AddComponent<AutoAddBoxColliderBehaviour>();

					return (gameBehaviour, (vr, cr) => gameBehaviour.Initialise(new BoxColliderData(boxColliderInfo.Id,
						boxColliderInfo.Size.Resolve(vr, cr).Map(s => s.ToUnity()),
						boxColliderInfo.IsTrigger.Resolve(vr, cr))));
				}

				case SphereColliderInfo sphereColliderInfo:
				{
					var gameBehaviour = gameObject.AddComponent<AutoAddSphereColliderBehaviour>();

					return (gameBehaviour, (vr, cr) => gameBehaviour.Initialise(new SphereColliderData(sphereColliderInfo.Id,
						sphereColliderInfo.Radius.Resolve(vr, cr))));
				}

				case RigidbodyInfo rigidbodyInfo:
				{
					var gameBehaviour = gameObject.AddComponent<RigidbodyBehaviour>();

					return (gameBehaviour, (vr, cr) => gameBehaviour.Initialise(new RigidbodyData(rigidbodyInfo.Id)
					{
						UseGravity = rigidbodyInfo.UseGravity.Resolve(vr, cr)
					}));
				}

				case VelocityInfo velocityInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Velocity>();

					return (gameBehaviour, (vr, cr) => gameBehaviour.Initialise(new VelocityData(velocityInfo.Id,
						velocityInfo.Velocity.Resolve(vr, cr).Map(s => s.ToUnity()))));
				}

				case TranslateInfo translateInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Translate>();

					return (gameBehaviour, (vr, cr) => gameBehaviour.Initialise(new TranslateData(translateInfo.Id,
						translateInfo.Displacement.Resolve(vr, cr).Map(s => s.ToUnity()))));
				}

				case SetPositionInfo setPositionInfo:
				{
					var gameBehaviour = gameObject.AddComponent<SetPosition>();

					return (gameBehaviour, (vr, cr) => gameBehaviour.Initialise(new SetPositionData(setPositionInfo.Id,
						setPositionInfo.ValueExpression.Resolve(vr, cr).Map(s => s.ToUnity()))));
				}

				case KeyHoldTriggerInfo keyHoldTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<KeyHoldTrigger>();

					return (gameBehaviour, (vr, cr) => gameBehaviour.Initialise(new KeyHoldTriggerData(keyHoldTriggerInfo.Id,
						keyHoldTriggerInfo.Key.Resolve(vr, cr),
						Array.Empty<Action>())));
				}

				case KeyDownTriggerInfo keyDownTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<KeyDownTrigger>();

					return (gameBehaviour, (vr, cr) => gameBehaviour.Initialise(new KeyDownTriggerData(keyDownTriggerInfo.Id,
						keyDownTriggerInfo.Key.Resolve(vr, cr),
						Array.Empty<Action>())));
				}

				case KeyUpTriggerInfo keyUpTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<KeyUpTrigger>();

					return (gameBehaviour, (vr, cr) => gameBehaviour.Initialise(new KeyUpTriggerData(keyUpTriggerInfo.Id,
						keyUpTriggerInfo.Key.Resolve(vr, cr),
						Array.Empty<Action>())));
				}

				case TapTriggerInfo tapTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Tap>();

					return (gameBehaviour,
						(vr, cr) => gameBehaviour.Initialise(new TapTriggerData(tapTriggerInfo.Id, Array.Empty<Action>())));
				}

				case DoubleTapTriggerInfo doubleTapTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<DoubleTap>();

					return (gameBehaviour,
						(vr, cr) => gameBehaviour.Initialise(new DoubleTapTriggerData(doubleTapTriggerInfo.Id,
							Array.Empty<Action>())));
				}

				case LongPressTriggerInfo longPressTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<LongPress>();

					return (gameBehaviour,
						(vr, cr) => gameBehaviour.Initialise(new LongPressTriggerData(longPressTriggerInfo.Id,
							Array.Empty<Action>())));
				}

				case SwipeTriggerInfo swipeTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Swipe>();

					return (gameBehaviour,
						(vr, cr) => gameBehaviour.Initialise(new SwipeTriggerData(swipeTriggerInfo.Id, Array.Empty<Action>())));
				}

				case DragTriggerInfo dragTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Drag>();

					return (gameBehaviour,
						(vr, cr) => gameBehaviour.Initialise(new DragTriggerData(dragTriggerInfo.Id, Array.Empty<Action>())));
				}

				case PinchTriggerInfo pinchTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Pinch>();

					return (gameBehaviour,
						(vr, cr) => gameBehaviour.Initialise(new PinchTriggerData(pinchTriggerInfo.Id, Array.Empty<Action>())));
				}

				case RotateTriggerInfo rotateTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Rotate>();

					return (gameBehaviour,
						(vr, cr) => gameBehaviour.Initialise(new RotateTriggerData(rotateTriggerInfo.Id,
							Array.Empty<Action>())));
				}

				case ConditionInfo conditionInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Condition>();

					return (gameBehaviour, (vr, cr) => gameBehaviour.Initialise(new ConditionData(conditionInfo.Id,
						ResolveConditionExpression(conditionInfo.ExpressionId, conditionInfo.Arguments, vr, cr),
						Array.Empty<Action>())));
				}

				case TimerTriggerInfo timerTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<TimerTrigger>();

					return (gameBehaviour, (vr, cr) => gameBehaviour.Initialise(new TimerTriggerData(timerTriggerInfo.Id,
						timerTriggerInfo.Delay.Resolve(vr, cr),
						Array.Empty<Action>())));
				}

				case IntervalTriggerInfo intervalTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<IntervalTrigger>();

					return (gameBehaviour, (vr, cr) => gameBehaviour.Initialise(new IntervalTriggerData(intervalTriggerInfo.Id,
						intervalTriggerInfo.Interval.Resolve(vr, cr),
						Array.Empty<Action>())));
				}

				case EveryFrameTriggerInfo everyFrameTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<EveryFrameTrigger>();

					return (gameBehaviour,
						(vr, cr) => gameBehaviour.Initialise(new EveryFrameTriggerData(everyFrameTriggerInfo.Id,
							Array.Empty<Action>())));
				}

				case CollisionEnterTriggerInfo collisionEnterTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<CollisionEnter>();

					return (gameBehaviour, (vr, cr) => gameBehaviour.Initialise(new CollisionEnterTriggerData(
						collisionEnterTriggerInfo.Id,
						collisionEnterTriggerInfo.TagsToDetect,
						Array.Empty<Action>())));
				}

				case CollisionExitTriggerInfo collisionExitTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<CollisionExit>();

					return (gameBehaviour, (vr, cr) => gameBehaviour.Initialise(new CollisionExitTriggerData(
						collisionExitTriggerInfo.Id,
						collisionExitTriggerInfo.TagsToDetect,
						Array.Empty<Action>())));
				}

				case CollisionStayTriggerInfo collisionStayTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<CollisionStay>();

					return (gameBehaviour, (vr, cr) => gameBehaviour.Initialise(new CollisionStayTriggerData(
						collisionStayTriggerInfo.Id,
						collisionStayTriggerInfo.TagsToDetect,
						Array.Empty<Action>())));
				}

				case TriggerEnterTriggerInfo triggerEnterTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<TriggerEnter>();

					return (gameBehaviour, (vr, cr) => gameBehaviour.Initialise(new TriggerEnterTriggerData(
						triggerEnterTriggerInfo.Id,
						triggerEnterTriggerInfo.TagsToDetect,
						Array.Empty<Action>())));
				}

				case TriggerExitTriggerInfo triggerExitTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<TriggerExit>();

					return (gameBehaviour, (vr, cr) => gameBehaviour.Initialise(new TriggerExitTriggerData(
						triggerExitTriggerInfo.Id,
						triggerExitTriggerInfo.TagsToDetect,
						Array.Empty<Action>())));
				}
				default:
					throw new ArgumentException($"Unsupported behaviour info type:	{behaviourInfo.GetType()}");
			}
		}

		private static IValueProvider<bool> ResolveConditionExpression(
			ValueWrapper<string> expressionIdWrapper,
			System.Collections.Generic.IReadOnlyList<ValueWrapper<object>> arguments,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions)
		{
			// Build a synthetic ExpressionRef<bool> and resolve it via the standard path.
			var idProvider = expressionIdWrapper.Resolve(variables, expressions);
			var exprRef = new ExpressionRef<bool>(idProvider.Value, arguments);
			return exprRef.Resolve(variables, expressions);
		}
	}
}