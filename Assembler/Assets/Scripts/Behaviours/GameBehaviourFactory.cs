using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours.Camera;
using Assembler.Behaviours.Movement;
using Assembler.Behaviours.Physics;
using Assembler.Behaviours.Spawners;
using Assembler.Behaviours.Triggers.Conditionals;
using Assembler.Behaviours.Triggers.Input;
using Assembler.Behaviours.Triggers.Physical;
using Assembler.Behaviours.Triggers.Timing;
using Assembler.Behaviours.VariableUpdaters;
using Assembler.Core;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Behaviours
{
	public static class GameBehaviourFactory
	{
		public static (GameBehaviour, Action<IReadOnlyDictionary<BehaviourDescriptor, GameBehaviour>>) Create(
			GameObject gameObject,
			BehaviourInfo behaviourInfo,
			VariableRegistry variableRegistry,
			CompiledExpressionsRegistry compiledExpressionRegistry,
			IEntitySpawner entitySpawner)
		{
			var vr = variableRegistry;
			var cr = compiledExpressionRegistry;

			switch (behaviourInfo)
			{
				case BoxColliderInfo boxColliderInfo:
				{
					var gameBehaviour = gameObject.AddComponent<AutoAddBoxColliderBehaviour>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new BoxColliderData(boxColliderInfo.Id,
						boxColliderInfo.Listeners.ToActions(listenerRegistry),
						boxColliderInfo.Size.Resolve(vr, cr),
						boxColliderInfo.IsTrigger.Resolve(vr, cr))));
				}

				case SphereColliderInfo sphereColliderInfo:
				{
					var gameBehaviour = gameObject.AddComponent<AutoAddSphereColliderBehaviour>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new SphereColliderData(
						sphereColliderInfo.Id,
						sphereColliderInfo.Listeners.ToActions(listenerRegistry),
						sphereColliderInfo.Radius.Resolve(vr, cr))));
				}

				case RigidbodyInfo rigidbodyInfo:
				{
					var gameBehaviour = gameObject.AddComponent<RigidbodyBehaviour>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(
						new RigidbodyData(rigidbodyInfo.Id, rigidbodyInfo.Listeners.ToActions(listenerRegistry))
						{
							UseGravity = rigidbodyInfo.UseGravity.Resolve(vr, cr)
						}));
				}

				case VelocityInfo velocityInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Velocity>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new VelocityData(velocityInfo.Id,
						velocityInfo.Listeners.ToActions(listenerRegistry),
						velocityInfo.Velocity.Resolve(vr, cr))));
				}

				case TranslateInfo translateInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Translate>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new TranslateData(translateInfo.Id,
						translateInfo.Listeners.ToActions(listenerRegistry),
						translateInfo.Displacement.Resolve(vr, cr))));
				}

				case SetPositionInfo setPositionInfo:
				{
					var gameBehaviour = gameObject.AddComponent<SetPosition>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new SetPositionData(setPositionInfo.Id,
						setPositionInfo.Listeners.ToActions(listenerRegistry),
						setPositionInfo.ValueExpression.Resolve(vr, cr))));
				}

				case KeyHoldTriggerInfo keyHoldTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<KeyHoldTrigger>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new KeyHoldTriggerData(
						keyHoldTriggerInfo.Id,
						keyHoldTriggerInfo.Key.Resolve(vr, cr),
						keyHoldTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case KeyDownTriggerInfo keyDownTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<KeyDownTrigger>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new KeyDownTriggerData(
						keyDownTriggerInfo.Id,
						keyDownTriggerInfo.Key.Resolve(vr, cr),
						keyDownTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case KeyUpTriggerInfo keyUpTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<KeyUpTrigger>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new KeyUpTriggerData(
						keyUpTriggerInfo.Id,
						keyUpTriggerInfo.Key.Resolve(vr, cr),
						keyUpTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case TapTriggerInfo tapTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Tap>();

					return (gameBehaviour,
						listenerRegistry => gameBehaviour.Initialise(new TapTriggerData(tapTriggerInfo.Id,
							tapTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case DoubleTapTriggerInfo doubleTapTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<DoubleTap>();

					return (gameBehaviour,
						listenerRegistry =>
							gameBehaviour.Initialise(new DoubleTapTriggerData(doubleTapTriggerInfo.Id,
								doubleTapTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case LongPressTriggerInfo longPressTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<LongPress>();

					return (gameBehaviour,
						listenerRegistry =>
							gameBehaviour.Initialise(new LongPressTriggerData(longPressTriggerInfo.Id,
								longPressTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case SwipeTriggerInfo swipeTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Swipe>();

					return (gameBehaviour,
						listenerRegistry =>
							gameBehaviour.Initialise(new SwipeTriggerData(swipeTriggerInfo.Id,
								swipeTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case DragTriggerInfo dragTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Drag>();

					return (gameBehaviour,
						listenerRegistry =>
							gameBehaviour.Initialise(new DragTriggerData(dragTriggerInfo.Id,
								dragTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case PinchTriggerInfo pinchTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Pinch>();

					return (gameBehaviour,
						listenerRegistry =>
							gameBehaviour.Initialise(new PinchTriggerData(pinchTriggerInfo.Id,
								pinchTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case RotateTriggerInfo rotateTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Rotate>();

					return (gameBehaviour,
						listenerRegistry =>
							gameBehaviour.Initialise(new RotateTriggerData(rotateTriggerInfo.Id,
								rotateTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				// case ConditionInfo conditionInfo:
				// {
				// 	var gameBehaviour = gameObject.AddComponent<Condition>();
				//
				// 	return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new ConditionData(conditionInfo.Id,
				// 		ResolveConditionExpression(conditionInfo.ExpressionId,
				// 			conditionInfo.Arguments,
				// 			vr,
				// 			cr),
				// 		listenerRegistry)));
				// }
				
				case OnStartTriggerInfo onStartTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<OnStartTrigger>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new OnStartTriggerData(
						onStartTriggerInfo.Id,
						onStartTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case TimerTriggerInfo timerTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<TimerTrigger>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new TimerTriggerData(
						timerTriggerInfo.Id,
						timerTriggerInfo.Delay.Resolve(vr, cr),
						timerTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case IntervalTriggerInfo intervalTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<IntervalTrigger>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new IntervalTriggerData(
						intervalTriggerInfo.Id,
						intervalTriggerInfo.Interval.Resolve(vr, cr),
						intervalTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case EveryFrameTriggerInfo everyFrameTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<EveryFrameTrigger>();

					return (gameBehaviour,
						listenerRegistry =>
							gameBehaviour.Initialise(new EveryFrameTriggerData(everyFrameTriggerInfo.Id,
								everyFrameTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case CollisionEnterTriggerInfo collisionEnterTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<CollisionEnter>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new CollisionEnterTriggerData(
						collisionEnterTriggerInfo.Id,
						collisionEnterTriggerInfo.TagsToDetect,
						collisionEnterTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case CollisionExitTriggerInfo collisionExitTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<CollisionExit>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new CollisionExitTriggerData(
						collisionExitTriggerInfo.Id,
						collisionExitTriggerInfo.TagsToDetect,
						collisionExitTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case CollisionStayTriggerInfo collisionStayTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<CollisionStay>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new CollisionStayTriggerData(
						collisionStayTriggerInfo.Id,
						collisionStayTriggerInfo.TagsToDetect,
						collisionStayTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case TriggerEnterTriggerInfo triggerEnterTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<TriggerEnter>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new TriggerEnterTriggerData(
						triggerEnterTriggerInfo.Id,
						triggerEnterTriggerInfo.TagsToDetect,
						triggerEnterTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case TriggerExitTriggerInfo triggerExitTriggerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<TriggerExit>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new TriggerExitTriggerData(
						triggerExitTriggerInfo.Id,
						triggerExitTriggerInfo.TagsToDetect,
						triggerExitTriggerInfo.Listeners.ToActions(listenerRegistry))));
				}

				case VariableSetterInfo<Vector3> variableSetterInfo:
				{
					var gameBehaviour = gameObject.AddComponent<Vector3Setter>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new VariableSetterData<Vector3>(
						variableSetterInfo.Id,
						variableSetterInfo.Listeners.ToActions(listenerRegistry),
						variableSetterInfo.ValueToSet.Resolve(vr, cr),
						variableSetterInfo.ValueToGet.Resolve(vr, cr)
					)));
				}

				case VariableSetterInfo<int> variableSetterInfo:
				{
					var gameBehaviour = gameObject.AddComponent<IntSetter>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new VariableSetterData<int>(
						variableSetterInfo.Id,
						variableSetterInfo.Listeners.ToActions(listenerRegistry),
						variableSetterInfo.ValueToSet.Resolve(vr, cr),
						variableSetterInfo.ValueToGet.Resolve(vr, cr)
					)));
				}

				case VariableSetterInfo<float> variableSetterInfo:
				{
					var gameBehaviour = gameObject.AddComponent<FloatSetter>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new VariableSetterData<float>(
						variableSetterInfo.Id,
						variableSetterInfo.Listeners.ToActions(listenerRegistry),
						variableSetterInfo.ValueToSet.Resolve(vr, cr),
						variableSetterInfo.ValueToGet.Resolve(vr, cr)
					)));
				}

				case VariableSetterInfo<bool> variableSetterInfo:
				{
					var gameBehaviour = gameObject.AddComponent<BoolSetter>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new VariableSetterData<bool>(
						variableSetterInfo.Id,
						variableSetterInfo.Listeners.ToActions(listenerRegistry),
						variableSetterInfo.ValueToSet.Resolve(vr, cr),
						variableSetterInfo.ValueToGet.Resolve(vr, cr)
					)));
				}

				case VariableSetterInfo<string> variableSetterInfo:
				{
					var gameBehaviour = gameObject.AddComponent<StringSetter>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new VariableSetterData<string>(
						variableSetterInfo.Id,
						variableSetterInfo.Listeners.ToActions(listenerRegistry),
						variableSetterInfo.ValueToSet.Resolve(vr, cr),
						variableSetterInfo.ValueToGet.Resolve(vr, cr)
					)));
				}

				case CameraInfo info:
				{
					var gameBehaviour = gameObject.AddComponent<CameraBehaviour>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new CameraData(info.Id,
						info.Listeners.ToActions(listenerRegistry),
						info.View.Resolve(vr, cr),
						info.Size.Resolve(vr, cr))));
				}

				case ConditionTriggerInfo info:
				{
					var gameBehaviour = gameObject.AddComponent<Condition>();

					return (gameBehaviour,
						listenerRegistry => gameBehaviour.Initialise(new ConditionData(info.Id,
							info.Condition.Resolve(vr, cr),
							info.Listeners.ToActions(listenerRegistry))));
				}

				case SpawnerInfo spawnerInfo:
				{
					var gameBehaviour = gameObject.AddComponent<SpawnerBehaviour>();
					gameBehaviour.Spawner = entitySpawner;

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new SpawnerData(
						spawnerInfo.Id,
						spawnerInfo.Listeners.ToActions(listenerRegistry),
						spawnerInfo.TemplateId.Resolve(vr, cr),
						spawnerInfo.Position.Resolve(vr, cr))));
				}

				case DestroyInfo info:
				{
					var gameBehaviour = gameObject.AddComponent<DestroyBehaviour>();

					return (gameBehaviour, listenerRegistry => gameBehaviour.Initialise(new DestroyData(
						info.Id,
						info.Listeners.ToActions(listenerRegistry))));
				}

				default:
					throw new ArgumentException($"Unsupported behaviour info type '{behaviourInfo.GetType()}'");
			}
		}

		private static IReadOnlyList<Action> ToActions(this IReadOnlyList<BehaviourDescriptor> listeners,
			IReadOnlyDictionary<BehaviourDescriptor, GameBehaviour> listenerRegistry) =>
			listeners.Select(d => listenerRegistry[d]).Select(b => (Action)b.Execute).ToArray();
	}
}