using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.Animations;
using Assembler.Behaviours.Audio;
using Assembler.Behaviours.Debug.UI;
using Assembler.Behaviours.Camera;
using Assembler.Behaviours.Debug;
using Assembler.Behaviours.ListOperations;
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
			AssetRegistry ar,
			TriggerContext tc,
			EntityVariableScope scope);

		private readonly static Dictionary<Type, BehaviourBuilder> Builders = new()
		{
			[typeof(BoxColliderInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (BoxColliderInfo)info;
				var b = go.AddComponent<AutoAddBoxColliderBehaviour>();

				return (b, lr => b.Initialise(new BoxColliderData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Size.Resolve(vr, cr, ar, tc, scope),
					i.IsTrigger.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(SphereColliderInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (SphereColliderInfo)info;
				var b = go.AddComponent<AutoAddSphereColliderBehaviour>();

				return (b, lr => b.Initialise(new SphereColliderData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Radius.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(RigidbodyInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (RigidbodyInfo)info;
				var b = go.AddComponent<RigidbodyBehaviour>();

				return (b, lr => b.Initialise(new RigidbodyData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))
				{
					UseGravity = i.UseGravity.Resolve(vr, cr, ar, tc, scope)
				}));
			},
			[typeof(VelocityInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (VelocityInfo)info;
				var b = go.AddComponent<Velocity>();

				return (b, lr => b.Initialise(new VelocityData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Velocity.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(TranslateInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (TranslateInfo)info;
				var b = go.AddComponent<Translate>();

				return (b, lr => b.Initialise(new TranslateData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Displacement.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(MoveAnimationInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (MoveAnimationInfo)info;
				var b = go.AddComponent<MoveAnimation>();

				return (b, lr => b.Initialise(new TransformAnimationData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Start.Resolve(vr, cr, ar, tc, scope),
					i.End.Resolve(vr, cr, ar, tc, scope),
					i.Duration.Resolve(vr, cr, ar, tc, scope),
					i.Easing.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ScaleAnimationInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ScaleAnimationInfo)info;
				var b = go.AddComponent<ScaleAnimation>();

				return (b, lr => b.Initialise(new TransformAnimationData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Start.Resolve(vr, cr, ar, tc, scope),
					i.End.Resolve(vr, cr, ar, tc, scope),
					i.Duration.Resolve(vr, cr, ar, tc, scope),
					i.Easing.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(RotateAnimationInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (RotateAnimationInfo)info;
				var b = go.AddComponent<RotateAnimation>();

				return (b, lr => b.Initialise(new TransformAnimationData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Start.Resolve(vr, cr, ar, tc, scope),
					i.End.Resolve(vr, cr, ar, tc, scope),
					i.Duration.Resolve(vr, cr, ar, tc, scope),
					i.Easing.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(SetPositionInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (SetPositionInfo)info;
				var b = go.AddComponent<SetPosition>();

				return (b, lr => b.Initialise(new SetPositionData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueExpression.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(KeyHoldTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (KeyHoldTriggerInfo)info;
				var b = go.AddComponent<KeyHoldTrigger>();

				return (b, lr => b.Initialise(new KeyHoldTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar, tc, scope),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(KeyDownTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (KeyDownTriggerInfo)info;
				var b = go.AddComponent<KeyDownTrigger>();

				return (b, lr => b.Initialise(new KeyDownTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar, tc, scope),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(KeyUpTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (KeyUpTriggerInfo)info;
				var b = go.AddComponent<KeyUpTrigger>();

				return (b, lr => b.Initialise(new KeyUpTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar, tc, scope),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(TapTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (TapTriggerInfo)info;
				var b = go.AddComponent<Tap>();
				return (b, lr => b.Initialise(new TapTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(DoubleTapTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (DoubleTapTriggerInfo)info;
				var b = go.AddComponent<DoubleTap>();
				return (b, lr => b.Initialise(new DoubleTapTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(LongPressTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (LongPressTriggerInfo)info;
				var b = go.AddComponent<LongPress>();
				return (b, lr => b.Initialise(new LongPressTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(SwipeTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (SwipeTriggerInfo)info;
				var b = go.AddComponent<Swipe>();
				return (b, lr => b.Initialise(new SwipeTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(DragTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (DragTriggerInfo)info;
				var b = go.AddComponent<Drag>();
				return (b, lr => b.Initialise(new DragTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(PinchTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (PinchTriggerInfo)info;
				var b = go.AddComponent<Pinch>();
				return (b, lr => b.Initialise(new PinchTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(RotateTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (RotateTriggerInfo)info;
				var b = go.AddComponent<Rotate>();
				return (b, lr => b.Initialise(new RotateTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(OnStartTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (OnStartTriggerInfo)info;
				var b = go.AddComponent<OnStartTrigger>();
				return (b, lr => b.Initialise(new OnStartTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(TimerTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (TimerTriggerInfo)info;
				var b = go.AddComponent<TimerTrigger>();

				return (b, lr => b.Initialise(new TimerTriggerData(i.Id,
					i.Delay.Resolve(vr, cr, ar, tc, scope),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(DeferredTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (DeferredTriggerInfo)info;
				var b = go.AddComponent<DeferredTrigger>();

				return (b, lr => b.Initialise(new DeferredTriggerData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Delay.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(IntervalTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (IntervalTriggerInfo)info;
				var b = go.AddComponent<IntervalTrigger>();

				return (b, lr => b.Initialise(new IntervalTriggerData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Interval.Resolve(vr, cr, ar, tc, scope),
					i.Count.Resolve(vr, cr, ar, tc, scope),
					i.AutoStart.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(EveryFrameTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (EveryFrameTriggerInfo)info;
				var b = go.AddComponent<EveryFrameTrigger>();
				return (b, lr => b.Initialise(new EveryFrameTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(CollisionEnterTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (CollisionEnterTriggerInfo)info;
				var b = go.AddComponent<CollisionEnter>();
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new CollisionEnterTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(CollisionExitTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (CollisionExitTriggerInfo)info;
				var b = go.AddComponent<CollisionExit>();
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new CollisionExitTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(CollisionStayTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (CollisionStayTriggerInfo)info;
				var b = go.AddComponent<CollisionStay>();
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new CollisionStayTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(TriggerEnterTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (TriggerEnterTriggerInfo)info;
				var b = go.AddComponent<TriggerEnter>();
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new TriggerEnterTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(TriggerExitTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (TriggerExitTriggerInfo)info;
				var b = go.AddComponent<TriggerExit>();
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new TriggerExitTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(ConditionTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ConditionTriggerInfo)info;
				var b = go.AddComponent<Condition>();

				return (b, lr => b.Initialise(new ConditionData(i.Id,
					i.Condition.Resolve(vr, cr, ar, tc, scope),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(CameraInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (CameraInfo)info;
				var b = go.AddComponent<CameraBehaviour>();

				return (b, lr => b.Initialise(new CameraData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.View.Resolve(vr, cr, ar, tc, scope),
					i.Size.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(SpawnerInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (SpawnerInfo)info;
				var b = go.AddComponent<SpawnerBehaviour>();
				b.Spawner = es;

				return (b, lr => b.Initialise(new SpawnerData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.TemplateId.Resolve(vr, cr, ar, tc, scope),
					i.Position.Resolve(vr, cr, ar, tc, scope),
					i.Rotation.Resolve(vr, cr, ar, tc, scope),
					i.Parameters.ToDictionary(kv => kv.Key,
						kv => (IValueProvider)kv.Value.Resolve(vr, cr, ar, tc, scope)))));
			},
			[typeof(DestroyInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (DestroyInfo)info;
				var b = go.AddComponent<DestroyBehaviour>();
				return (b, lr => b.Initialise(new DestroyData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(VariableSetterInfo<Vector3>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (VariableSetterInfo<Vector3>)info;
				var b = go.AddComponent<Vector3Setter>();

				return (b, lr => b.Initialise(new VariableSetterData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(VariableSetterInfo<int>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (VariableSetterInfo<int>)info;
				var b = go.AddComponent<IntSetter>();

				return (b, lr => b.Initialise(new VariableSetterData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(VariableSetterInfo<float>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (VariableSetterInfo<float>)info;
				var b = go.AddComponent<FloatSetter>();

				return (b, lr => b.Initialise(new VariableSetterData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(VariableSetterInfo<bool>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (VariableSetterInfo<bool>)info;
				var b = go.AddComponent<BoolSetter>();

				return (b, lr => b.Initialise(new VariableSetterData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(VariableSetterInfo<string>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (VariableSetterInfo<string>)info;
				var b = go.AddComponent<StringSetter>();

				return (b, lr => b.Initialise(new VariableSetterData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope))));
			},

			// --- List operations: Vector3 ---
			[typeof(ListAddInfo<Vector3>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListAddInfo<Vector3>)info;
				var b = go.AddComponent<Vector3ListAdd>();
				return (b, lr => b.Initialise(new ListAddData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListRemoveAtInfo<Vector3>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListRemoveAtInfo<Vector3>)info;
				var b = go.AddComponent<Vector3ListRemoveAt>();
				return (b, lr => b.Initialise(new ListRemoveAtData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListSetAtInfo<Vector3>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListSetAtInfo<Vector3>)info;
				var b = go.AddComponent<Vector3ListSetAt>();
				return (b, lr => b.Initialise(new ListSetAtData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListClearInfo<Vector3>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListClearInfo<Vector3>)info;
				var b = go.AddComponent<Vector3ListClear>();
				return (b, lr => b.Initialise(new ListClearData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope))));
			},

			// --- List operations: int ---
			[typeof(ListAddInfo<int>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListAddInfo<int>)info;
				var b = go.AddComponent<IntListAdd>();
				return (b, lr => b.Initialise(new ListAddData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListRemoveAtInfo<int>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListRemoveAtInfo<int>)info;
				var b = go.AddComponent<IntListRemoveAt>();
				return (b, lr => b.Initialise(new ListRemoveAtData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListSetAtInfo<int>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListSetAtInfo<int>)info;
				var b = go.AddComponent<IntListSetAt>();
				return (b, lr => b.Initialise(new ListSetAtData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListClearInfo<int>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListClearInfo<int>)info;
				var b = go.AddComponent<IntListClear>();
				return (b, lr => b.Initialise(new ListClearData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope))));
			},

			// --- List operations: float ---
			[typeof(ListAddInfo<float>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListAddInfo<float>)info;
				var b = go.AddComponent<FloatListAdd>();
				return (b, lr => b.Initialise(new ListAddData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListRemoveAtInfo<float>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListRemoveAtInfo<float>)info;
				var b = go.AddComponent<FloatListRemoveAt>();
				return (b, lr => b.Initialise(new ListRemoveAtData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListSetAtInfo<float>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListSetAtInfo<float>)info;
				var b = go.AddComponent<FloatListSetAt>();
				return (b, lr => b.Initialise(new ListSetAtData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListClearInfo<float>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListClearInfo<float>)info;
				var b = go.AddComponent<FloatListClear>();
				return (b, lr => b.Initialise(new ListClearData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope))));
			},

			// --- List operations: bool ---
			[typeof(ListAddInfo<bool>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListAddInfo<bool>)info;
				var b = go.AddComponent<BoolListAdd>();
				return (b, lr => b.Initialise(new ListAddData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListRemoveAtInfo<bool>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListRemoveAtInfo<bool>)info;
				var b = go.AddComponent<BoolListRemoveAt>();
				return (b, lr => b.Initialise(new ListRemoveAtData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListSetAtInfo<bool>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListSetAtInfo<bool>)info;
				var b = go.AddComponent<BoolListSetAt>();
				return (b, lr => b.Initialise(new ListSetAtData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListClearInfo<bool>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListClearInfo<bool>)info;
				var b = go.AddComponent<BoolListClear>();
				return (b, lr => b.Initialise(new ListClearData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope))));
			},

			// --- List operations: string ---
			[typeof(ListAddInfo<string>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListAddInfo<string>)info;
				var b = go.AddComponent<StringListAdd>();
				return (b, lr => b.Initialise(new ListAddData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListRemoveAtInfo<string>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListRemoveAtInfo<string>)info;
				var b = go.AddComponent<StringListRemoveAt>();
				return (b, lr => b.Initialise(new ListRemoveAtData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListSetAtInfo<string>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListSetAtInfo<string>)info;
				var b = go.AddComponent<StringListSetAt>();
				return (b, lr => b.Initialise(new ListSetAtData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListClearInfo<string>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListClearInfo<string>)info;
				var b = go.AddComponent<StringListClear>();
				return (b, lr => b.Initialise(new ListClearData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope))));
			},

			// --- List operations: Color ---
			[typeof(ListAddInfo<Color>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListAddInfo<Color>)info;
				var b = go.AddComponent<ColourListAdd>();
				return (b, lr => b.Initialise(new ListAddData<Color>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListRemoveAtInfo<Color>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListRemoveAtInfo<Color>)info;
				var b = go.AddComponent<ColourListRemoveAt>();
				return (b, lr => b.Initialise(new ListRemoveAtData<Color>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListSetAtInfo<Color>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListSetAtInfo<Color>)info;
				var b = go.AddComponent<ColourListSetAt>();
				return (b, lr => b.Initialise(new ListSetAtData<Color>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListClearInfo<Color>)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListClearInfo<Color>)info;
				var b = go.AddComponent<ColourListClear>();
				return (b, lr => b.Initialise(new ListClearData<Color>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope))));
			},

			[typeof(SpriteInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (SpriteInfo)info;
				var b = go.AddComponent<SpriteBehaviour>();

				return (b, lr => b.Initialise(new SpriteData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Sprite.Resolve(vr, cr, ar, tc, scope),
					i.Size.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(AudioSourceInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (AudioSourceInfo)info;
				var b = go.AddComponent<AudioSourceBehaviour>();

				return (b, lr => b.Initialise(new AudioSourceData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Clip.Resolve(vr, cr, ar, tc, scope),
					i.PlayOnStart.Resolve(vr, cr, ar, tc, scope),
					i.Loop.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(SphereGizmoInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (SphereGizmoInfo)info;
				var b = go.AddComponent<SphereGizmoBehaviour>();

				return (b, lr => b.Initialise(new SphereGizmoData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Radius.Resolve(vr, cr, ar, tc, scope),
					i.IsWire.Resolve(vr, cr, ar, tc, scope),
					i.Colour.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(CubeGizmoInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (CubeGizmoInfo)info;
				var b = go.AddComponent<CubeGizmoBehaviour>();

				return (b, lr => b.Initialise(new CubeGizmoData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Size.Resolve(vr, cr, ar, tc, scope),
					i.IsWire.Resolve(vr, cr, ar, tc, scope),
					i.Colour.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(TextLabelInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (TextLabelInfo)info;
				var b = go.AddComponent<TextLabel>();
				return (b, lr => b.Initialise(new TextLabelData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Text.Resolve(vr, cr, ar, tc, scope),
					i.Label.Resolve(vr, cr, ar, tc, scope),
					i.FontSize.Resolve(vr, cr, ar, tc, scope),
					i.Rect)));
			},
			[typeof(ProgressBarInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ProgressBarInfo)info;
				var b = go.AddComponent<ProgressBar>();
				return (b, lr => b.Initialise(new ProgressBarData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope),
					i.Rect)));
			},
			[typeof(UIImageInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (UIImageInfo)info;
				var b = go.AddComponent<UIImage>();
				return (b, lr => b.Initialise(new UIImageData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Colour.Resolve(vr, cr, ar, tc, scope),
					i.Rect)));
			},
			[typeof(UIButtonInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (UIButtonInfo)info;
				var b = go.AddComponent<UIButton>();
				b.TriggerContext = tc;
				return (b, lr => b.Initialise(new UIButtonData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Label.Resolve(vr, cr, ar, tc, scope),
					i.Rect)));
			},
			[typeof(UIToggleInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (UIToggleInfo)info;
				var b = go.AddComponent<UIToggle>();
				b.TriggerContext = tc;
				return (b, lr => b.Initialise(new UIToggleData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.InitialValue.Resolve(vr, cr, ar, tc, scope),
					i.Label.Resolve(vr, cr, ar, tc, scope),
					i.Rect)));
			},
			[typeof(UISliderInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (UISliderInfo)info;
				var b = go.AddComponent<UISlider>();
				b.TriggerContext = tc;
				return (b, lr => b.Initialise(new UISliderData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.InitialValue.Resolve(vr, cr, ar, tc, scope),
					i.MinValue.Resolve(vr, cr, ar, tc, scope),
					i.MaxValue.Resolve(vr, cr, ar, tc, scope),
					i.Rect)));
			},
			[typeof(UIInputFieldInfo)] = (go, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (UIInputFieldInfo)info;
				var b = go.AddComponent<UIInputField>();
				b.TriggerContext = tc;
				return (b, lr => b.Initialise(new UIInputFieldData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Rect)));
			}
		};

		public static (GameBehaviour, InitialiseBehaviourEvent) Create(
			GameObject gameObject,
			BehaviourInfo behaviourInfo,
			VariableRegistry variableRegistry,
			CompiledExpressionsRegistry compiledExpressionRegistry,
			IEntitySpawner entitySpawner,
			AssetRegistry assets,
			TriggerContext triggerContext,
			EntityVariableScope? scope = null)
		{
			return Builders.TryGetValue(behaviourInfo.GetType(), out var builder)
				? builder(gameObject,
					behaviourInfo,
					variableRegistry,
					compiledExpressionRegistry,
					entitySpawner,
					assets,
					triggerContext,
					scope)
				: throw new ArgumentException($"Unsupported behaviour info type '{behaviourInfo.GetType()}'");
		}

		private static IReadOnlyList<Action> ToActions(this IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyBehaviourRegistry listenerRegistry,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			AssetRegistry assets,
			TriggerContext triggerContext,
			EntityVariableScope scope) =>
			listeners.Select(l => l switch
			{
				DirectListenerInfo direct => BuildDirectAction(direct, listenerRegistry, triggerContext),
				EntityTaggedListenerInfo entityTagged => BuildEntityTaggedAction(entityTagged,
					listenerRegistry,
					variables,
					expressions,
					assets,
					triggerContext,
					scope),
				BehaviourTaggedListenerInfo behaviourTagged => BuildBehaviourTaggedAction(behaviourTagged,
					listenerRegistry,
					variables,
					expressions,
					assets,
					triggerContext,
					scope),
				_ => throw new ArgumentException($"Unsupported listener type '{l.GetType()}'")
			}).ToArray();

		private static Action BuildDirectAction(DirectListenerInfo listener,
			IReadOnlyBehaviourRegistry registry,
			TriggerContext triggerContext)
		{
			var behaviour = registry[listener.BehaviourDescriptor];

			if (listener.OutputMapping.Count == 0)
			{
				return behaviour.Execute;
			}

			return () =>
			{
				triggerContext.ApplyMapping(listener.OutputMapping);
				behaviour.Execute();
			};
		}

		private static Action BuildEntityTaggedAction(EntityTaggedListenerInfo listener,
			IReadOnlyBehaviourRegistry registry,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			AssetRegistry assets,
			TriggerContext triggerContext,
			EntityVariableScope scope)
		{
			var entityTagProvider = listener.EntityTag.Resolve(variables, expressions, assets, triggerContext, scope);
			var behaviourId = listener.BehaviourId;

			return () =>
			{
				if (listener.OutputMapping.Count > 0)
				{
					triggerContext.ApplyMapping(listener.OutputMapping);
				}

				var entityTag = entityTagProvider.Value;
				if (entityTag == null || string.IsNullOrEmpty(behaviourId))
				{
					return;
				}

				var targets = registry.GetByEntityTagAndBehaviourId(entityTag, behaviourId);
				InvokeAll(targets);
			};
		}

		private static Action BuildBehaviourTaggedAction(BehaviourTaggedListenerInfo listener,
			IReadOnlyBehaviourRegistry registry,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			AssetRegistry assets,
			TriggerContext triggerContext,
			EntityVariableScope scope)
		{
			var behaviourTagProvider = listener.BehaviourTag.Resolve(variables, expressions, assets, triggerContext, scope);

			return () =>
			{
				if (listener.OutputMapping.Count > 0)
				{
					triggerContext.ApplyMapping(listener.OutputMapping);
				}

				var behaviourTag = behaviourTagProvider.Value;
				if (behaviourTag == null)
				{
					return;
				}

				var targets = registry.GetByBehaviourTag(behaviourTag);
				InvokeAll(targets);
			};
		}

		private static void InvokeAll(IReadOnlyList<GameBehaviour> targets)
		{
			foreach (var behaviour in targets)
			{
				if (behaviour)
				{
					behaviour.Execute();
				}
			}
		}
	}
}
