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
using Assembler.Behaviours.Rotation;
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
using Rotate = Assembler.Behaviours.Rotation.Rotate;

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
			EntityVariableScope scope,
			EntityTransformRegistry er);

		private readonly static Dictionary<Type, BehaviourBuilder> Builders = new()
		{
			[typeof(BoxColliderInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (BoxColliderInfo)info;
				var b = go.AddComponent<AutoAddBoxColliderBehaviour>();

				return (b, lr => b.Initialise(new BoxColliderData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Size.Resolve(vr, cr, ar, tc, scope, er),
					i.IsTrigger.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(SphereColliderInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (SphereColliderInfo)info;
				var b = go.AddComponent<AutoAddSphereColliderBehaviour>();

				return (b, lr => b.Initialise(new SphereColliderData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Radius.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(RigidbodyInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (RigidbodyInfo)info;
				var b = go.AddComponent<RigidbodyBehaviour>();

				return (b, lr => b.Initialise(new RigidbodyData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))
				{
					UseGravity = i.UseGravity.Resolve(vr, cr, ar, tc, scope, er)
				}));
			},
			[typeof(VelocityInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (VelocityInfo)info;
				var b = go.AddComponent<Velocity>();

				return (b, lr => b.Initialise(new VelocityData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Velocity.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(TranslateInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (TranslateInfo)info;
				var b = go.AddComponent<Translate>();

				return (b, lr => b.Initialise(new TranslateData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Displacement.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(AngularVelocityInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (AngularVelocityInfo)info;
				var b = go.AddComponent<AngularVelocity>();

				return (b, lr => b.Initialise(new AngularVelocityData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.AngularVelocity.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(RotateInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (RotateInfo)info;
				var b = go.AddComponent<Rotate>();

				return (b, lr => b.Initialise(new RotateData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Displacement.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(SetRotationInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (SetRotationInfo)info;
				var b = go.AddComponent<SetRotation>();

				return (b, lr => b.Initialise(new SetRotationData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.ValueExpression.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(MoveAnimationInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (MoveAnimationInfo)info;
				var b = go.AddComponent<MoveAnimation>();

				return (b, lr => b.Initialise(new TransformAnimationData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Start.Resolve(vr, cr, ar, tc, scope, er),
					i.End.Resolve(vr, cr, ar, tc, scope, er),
					i.Duration.Resolve(vr, cr, ar, tc, scope, er),
					i.Easing.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ScaleAnimationInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ScaleAnimationInfo)info;
				var b = go.AddComponent<ScaleAnimation>();

				return (b, lr => b.Initialise(new TransformAnimationData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Start.Resolve(vr, cr, ar, tc, scope, er),
					i.End.Resolve(vr, cr, ar, tc, scope, er),
					i.Duration.Resolve(vr, cr, ar, tc, scope, er),
					i.Easing.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(RotateAnimationInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (RotateAnimationInfo)info;
				var b = go.AddComponent<RotateAnimation>();

				return (b, lr => b.Initialise(new TransformAnimationData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Start.Resolve(vr, cr, ar, tc, scope, er),
					i.End.Resolve(vr, cr, ar, tc, scope, er),
					i.Duration.Resolve(vr, cr, ar, tc, scope, er),
					i.Easing.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(SetPositionInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (SetPositionInfo)info;
				var b = go.AddComponent<SetPosition>();

				return (b, lr => b.Initialise(new SetPositionData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.ValueExpression.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(KeyHoldTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (KeyHoldTriggerInfo)info;
				var b = go.AddComponent<KeyHoldTrigger>();

				return (b, lr => b.Initialise(new KeyHoldTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar, tc, scope, er),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(KeyDownTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (KeyDownTriggerInfo)info;
				var b = go.AddComponent<KeyDownTrigger>();

				return (b, lr => b.Initialise(new KeyDownTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar, tc, scope, er),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(KeyUpTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (KeyUpTriggerInfo)info;
				var b = go.AddComponent<KeyUpTrigger>();

				return (b, lr => b.Initialise(new KeyUpTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar, tc, scope, er),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(TapTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (TapTriggerInfo)info;
				var b = go.AddComponent<Tap>();
				return (b, lr => b.Initialise(new TapTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(DoubleTapTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (DoubleTapTriggerInfo)info;
				var b = go.AddComponent<DoubleTap>();
				return (b, lr => b.Initialise(new DoubleTapTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(LongPressTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (LongPressTriggerInfo)info;
				var b = go.AddComponent<LongPress>();
				return (b, lr => b.Initialise(new LongPressTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(SwipeTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (SwipeTriggerInfo)info;
				var b = go.AddComponent<Swipe>();
				return (b, lr => b.Initialise(new SwipeTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(DragTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (DragTriggerInfo)info;
				var b = go.AddComponent<Drag>();
				return (b, lr => b.Initialise(new DragTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(PinchTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (PinchTriggerInfo)info;
				var b = go.AddComponent<Pinch>();
				return (b, lr => b.Initialise(new PinchTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(RotateTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (RotateTriggerInfo)info;
				var b = go.AddComponent<Assembler.Behaviours.Triggers.Input.Rotate>();
				return (b, lr => b.Initialise(new RotateTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(OnStartTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (OnStartTriggerInfo)info;
				var b = go.AddComponent<OnStartTrigger>();
				return (b, lr => b.Initialise(new OnStartTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(TimerTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (TimerTriggerInfo)info;
				var b = go.AddComponent<TimerTrigger>();

				return (b, lr => b.Initialise(new TimerTriggerData(i.Id,
					i.Delay.Resolve(vr, cr, ar, tc, scope, er),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(DeferredTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (DeferredTriggerInfo)info;
				var b = go.AddComponent<DeferredTrigger>();

				return (b, lr => b.Initialise(new DeferredTriggerData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Delay.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(IntervalTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (IntervalTriggerInfo)info;
				var b = go.AddComponent<IntervalTrigger>();

				return (b, lr => b.Initialise(new IntervalTriggerData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Interval.Resolve(vr, cr, ar, tc, scope, er),
					i.Count.Resolve(vr, cr, ar, tc, scope, er),
					i.AutoStart.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(EveryFrameTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (EveryFrameTriggerInfo)info;
				var b = go.AddComponent<EveryFrameTrigger>();
				return (b, lr => b.Initialise(new EveryFrameTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(CollisionEnterTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (CollisionEnterTriggerInfo)info;
				var b = go.AddComponent<CollisionEnter>();
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new CollisionEnterTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(CollisionExitTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (CollisionExitTriggerInfo)info;
				var b = go.AddComponent<CollisionExit>();
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new CollisionExitTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(CollisionStayTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (CollisionStayTriggerInfo)info;
				var b = go.AddComponent<CollisionStay>();
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new CollisionStayTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(TriggerEnterTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (TriggerEnterTriggerInfo)info;
				var b = go.AddComponent<TriggerEnter>();
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new TriggerEnterTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(TriggerExitTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (TriggerExitTriggerInfo)info;
				var b = go.AddComponent<TriggerExit>();
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new TriggerExitTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(ConditionGateInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ConditionGateInfo)info;
				var b = go.AddComponent<ConditionGate>();

				return (b, lr => b.Initialise(new ConditionGateData(i.Id,
					i.Condition.Resolve(vr, cr, ar, tc, scope, er),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(CameraInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (CameraInfo)info;
				var b = go.AddComponent<CameraBehaviour>();

				return (b, lr => b.Initialise(new CameraData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.View.Resolve(vr, cr, ar, tc, scope, er),
					i.Size.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(SpawnerInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (SpawnerInfo)info;
				var b = go.AddComponent<SpawnerBehaviour>();
				b.Spawner = es;

				return (b, lr => b.Initialise(new SpawnerData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.TemplateId.Resolve(vr, cr, ar, tc, scope, er),
					i.Position.Resolve(vr, cr, ar, tc, scope, er),
					i.Rotation.Resolve(vr, cr, ar, tc, scope, er),
					i.Parameters.ToDictionary(kv => kv.Key,
						kv => (IValueProvider)kv.Value.Resolve(vr, cr, ar, tc, scope, er)))));
			},
			[typeof(DestroyInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (DestroyInfo)info;
				var b = go.AddComponent<DestroyBehaviour>();
				return (b, lr => b.Initialise(new DestroyData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er))));
			},
			[typeof(VariableSetterInfo<Vector3>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (VariableSetterInfo<Vector3>)info;
				var b = go.AddComponent<Vector3Setter>();

				return (b, lr => b.Initialise(new VariableSetterData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope, er),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(VariableSetterInfo<int>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (VariableSetterInfo<int>)info;
				var b = go.AddComponent<IntSetter>();

				return (b, lr => b.Initialise(new VariableSetterData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope, er),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(VariableSetterInfo<float>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (VariableSetterInfo<float>)info;
				var b = go.AddComponent<FloatSetter>();

				return (b, lr => b.Initialise(new VariableSetterData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope, er),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(VariableSetterInfo<bool>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (VariableSetterInfo<bool>)info;
				var b = go.AddComponent<BoolSetter>();

				return (b, lr => b.Initialise(new VariableSetterData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope, er),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(VariableSetterInfo<string>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (VariableSetterInfo<string>)info;
				var b = go.AddComponent<StringSetter>();

				return (b, lr => b.Initialise(new VariableSetterData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope, er),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(FormatStringSetterInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (FormatStringSetterInfo)info;
				var b = go.AddComponent<FormatStringSetter>();

				return (b, lr => b.Initialise(new FormatStringSetterData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope, er),
					i.Format.Resolve(vr, cr, ar, tc, scope, er),
					i.Arguments.Select(a => (IValueProvider)a.Resolve(vr, cr, ar, tc, scope, er)).ToArray())));
			},

			// --- List operations: Vector3 ---
			[typeof(ListAddInfo<Vector3>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListAddInfo<Vector3>)info;
				var b = go.AddComponent<Vector3ListAdd>();
				return (b, lr => b.Initialise(new ListAddData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Value.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListRemoveAtInfo<Vector3>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListRemoveAtInfo<Vector3>)info;
				var b = go.AddComponent<Vector3ListRemoveAt>();
				return (b, lr => b.Initialise(new ListRemoveAtData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Index.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListSetAtInfo<Vector3>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListSetAtInfo<Vector3>)info;
				var b = go.AddComponent<Vector3ListSetAt>();
				return (b, lr => b.Initialise(new ListSetAtData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Index.Resolve(vr, cr, ar, tc, scope, er),
					i.Value.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListClearInfo<Vector3>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListClearInfo<Vector3>)info;
				var b = go.AddComponent<Vector3ListClear>();
				return (b, lr => b.Initialise(new ListClearData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er))));
			},

			// --- List operations: int ---
			[typeof(ListAddInfo<int>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListAddInfo<int>)info;
				var b = go.AddComponent<IntListAdd>();
				return (b, lr => b.Initialise(new ListAddData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Value.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListRemoveAtInfo<int>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListRemoveAtInfo<int>)info;
				var b = go.AddComponent<IntListRemoveAt>();
				return (b, lr => b.Initialise(new ListRemoveAtData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Index.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListSetAtInfo<int>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListSetAtInfo<int>)info;
				var b = go.AddComponent<IntListSetAt>();
				return (b, lr => b.Initialise(new ListSetAtData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Index.Resolve(vr, cr, ar, tc, scope, er),
					i.Value.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListClearInfo<int>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListClearInfo<int>)info;
				var b = go.AddComponent<IntListClear>();
				return (b, lr => b.Initialise(new ListClearData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er))));
			},

			// --- List operations: float ---
			[typeof(ListAddInfo<float>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListAddInfo<float>)info;
				var b = go.AddComponent<FloatListAdd>();
				return (b, lr => b.Initialise(new ListAddData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Value.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListRemoveAtInfo<float>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListRemoveAtInfo<float>)info;
				var b = go.AddComponent<FloatListRemoveAt>();
				return (b, lr => b.Initialise(new ListRemoveAtData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Index.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListSetAtInfo<float>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListSetAtInfo<float>)info;
				var b = go.AddComponent<FloatListSetAt>();
				return (b, lr => b.Initialise(new ListSetAtData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Index.Resolve(vr, cr, ar, tc, scope, er),
					i.Value.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListClearInfo<float>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListClearInfo<float>)info;
				var b = go.AddComponent<FloatListClear>();
				return (b, lr => b.Initialise(new ListClearData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er))));
			},

			// --- List operations: bool ---
			[typeof(ListAddInfo<bool>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListAddInfo<bool>)info;
				var b = go.AddComponent<BoolListAdd>();
				return (b, lr => b.Initialise(new ListAddData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Value.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListRemoveAtInfo<bool>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListRemoveAtInfo<bool>)info;
				var b = go.AddComponent<BoolListRemoveAt>();
				return (b, lr => b.Initialise(new ListRemoveAtData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Index.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListSetAtInfo<bool>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListSetAtInfo<bool>)info;
				var b = go.AddComponent<BoolListSetAt>();
				return (b, lr => b.Initialise(new ListSetAtData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Index.Resolve(vr, cr, ar, tc, scope, er),
					i.Value.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListClearInfo<bool>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListClearInfo<bool>)info;
				var b = go.AddComponent<BoolListClear>();
				return (b, lr => b.Initialise(new ListClearData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er))));
			},

			// --- List operations: string ---
			[typeof(ListAddInfo<string>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListAddInfo<string>)info;
				var b = go.AddComponent<StringListAdd>();
				return (b, lr => b.Initialise(new ListAddData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Value.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListRemoveAtInfo<string>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListRemoveAtInfo<string>)info;
				var b = go.AddComponent<StringListRemoveAt>();
				return (b, lr => b.Initialise(new ListRemoveAtData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Index.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListSetAtInfo<string>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListSetAtInfo<string>)info;
				var b = go.AddComponent<StringListSetAt>();
				return (b, lr => b.Initialise(new ListSetAtData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Index.Resolve(vr, cr, ar, tc, scope, er),
					i.Value.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListClearInfo<string>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListClearInfo<string>)info;
				var b = go.AddComponent<StringListClear>();
				return (b, lr => b.Initialise(new ListClearData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er))));
			},

			// --- List operations: Color ---
			[typeof(ListAddInfo<Color>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListAddInfo<Color>)info;
				var b = go.AddComponent<ColourListAdd>();
				return (b, lr => b.Initialise(new ListAddData<Color>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Value.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListRemoveAtInfo<Color>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListRemoveAtInfo<Color>)info;
				var b = go.AddComponent<ColourListRemoveAt>();
				return (b, lr => b.Initialise(new ListRemoveAtData<Color>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Index.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListSetAtInfo<Color>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListSetAtInfo<Color>)info;
				var b = go.AddComponent<ColourListSetAt>();
				return (b, lr => b.Initialise(new ListSetAtData<Color>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er),
					i.Index.Resolve(vr, cr, ar, tc, scope, er),
					i.Value.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(ListClearInfo<Color>)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ListClearInfo<Color>)info;
				var b = go.AddComponent<ColourListClear>();
				return (b, lr => b.Initialise(new ListClearData<Color>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.List.Resolve(vr, cr, ar, tc, scope, er))));
			},

			[typeof(SpriteInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (SpriteInfo)info;
				var b = go.AddComponent<SpriteBehaviour>();

				return (b, lr => b.Initialise(new SpriteData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Sprite.Resolve(vr, cr, ar, tc, scope, er),
					i.Size.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(AudioSourceInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (AudioSourceInfo)info;
				var b = go.AddComponent<AudioSourceBehaviour>();

				return (b, lr => b.Initialise(new AudioSourceData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Clip.Resolve(vr, cr, ar, tc, scope, er),
					i.PlayOnStart.Resolve(vr, cr, ar, tc, scope, er),
					i.Loop.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(SphereGizmoInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (SphereGizmoInfo)info;
				var b = go.AddComponent<SphereGizmoBehaviour>();

				return (b, lr => b.Initialise(new SphereGizmoData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Radius.Resolve(vr, cr, ar, tc, scope, er),
					i.IsWire.Resolve(vr, cr, ar, tc, scope, er),
					i.Colour.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(CubeGizmoInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (CubeGizmoInfo)info;
				var b = go.AddComponent<CubeGizmoBehaviour>();

				return (b, lr => b.Initialise(new CubeGizmoData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Size.Resolve(vr, cr, ar, tc, scope, er),
					i.IsWire.Resolve(vr, cr, ar, tc, scope, er),
					i.Colour.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(LineGizmoInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (LineGizmoInfo)info;
				var b = go.AddComponent<LineGizmoBehaviour>();

				return (b, lr => b.Initialise(new LineGizmoData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Start.Resolve(vr, cr, ar, tc, scope, er),
					i.End.Resolve(vr, cr, ar, tc, scope, er),
					i.Colour.Resolve(vr, cr, ar, tc, scope, er))));
			},
			[typeof(TextLabelInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (TextLabelInfo)info;
				var b = go.AddComponent<TextLabel>();
				return (b, lr => b.Initialise(new TextLabelData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Text.Resolve(vr, cr, ar, tc, scope, er),
					i.Label.Resolve(vr, cr, ar, tc, scope, er),
					i.FontSize.Resolve(vr, cr, ar, tc, scope, er),
					i.Rect)));
			},
			[typeof(ProgressBarInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (ProgressBarInfo)info;
				var b = go.AddComponent<ProgressBar>();
				return (b, lr => b.Initialise(new ProgressBarData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Value.Resolve(vr, cr, ar, tc, scope, er),
					i.Rect)));
			},
			[typeof(UIImageInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (UIImageInfo)info;
				var b = go.AddComponent<UIImage>();
				return (b, lr => b.Initialise(new UIImageData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Colour.Resolve(vr, cr, ar, tc, scope, er),
					i.Rect)));
			},
			[typeof(UIButtonInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (UIButtonInfo)info;
				var b = go.AddComponent<UIButton>();
				b.TriggerContext = tc;
				return (b, lr => b.Initialise(new UIButtonData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Label.Resolve(vr, cr, ar, tc, scope, er),
					i.Rect)));
			},
			[typeof(UIToggleInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (UIToggleInfo)info;
				var b = go.AddComponent<UIToggle>();
				b.TriggerContext = tc;
				return (b, lr => b.Initialise(new UIToggleData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.InitialValue.Resolve(vr, cr, ar, tc, scope, er),
					i.Label.Resolve(vr, cr, ar, tc, scope, er),
					i.Rect)));
			},
			[typeof(UISliderInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (UISliderInfo)info;
				var b = go.AddComponent<UISlider>();
				b.TriggerContext = tc;
				return (b, lr => b.Initialise(new UISliderData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.InitialValue.Resolve(vr, cr, ar, tc, scope, er),
					i.MinValue.Resolve(vr, cr, ar, tc, scope, er),
					i.MaxValue.Resolve(vr, cr, ar, tc, scope, er),
					i.Rect)));
			},
			[typeof(UIInputFieldInfo)] = (go, info, vr, cr, es, ar, tc, scope, er) =>
			{
				var i = (UIInputFieldInfo)info;
				var b = go.AddComponent<UIInputField>();
				b.TriggerContext = tc;
				return (b, lr => b.Initialise(new UIInputFieldData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope, er),
					i.Rect)));
			}
		};

		// Maps each BehaviourInfo type to the concrete GameBehaviour MonoBehaviour that the Builders dictionary
		// instantiates for it. Used by doc generation (Editor/BehaviourDocs.cs) to locate the XML doc
		// comments authored on the MonoBehaviour (summary, property descriptions, trigger outputs).
		public static readonly IReadOnlyDictionary<Type, Type> MonoBehaviourByInfo = new Dictionary<Type, Type>
		{
			[typeof(BoxColliderInfo)] = typeof(AutoAddBoxColliderBehaviour),
			[typeof(SphereColliderInfo)] = typeof(AutoAddSphereColliderBehaviour),
			[typeof(RigidbodyInfo)] = typeof(RigidbodyBehaviour),
			[typeof(VelocityInfo)] = typeof(Velocity),
			[typeof(TranslateInfo)] = typeof(Translate),
			[typeof(SetPositionInfo)] = typeof(SetPosition),
			[typeof(AngularVelocityInfo)] = typeof(AngularVelocity),
			[typeof(RotateInfo)] = typeof(Rotate),
			[typeof(SetRotationInfo)] = typeof(SetRotation),
			[typeof(KeyHoldTriggerInfo)] = typeof(KeyHoldTrigger),
			[typeof(KeyDownTriggerInfo)] = typeof(KeyDownTrigger),
			[typeof(KeyUpTriggerInfo)] = typeof(KeyUpTrigger),
			[typeof(TapTriggerInfo)] = typeof(Tap),
			[typeof(DoubleTapTriggerInfo)] = typeof(DoubleTap),
			[typeof(LongPressTriggerInfo)] = typeof(LongPress),
			[typeof(SwipeTriggerInfo)] = typeof(Swipe),
			[typeof(DragTriggerInfo)] = typeof(Drag),
			[typeof(PinchTriggerInfo)] = typeof(Pinch),
			[typeof(RotateTriggerInfo)] = typeof(Rotate),
			[typeof(OnStartTriggerInfo)] = typeof(OnStartTrigger),
			[typeof(TimerTriggerInfo)] = typeof(TimerTrigger),
			[typeof(DeferredTriggerInfo)] = typeof(DeferredTrigger),
			[typeof(IntervalTriggerInfo)] = typeof(IntervalTrigger),
			[typeof(EveryFrameTriggerInfo)] = typeof(EveryFrameTrigger),
			[typeof(CollisionEnterTriggerInfo)] = typeof(CollisionEnter),
			[typeof(CollisionExitTriggerInfo)] = typeof(CollisionExit),
			[typeof(CollisionStayTriggerInfo)] = typeof(CollisionStay),
			[typeof(TriggerEnterTriggerInfo)] = typeof(TriggerEnter),
			[typeof(TriggerExitTriggerInfo)] = typeof(TriggerExit),
			[typeof(ConditionGateInfo)] = typeof(ConditionGate),
			[typeof(CameraInfo)] = typeof(CameraBehaviour),
			[typeof(SpawnerInfo)] = typeof(SpawnerBehaviour),
			[typeof(DestroyInfo)] = typeof(DestroyBehaviour),
			[typeof(VariableSetterInfo<Vector3>)] = typeof(Vector3Setter),
			[typeof(VariableSetterInfo<int>)] = typeof(IntSetter),
			[typeof(VariableSetterInfo<float>)] = typeof(FloatSetter),
			[typeof(VariableSetterInfo<bool>)] = typeof(BoolSetter),
			[typeof(VariableSetterInfo<string>)] = typeof(StringSetter),
			[typeof(FormatStringSetterInfo)] = typeof(FormatStringSetter),
			[typeof(ListAddInfo<Vector3>)] = typeof(Vector3ListAdd),
			[typeof(ListRemoveAtInfo<Vector3>)] = typeof(Vector3ListRemoveAt),
			[typeof(ListSetAtInfo<Vector3>)] = typeof(Vector3ListSetAt),
			[typeof(ListClearInfo<Vector3>)] = typeof(Vector3ListClear),
			[typeof(ListAddInfo<int>)] = typeof(IntListAdd),
			[typeof(ListRemoveAtInfo<int>)] = typeof(IntListRemoveAt),
			[typeof(ListSetAtInfo<int>)] = typeof(IntListSetAt),
			[typeof(ListClearInfo<int>)] = typeof(IntListClear),
			[typeof(ListAddInfo<float>)] = typeof(FloatListAdd),
			[typeof(ListRemoveAtInfo<float>)] = typeof(FloatListRemoveAt),
			[typeof(ListSetAtInfo<float>)] = typeof(FloatListSetAt),
			[typeof(ListClearInfo<float>)] = typeof(FloatListClear),
			[typeof(ListAddInfo<bool>)] = typeof(BoolListAdd),
			[typeof(ListRemoveAtInfo<bool>)] = typeof(BoolListRemoveAt),
			[typeof(ListSetAtInfo<bool>)] = typeof(BoolListSetAt),
			[typeof(ListClearInfo<bool>)] = typeof(BoolListClear),
			[typeof(ListAddInfo<string>)] = typeof(StringListAdd),
			[typeof(ListRemoveAtInfo<string>)] = typeof(StringListRemoveAt),
			[typeof(ListSetAtInfo<string>)] = typeof(StringListSetAt),
			[typeof(ListClearInfo<string>)] = typeof(StringListClear),
			[typeof(ListAddInfo<Color>)] = typeof(ColourListAdd),
			[typeof(ListRemoveAtInfo<Color>)] = typeof(ColourListRemoveAt),
			[typeof(ListSetAtInfo<Color>)] = typeof(ColourListSetAt),
			[typeof(ListClearInfo<Color>)] = typeof(ColourListClear),
			[typeof(SpriteInfo)] = typeof(SpriteBehaviour),
			[typeof(AudioSourceInfo)] = typeof(AudioSourceBehaviour),
			[typeof(SphereGizmoInfo)] = typeof(SphereGizmoBehaviour),
			[typeof(CubeGizmoInfo)] = typeof(CubeGizmoBehaviour),
			[typeof(LineGizmoInfo)] = typeof(LineGizmoBehaviour),
			[typeof(TextLabelInfo)] = typeof(TextLabel),
			[typeof(ProgressBarInfo)] = typeof(ProgressBar),
			[typeof(UIImageInfo)] = typeof(UIImage),
			[typeof(UIButtonInfo)] = typeof(UIButton),
			[typeof(UIToggleInfo)] = typeof(UIToggle),
			[typeof(UISliderInfo)] = typeof(UISlider),
			[typeof(UIInputFieldInfo)] = typeof(UIInputField),
		};

		public static (GameBehaviour, InitialiseBehaviourEvent) Create(
			GameObject gameObject,
			BehaviourInfo behaviourInfo,
			VariableRegistry variableRegistry,
			CompiledExpressionsRegistry compiledExpressionRegistry,
			IEntitySpawner entitySpawner,
			AssetRegistry assets,
			TriggerContext triggerContext,
			EntityTransformRegistry entityTransforms,
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
					scope,
					entityTransforms)
				: throw new ArgumentException($"Unsupported behaviour info type '{behaviourInfo.GetType()}'");
		}

		private static IReadOnlyList<Action> ToActions(this IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyBehaviourRegistry listenerRegistry,
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			AssetRegistry assets,
			TriggerContext triggerContext,
			EntityVariableScope scope,
			EntityTransformRegistry entityTransforms) =>
			listeners.Select(l => l switch
			{
				DirectListenerInfo direct => BuildDirectAction(direct, listenerRegistry, triggerContext),
				EntityTaggedListenerInfo entityTagged => BuildEntityTaggedAction(entityTagged,
					listenerRegistry,
					variables,
					expressions,
					assets,
					triggerContext,
					scope,
					entityTransforms),
				BehaviourTaggedListenerInfo behaviourTagged => BuildBehaviourTaggedAction(behaviourTagged,
					listenerRegistry,
					variables,
					expressions,
					assets,
					triggerContext,
					scope,
					entityTransforms),
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
			EntityVariableScope scope,
			EntityTransformRegistry entityTransforms)
		{
			var entityTagProvider = listener.EntityTag.Resolve(variables, expressions, assets, triggerContext, scope, entityTransforms);
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
			EntityVariableScope scope,
			EntityTransformRegistry entityTransforms)
		{
			var behaviourTagProvider = listener.BehaviourTag.Resolve(variables, expressions, assets, triggerContext, scope, entityTransforms);

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
