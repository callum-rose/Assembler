using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
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
			EntityVariableScope scope,
			GameBehaviour? existing);

		private static T GetOrAdd<T>(GameObject go, GameBehaviour? existing) where T : GameBehaviour =>
			existing as T ?? go.AddComponent<T>();

		private readonly static Dictionary<Type, BehaviourBuilder> Builders = new()
		{
			[typeof(BoxColliderInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (BoxColliderInfo)info;
				var b = GetOrAdd<AutoAddBoxColliderBehaviour>(go, existing);

				return (b, lr => b.Initialise(new BoxColliderData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Size.Resolve(vr, cr, ar, tc, scope),
					i.IsTrigger.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(SphereColliderInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (SphereColliderInfo)info;
				var b = GetOrAdd<AutoAddSphereColliderBehaviour>(go, existing);

				return (b, lr => b.Initialise(new SphereColliderData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Radius.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(RigidbodyInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (RigidbodyInfo)info;
				var b = GetOrAdd<RigidbodyBehaviour>(go, existing);

				return (b, lr => b.Initialise(new RigidbodyData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))
				{
					UseGravity = i.UseGravity.Resolve(vr, cr, ar, tc, scope)
				}));
			},
			[typeof(VelocityInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (VelocityInfo)info;
				var b = GetOrAdd<Velocity>(go, existing);

				return (b, lr => b.Initialise(new VelocityData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Velocity.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(TranslateInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (TranslateInfo)info;
				var b = GetOrAdd<Translate>(go, existing);

				return (b, lr => b.Initialise(new TranslateData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Displacement.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(SetPositionInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (SetPositionInfo)info;
				var b = GetOrAdd<SetPosition>(go, existing);

				return (b, lr => b.Initialise(new SetPositionData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueExpression.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(KeyHoldTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (KeyHoldTriggerInfo)info;
				var b = GetOrAdd<KeyHoldTrigger>(go, existing);

				return (b, lr => b.Initialise(new KeyHoldTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar, tc, scope),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(KeyDownTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (KeyDownTriggerInfo)info;
				var b = GetOrAdd<KeyDownTrigger>(go, existing);

				return (b, lr => b.Initialise(new KeyDownTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar, tc, scope),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(KeyUpTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (KeyUpTriggerInfo)info;
				var b = GetOrAdd<KeyUpTrigger>(go, existing);

				return (b, lr => b.Initialise(new KeyUpTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar, tc, scope),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(TapTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (TapTriggerInfo)info;
				var b = GetOrAdd<Tap>(go, existing);
				return (b, lr => b.Initialise(new TapTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(DoubleTapTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (DoubleTapTriggerInfo)info;
				var b = GetOrAdd<DoubleTap>(go, existing);
				return (b, lr => b.Initialise(new DoubleTapTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(LongPressTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (LongPressTriggerInfo)info;
				var b = GetOrAdd<LongPress>(go, existing);
				return (b, lr => b.Initialise(new LongPressTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(SwipeTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (SwipeTriggerInfo)info;
				var b = GetOrAdd<Swipe>(go, existing);
				return (b, lr => b.Initialise(new SwipeTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(DragTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (DragTriggerInfo)info;
				var b = GetOrAdd<Drag>(go, existing);
				return (b, lr => b.Initialise(new DragTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(PinchTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (PinchTriggerInfo)info;
				var b = GetOrAdd<Pinch>(go, existing);
				return (b, lr => b.Initialise(new PinchTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(RotateTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (RotateTriggerInfo)info;
				var b = GetOrAdd<Rotate>(go, existing);
				return (b, lr => b.Initialise(new RotateTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(OnStartTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (OnStartTriggerInfo)info;
				var b = GetOrAdd<OnStartTrigger>(go, existing);
				return (b, lr => b.Initialise(new OnStartTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(TimerTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (TimerTriggerInfo)info;
				var b = GetOrAdd<TimerTrigger>(go, existing);

				return (b, lr => b.Initialise(new TimerTriggerData(i.Id,
					i.Delay.Resolve(vr, cr, ar, tc, scope),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(DeferredTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (DeferredTriggerInfo)info;
				var b = GetOrAdd<DeferredTrigger>(go, existing);

				return (b, lr => b.Initialise(new DeferredTriggerData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Delay.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(IntervalTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (IntervalTriggerInfo)info;
				var b = GetOrAdd<IntervalTrigger>(go, existing);

				return (b, lr => b.Initialise(new IntervalTriggerData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Interval.Resolve(vr, cr, ar, tc, scope),
					i.Count.Resolve(vr, cr, ar, tc, scope),
					i.AutoStart.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(EveryFrameTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (EveryFrameTriggerInfo)info;
				var b = GetOrAdd<EveryFrameTrigger>(go, existing);
				return (b, lr => b.Initialise(new EveryFrameTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(CollisionEnterTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (CollisionEnterTriggerInfo)info;
				var b = GetOrAdd<CollisionEnter>(go, existing);
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new CollisionEnterTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(CollisionExitTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (CollisionExitTriggerInfo)info;
				var b = GetOrAdd<CollisionExit>(go, existing);
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new CollisionExitTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(CollisionStayTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (CollisionStayTriggerInfo)info;
				var b = GetOrAdd<CollisionStay>(go, existing);
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new CollisionStayTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(TriggerEnterTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (TriggerEnterTriggerInfo)info;
				var b = GetOrAdd<TriggerEnter>(go, existing);
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new TriggerEnterTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(TriggerExitTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (TriggerExitTriggerInfo)info;
				var b = GetOrAdd<TriggerExit>(go, existing);
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new TriggerExitTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(ConditionTriggerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ConditionTriggerInfo)info;
				var b = GetOrAdd<Condition>(go, existing);

				return (b, lr => b.Initialise(new ConditionData(i.Id,
					i.Condition.Resolve(vr, cr, ar, tc, scope),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(CameraInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (CameraInfo)info;
				var b = GetOrAdd<CameraBehaviour>(go, existing);

				return (b, lr => b.Initialise(new CameraData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.View.Resolve(vr, cr, ar, tc, scope),
					i.Size.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(SpawnerInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (SpawnerInfo)info;
				var b = GetOrAdd<SpawnerBehaviour>(go, existing);
				b.Spawner = es;

				return (b, lr => b.Initialise(new SpawnerData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.TemplateId.Resolve(vr, cr, ar, tc, scope),
					i.Position.Resolve(vr, cr, ar, tc, scope),
					i.Rotation.Resolve(vr, cr, ar, tc, scope),
					i.Parameters.ToDictionary(kv => kv.Key,
						kv => (IValueProvider)kv.Value.Resolve(vr, cr, ar, tc, scope)))));
			},
			[typeof(DestroyInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (DestroyInfo)info;
				var b = GetOrAdd<DestroyBehaviour>(go, existing);
				b.Spawner = es;
				return (b, lr => b.Initialise(new DestroyData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))));
			},
			[typeof(VariableSetterInfo<Vector3>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (VariableSetterInfo<Vector3>)info;
				var b = GetOrAdd<Vector3Setter>(go, existing);

				return (b, lr => b.Initialise(new VariableSetterData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(VariableSetterInfo<int>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (VariableSetterInfo<int>)info;
				var b = GetOrAdd<IntSetter>(go, existing);

				return (b, lr => b.Initialise(new VariableSetterData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(VariableSetterInfo<float>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (VariableSetterInfo<float>)info;
				var b = GetOrAdd<FloatSetter>(go, existing);

				return (b, lr => b.Initialise(new VariableSetterData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(VariableSetterInfo<bool>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (VariableSetterInfo<bool>)info;
				var b = GetOrAdd<BoolSetter>(go, existing);

				return (b, lr => b.Initialise(new VariableSetterData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(VariableSetterInfo<string>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (VariableSetterInfo<string>)info;
				var b = GetOrAdd<StringSetter>(go, existing);

				return (b, lr => b.Initialise(new VariableSetterData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope))));
			},

			// --- List operations: Vector3 ---
			[typeof(ListAddInfo<Vector3>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListAddInfo<Vector3>)info;
				var b = GetOrAdd<Vector3ListAdd>(go, existing);
				return (b, lr => b.Initialise(new ListAddData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListRemoveAtInfo<Vector3>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListRemoveAtInfo<Vector3>)info;
				var b = GetOrAdd<Vector3ListRemoveAt>(go, existing);
				return (b, lr => b.Initialise(new ListRemoveAtData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListSetAtInfo<Vector3>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListSetAtInfo<Vector3>)info;
				var b = GetOrAdd<Vector3ListSetAt>(go, existing);
				return (b, lr => b.Initialise(new ListSetAtData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListClearInfo<Vector3>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListClearInfo<Vector3>)info;
				var b = GetOrAdd<Vector3ListClear>(go, existing);
				return (b, lr => b.Initialise(new ListClearData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope))));
			},

			// --- List operations: int ---
			[typeof(ListAddInfo<int>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListAddInfo<int>)info;
				var b = GetOrAdd<IntListAdd>(go, existing);
				return (b, lr => b.Initialise(new ListAddData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListRemoveAtInfo<int>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListRemoveAtInfo<int>)info;
				var b = GetOrAdd<IntListRemoveAt>(go, existing);
				return (b, lr => b.Initialise(new ListRemoveAtData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListSetAtInfo<int>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListSetAtInfo<int>)info;
				var b = GetOrAdd<IntListSetAt>(go, existing);
				return (b, lr => b.Initialise(new ListSetAtData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListClearInfo<int>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListClearInfo<int>)info;
				var b = GetOrAdd<IntListClear>(go, existing);
				return (b, lr => b.Initialise(new ListClearData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope))));
			},

			// --- List operations: float ---
			[typeof(ListAddInfo<float>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListAddInfo<float>)info;
				var b = GetOrAdd<FloatListAdd>(go, existing);
				return (b, lr => b.Initialise(new ListAddData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListRemoveAtInfo<float>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListRemoveAtInfo<float>)info;
				var b = GetOrAdd<FloatListRemoveAt>(go, existing);
				return (b, lr => b.Initialise(new ListRemoveAtData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListSetAtInfo<float>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListSetAtInfo<float>)info;
				var b = GetOrAdd<FloatListSetAt>(go, existing);
				return (b, lr => b.Initialise(new ListSetAtData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListClearInfo<float>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListClearInfo<float>)info;
				var b = GetOrAdd<FloatListClear>(go, existing);
				return (b, lr => b.Initialise(new ListClearData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope))));
			},

			// --- List operations: bool ---
			[typeof(ListAddInfo<bool>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListAddInfo<bool>)info;
				var b = GetOrAdd<BoolListAdd>(go, existing);
				return (b, lr => b.Initialise(new ListAddData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListRemoveAtInfo<bool>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListRemoveAtInfo<bool>)info;
				var b = GetOrAdd<BoolListRemoveAt>(go, existing);
				return (b, lr => b.Initialise(new ListRemoveAtData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListSetAtInfo<bool>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListSetAtInfo<bool>)info;
				var b = GetOrAdd<BoolListSetAt>(go, existing);
				return (b, lr => b.Initialise(new ListSetAtData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListClearInfo<bool>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListClearInfo<bool>)info;
				var b = GetOrAdd<BoolListClear>(go, existing);
				return (b, lr => b.Initialise(new ListClearData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope))));
			},

			// --- List operations: string ---
			[typeof(ListAddInfo<string>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListAddInfo<string>)info;
				var b = GetOrAdd<StringListAdd>(go, existing);
				return (b, lr => b.Initialise(new ListAddData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListRemoveAtInfo<string>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListRemoveAtInfo<string>)info;
				var b = GetOrAdd<StringListRemoveAt>(go, existing);
				return (b, lr => b.Initialise(new ListRemoveAtData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListSetAtInfo<string>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListSetAtInfo<string>)info;
				var b = GetOrAdd<StringListSetAt>(go, existing);
				return (b, lr => b.Initialise(new ListSetAtData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListClearInfo<string>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListClearInfo<string>)info;
				var b = GetOrAdd<StringListClear>(go, existing);
				return (b, lr => b.Initialise(new ListClearData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope))));
			},

			// --- List operations: Color ---
			[typeof(ListAddInfo<Color>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListAddInfo<Color>)info;
				var b = GetOrAdd<ColourListAdd>(go, existing);
				return (b, lr => b.Initialise(new ListAddData<Color>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListRemoveAtInfo<Color>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListRemoveAtInfo<Color>)info;
				var b = GetOrAdd<ColourListRemoveAt>(go, existing);
				return (b, lr => b.Initialise(new ListRemoveAtData<Color>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListSetAtInfo<Color>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListSetAtInfo<Color>)info;
				var b = GetOrAdd<ColourListSetAt>(go, existing);
				return (b, lr => b.Initialise(new ListSetAtData<Color>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(ListClearInfo<Color>)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ListClearInfo<Color>)info;
				var b = GetOrAdd<ColourListClear>(go, existing);
				return (b, lr => b.Initialise(new ListClearData<Color>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope))));
			},

			[typeof(SpriteInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (SpriteInfo)info;
				var b = GetOrAdd<SpriteBehaviour>(go, existing);

				return (b, lr => b.Initialise(new SpriteData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Sprite.Resolve(vr, cr, ar, tc, scope),
					i.Size.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(AudioSourceInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (AudioSourceInfo)info;
				var b = GetOrAdd<AudioSourceBehaviour>(go, existing);

				return (b, lr => b.Initialise(new AudioSourceData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Clip.Resolve(vr, cr, ar, tc, scope),
					i.PlayOnStart.Resolve(vr, cr, ar, tc, scope),
					i.Loop.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(SphereGizmoInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (SphereGizmoInfo)info;
				var b = GetOrAdd<SphereGizmoBehaviour>(go, existing);

				return (b, lr => b.Initialise(new SphereGizmoData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Radius.Resolve(vr, cr, ar, tc, scope),
					i.IsWire.Resolve(vr, cr, ar, tc, scope),
					i.Colour.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(CubeGizmoInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (CubeGizmoInfo)info;
				var b = GetOrAdd<CubeGizmoBehaviour>(go, existing);

				return (b, lr => b.Initialise(new CubeGizmoData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Size.Resolve(vr, cr, ar, tc, scope),
					i.IsWire.Resolve(vr, cr, ar, tc, scope),
					i.Colour.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(TextLabelInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (TextLabelInfo)info;
				var b = GetOrAdd<TextLabel>(go, existing);
				return (b, lr => b.Initialise(new TextLabelData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Text.Resolve(vr, cr, ar, tc, scope),
					i.Label.Resolve(vr, cr, ar, tc, scope),
					i.FontSize.Resolve(vr, cr, ar, tc, scope),
					i.Rect)));
			},
			[typeof(ProgressBarInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (ProgressBarInfo)info;
				var b = GetOrAdd<ProgressBar>(go, existing);
				return (b, lr => b.Initialise(new ProgressBarData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope),
					i.Rect)));
			},
			[typeof(UIImageInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (UIImageInfo)info;
				var b = GetOrAdd<UIImage>(go, existing);
				return (b, lr => b.Initialise(new UIImageData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Colour.Resolve(vr, cr, ar, tc, scope),
					i.Rect)));
			},
			[typeof(UIButtonInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (UIButtonInfo)info;
				var b = GetOrAdd<UIButton>(go, existing);
				b.TriggerContext = tc;
				return (b, lr => b.Initialise(new UIButtonData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Label.Resolve(vr, cr, ar, tc, scope),
					i.Rect)));
			},
			[typeof(UIToggleInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (UIToggleInfo)info;
				var b = GetOrAdd<UIToggle>(go, existing);
				b.TriggerContext = tc;
				return (b, lr => b.Initialise(new UIToggleData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.InitialValue.Resolve(vr, cr, ar, tc, scope),
					i.Label.Resolve(vr, cr, ar, tc, scope),
					i.Rect)));
			},
			[typeof(UISliderInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (UISliderInfo)info;
				var b = GetOrAdd<UISlider>(go, existing);
				b.TriggerContext = tc;
				return (b, lr => b.Initialise(new UISliderData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.InitialValue.Resolve(vr, cr, ar, tc, scope),
					i.MinValue.Resolve(vr, cr, ar, tc, scope),
					i.MaxValue.Resolve(vr, cr, ar, tc, scope),
					i.Rect)));
			},
			[typeof(UIInputFieldInfo)] = (go, info, vr, cr, es, ar, tc, scope, existing) =>
			{
				var i = (UIInputFieldInfo)info;
				var b = GetOrAdd<UIInputField>(go, existing);
				b.TriggerContext = tc;
				return (b, lr => b.Initialise(new UIInputFieldData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
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
			[typeof(ConditionTriggerInfo)] = typeof(Condition),
			[typeof(CameraInfo)] = typeof(CameraBehaviour),
			[typeof(SpawnerInfo)] = typeof(SpawnerBehaviour),
			[typeof(DestroyInfo)] = typeof(DestroyBehaviour),
			[typeof(VariableSetterInfo<Vector3>)] = typeof(Vector3Setter),
			[typeof(VariableSetterInfo<int>)] = typeof(IntSetter),
			[typeof(VariableSetterInfo<float>)] = typeof(FloatSetter),
			[typeof(VariableSetterInfo<bool>)] = typeof(BoolSetter),
			[typeof(VariableSetterInfo<string>)] = typeof(StringSetter),
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
			EntityVariableScope? scope = null,
			GameBehaviour? existing = null)
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
					existing)
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
