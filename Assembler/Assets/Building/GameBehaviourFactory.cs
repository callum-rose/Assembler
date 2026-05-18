using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.Audio;
using Assembler.Behaviours.Camera;
using Assembler.Behaviours.Debug;
using Assembler.Behaviours.Movement;
using Assembler.Behaviours.Physics;
using Assembler.Behaviours.Spawners;
using Assembler.Behaviours.Sprites;
using Assembler.Behaviours.Triggers.Conditionals;
using Assembler.Behaviours.Triggers.Input;
using Assembler.Behaviours.Triggers.Physical;
using Assembler.Behaviours.Triggers.Timing;
using Assembler.Behaviours.VariableUpdaters;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Building
{

	public static class GameBehaviourFactory
	{
		private delegate (GameBehaviour, InitialiseBehaviourEvent) BehaviourBuilder(
			GameObject go,
			BehaviourInfo info,
			VariableRegistry vr,
			CompiledExpressionsRegistry cr,
			IEntitySpawner spawner,
			AssetRegistry ar);

		private readonly static Dictionary<Type, BehaviourBuilder> Builders = new()
		{
			[typeof(BoxColliderInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (BoxColliderInfo)info;
				var b = go.AddComponent<AutoAddBoxColliderBehaviour>();

				return (b, lr => b.Initialise(new BoxColliderData(i.Id,
					i.Listeners.ToActions(lr),
					i.Size.Resolve(vr, cr, ar),
					i.IsTrigger.Resolve(vr, cr, ar))));
			},
			[typeof(SphereColliderInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (SphereColliderInfo)info;
				var b = go.AddComponent<AutoAddSphereColliderBehaviour>();

				return (b, lr => b.Initialise(new SphereColliderData(i.Id,
					i.Listeners.ToActions(lr),
					i.Radius.Resolve(vr, cr, ar))));
			},
			[typeof(RigidbodyInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (RigidbodyInfo)info;
				var b = go.AddComponent<RigidbodyBehaviour>();

				return (b, lr => b.Initialise(new RigidbodyData(i.Id, i.Listeners.ToActions(lr))
				{
					UseGravity = i.UseGravity.Resolve(vr, cr, ar)
				}));
			},
			[typeof(VelocityInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (VelocityInfo)info;
				var b = go.AddComponent<Velocity>();

				return (b, lr => b.Initialise(new VelocityData(i.Id,
					i.Listeners.ToActions(lr),
					i.Velocity.Resolve(vr, cr, ar))));
			},
			[typeof(TranslateInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (TranslateInfo)info;
				var b = go.AddComponent<Translate>();

				return (b, lr => b.Initialise(new TranslateData(i.Id,
					i.Listeners.ToActions(lr),
					i.Displacement.Resolve(vr, cr, ar))));
			},
			[typeof(SetPositionInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (SetPositionInfo)info;
				var b = go.AddComponent<SetPosition>();

				return (b, lr => b.Initialise(new SetPositionData(i.Id,
					i.Listeners.ToActions(lr),
					i.ValueExpression.Resolve(vr, cr, ar))));
			},
			[typeof(KeyHoldTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (KeyHoldTriggerInfo)info;
				var b = go.AddComponent<KeyHoldTrigger>();

				return (b, lr => b.Initialise(new KeyHoldTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar),
					i.Listeners.ToActions(lr))));
			},
			[typeof(KeyDownTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (KeyDownTriggerInfo)info;
				var b = go.AddComponent<KeyDownTrigger>();

				return (b, lr => b.Initialise(new KeyDownTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar),
					i.Listeners.ToActions(lr))));
			},
			[typeof(KeyUpTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (KeyUpTriggerInfo)info;
				var b = go.AddComponent<KeyUpTrigger>();

				return (b, lr => b.Initialise(new KeyUpTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar),
					i.Listeners.ToActions(lr))));
			},
			[typeof(TapTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (TapTriggerInfo)info;
				var b = go.AddComponent<Tap>();
				return (b, lr => b.Initialise(new TapTriggerData(i.Id, i.Listeners.ToActions(lr))));
			},
			[typeof(DoubleTapTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (DoubleTapTriggerInfo)info;
				var b = go.AddComponent<DoubleTap>();
				return (b, lr => b.Initialise(new DoubleTapTriggerData(i.Id, i.Listeners.ToActions(lr))));
			},
			[typeof(LongPressTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (LongPressTriggerInfo)info;
				var b = go.AddComponent<LongPress>();
				return (b, lr => b.Initialise(new LongPressTriggerData(i.Id, i.Listeners.ToActions(lr))));
			},
			[typeof(SwipeTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (SwipeTriggerInfo)info;
				var b = go.AddComponent<Swipe>();
				return (b, lr => b.Initialise(new SwipeTriggerData(i.Id, i.Listeners.ToActions(lr))));
			},
			[typeof(DragTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (DragTriggerInfo)info;
				var b = go.AddComponent<Drag>();
				return (b, lr => b.Initialise(new DragTriggerData(i.Id, i.Listeners.ToActions(lr))));
			},
			[typeof(PinchTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (PinchTriggerInfo)info;
				var b = go.AddComponent<Pinch>();
				return (b, lr => b.Initialise(new PinchTriggerData(i.Id, i.Listeners.ToActions(lr))));
			},
			[typeof(RotateTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (RotateTriggerInfo)info;
				var b = go.AddComponent<Rotate>();
				return (b, lr => b.Initialise(new RotateTriggerData(i.Id, i.Listeners.ToActions(lr))));
			},
			[typeof(OnStartTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (OnStartTriggerInfo)info;
				var b = go.AddComponent<OnStartTrigger>();
				return (b, lr => b.Initialise(new OnStartTriggerData(i.Id, i.Listeners.ToActions(lr))));
			},
			[typeof(TimerTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (TimerTriggerInfo)info;
				var b = go.AddComponent<TimerTrigger>();

				return (b, lr => b.Initialise(new TimerTriggerData(i.Id,
					i.Delay.Resolve(vr, cr, ar),
					i.Listeners.ToActions(lr))));
			},
			[typeof(IntervalTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (IntervalTriggerInfo)info;
				var b = go.AddComponent<IntervalTrigger>();

				return (b, lr => b.Initialise(new IntervalTriggerData(i.Id,
					i.Interval.Resolve(vr, cr, ar),
					i.Listeners.ToActions(lr))));
			},
			[typeof(EveryFrameTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (EveryFrameTriggerInfo)info;
				var b = go.AddComponent<EveryFrameTrigger>();
				return (b, lr => b.Initialise(new EveryFrameTriggerData(i.Id, i.Listeners.ToActions(lr))));
			},
			[typeof(CollisionEnterTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (CollisionEnterTriggerInfo)info;
				var b = go.AddComponent<CollisionEnter>();

				return (b, lr => b.Initialise(new CollisionEnterTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr))));
			},
			[typeof(CollisionExitTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (CollisionExitTriggerInfo)info;
				var b = go.AddComponent<CollisionExit>();

				return (b, lr => b.Initialise(new CollisionExitTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr))));
			},
			[typeof(CollisionStayTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (CollisionStayTriggerInfo)info;
				var b = go.AddComponent<CollisionStay>();

				return (b, lr => b.Initialise(new CollisionStayTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr))));
			},
			[typeof(TriggerEnterTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (TriggerEnterTriggerInfo)info;
				var b = go.AddComponent<TriggerEnter>();

				return (b, lr => b.Initialise(new TriggerEnterTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr))));
			},
			[typeof(TriggerExitTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (TriggerExitTriggerInfo)info;
				var b = go.AddComponent<TriggerExit>();

				return (b, lr => b.Initialise(new TriggerExitTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr))));
			},
			[typeof(ConditionTriggerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (ConditionTriggerInfo)info;
				var b = go.AddComponent<Condition>();

				return (b, lr => b.Initialise(new ConditionData(i.Id,
					i.Condition.Resolve(vr, cr, ar),
					i.Listeners.ToActions(lr))));
			},
			[typeof(CameraInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (CameraInfo)info;
				var b = go.AddComponent<CameraBehaviour>();

				return (b, lr => b.Initialise(new CameraData(i.Id,
					i.Listeners.ToActions(lr),
					i.View.Resolve(vr, cr, ar),
					i.Size.Resolve(vr, cr, ar))));
			},
			[typeof(SpawnerInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (SpawnerInfo)info;
				var b = go.AddComponent<SpawnerBehaviour>();
				b.Spawner = es;

				return (b, lr => b.Initialise(new SpawnerData(i.Id,
					i.Listeners.ToActions(lr),
					i.TemplateId.Resolve(vr, cr, ar),
					i.Position.Resolve(vr, cr, ar))));
			},
			[typeof(DestroyInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (DestroyInfo)info;
				var b = go.AddComponent<DestroyBehaviour>();
				return (b, lr => b.Initialise(new DestroyData(i.Id, i.Listeners.ToActions(lr))));
			},
			[typeof(VariableSetterInfo<Vector3>)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (VariableSetterInfo<Vector3>)info;
				var b = go.AddComponent<Vector3Setter>();

				return (b, lr => b.Initialise(new VariableSetterData<Vector3>(i.Id,
					i.Listeners.ToActions(lr),
					i.ValueToSet.Resolve(vr, cr, ar),
					i.ValueToGet.Resolve(vr, cr, ar))));
			},
			[typeof(VariableSetterInfo<int>)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (VariableSetterInfo<int>)info;
				var b = go.AddComponent<IntSetter>();

				return (b, lr => b.Initialise(new VariableSetterData<int>(i.Id,
					i.Listeners.ToActions(lr),
					i.ValueToSet.Resolve(vr, cr, ar),
					i.ValueToGet.Resolve(vr, cr, ar))));
			},
			[typeof(VariableSetterInfo<float>)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (VariableSetterInfo<float>)info;
				var b = go.AddComponent<FloatSetter>();

				return (b, lr => b.Initialise(new VariableSetterData<float>(i.Id,
					i.Listeners.ToActions(lr),
					i.ValueToSet.Resolve(vr, cr, ar),
					i.ValueToGet.Resolve(vr, cr, ar))));
			},
			[typeof(VariableSetterInfo<bool>)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (VariableSetterInfo<bool>)info;
				var b = go.AddComponent<BoolSetter>();

				return (b, lr => b.Initialise(new VariableSetterData<bool>(i.Id,
					i.Listeners.ToActions(lr),
					i.ValueToSet.Resolve(vr, cr, ar),
					i.ValueToGet.Resolve(vr, cr, ar))));
			},
			[typeof(VariableSetterInfo<string>)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (VariableSetterInfo<string>)info;
				var b = go.AddComponent<StringSetter>();

				return (b, lr => b.Initialise(new VariableSetterData<string>(i.Id,
					i.Listeners.ToActions(lr),
					i.ValueToSet.Resolve(vr, cr, ar),
					i.ValueToGet.Resolve(vr, cr, ar))));
			},
			[typeof(SpriteInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (SpriteInfo)info;
				var b = go.AddComponent<SpriteBehaviour>();

				return (b, lr => b.Initialise(new SpriteData(i.Id,
					i.Listeners.ToActions(lr),
					i.Sprite.Resolve(vr, cr, ar),
					i.Size.Resolve(vr, cr, ar))));
			},
			[typeof(AudioSourceInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (AudioSourceInfo)info;
				var b = go.AddComponent<AudioSourceBehaviour>();

				return (b, lr => b.Initialise(new AudioSourceData(i.Id,
					i.Listeners.ToActions(lr),
					i.Clip.Resolve(vr, cr, ar),
					i.PlayOnStart.Resolve(vr, cr, ar),
					i.Loop.Resolve(vr, cr, ar))));
			},
			[typeof(SphereGizmoInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (SphereGizmoInfo)info;
				var b = go.AddComponent<SphereGizmoBehaviour>();

				return (b, lr => b.Initialise(new SphereGizmoData(i.Id,
					i.Listeners.ToActions(lr),
					i.Radius.Resolve(vr, cr, ar),
					i.IsWire.Resolve(vr, cr, ar),
					i.Colour.Resolve(vr, cr, ar))));
			},
			[typeof(CubeGizmoInfo)] = (go, info, vr, cr, es, ar) =>
			{
				var i = (CubeGizmoInfo)info;
				var b = go.AddComponent<CubeGizmoBehaviour>();

				return (b, lr => b.Initialise(new CubeGizmoData(i.Id,
					i.Listeners.ToActions(lr),
					i.Size.Resolve(vr, cr, ar),
					i.IsWire.Resolve(vr, cr, ar),
					i.Colour.Resolve(vr, cr, ar))));
			}
		};

		public static (GameBehaviour, InitialiseBehaviourEvent) Create(
			GameObject gameObject,
			BehaviourInfo behaviourInfo,
			VariableRegistry variableRegistry,
			CompiledExpressionsRegistry compiledExpressionRegistry,
			IEntitySpawner entitySpawner,
			AssetRegistry assets = null)
		{
			return Builders.TryGetValue(behaviourInfo.GetType(), out var builder)
				? builder(gameObject,
					behaviourInfo,
					variableRegistry,
					compiledExpressionRegistry,
					entitySpawner,
					assets)
				: throw new ArgumentException($"Unsupported behaviour info type '{behaviourInfo.GetType()}'");
		}

		private static IReadOnlyList<Action> ToActions(this IReadOnlyList<BehaviourDescriptor> listeners,
			IReadOnlyBehaviourRegistry listenerRegistry) =>
			listeners.Select(d => listenerRegistry[d]).Select(b => (Action)b.Execute).ToArray();
	}
}