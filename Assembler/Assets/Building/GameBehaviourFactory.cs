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
		private delegate InitialiseBehaviourEvent BehaviourInitialiser(
			GameBehaviour behaviour,
			BehaviourInfo info,
			VariableRegistry vr,
			CompiledExpressionsRegistry cr,
			IEntitySpawner spawner,
			AssetRegistry ar,
			TriggerContext tc,
			EntityVariableScope scope);

		private readonly static Dictionary<Type, BehaviourInitialiser> Builders = new()
		{
			[typeof(BoxColliderInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (BoxColliderInfo)info;
				var b = (AutoAddBoxColliderBehaviour)behaviour;

				return lr => b.Initialise(new BoxColliderData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Size.Resolve(vr, cr, ar, tc, scope),
					i.IsTrigger.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(SphereColliderInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (SphereColliderInfo)info;
				var b = (AutoAddSphereColliderBehaviour)behaviour;

				return lr => b.Initialise(new SphereColliderData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Radius.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(RigidbodyInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (RigidbodyInfo)info;
				var b = (RigidbodyBehaviour)behaviour;

				return lr => b.Initialise(new RigidbodyData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope))
				{
					UseGravity = i.UseGravity.Resolve(vr, cr, ar, tc, scope)
				});
			},
			[typeof(VelocityInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (VelocityInfo)info;
				var b = (Velocity)behaviour;

				return lr => b.Initialise(new VelocityData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Velocity.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(TranslateInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (TranslateInfo)info;
				var b = (Translate)behaviour;

				return lr => b.Initialise(new TranslateData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Displacement.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(SetPositionInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (SetPositionInfo)info;
				var b = (SetPosition)behaviour;

				return lr => b.Initialise(new SetPositionData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueExpression.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(KeyHoldTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (KeyHoldTriggerInfo)info;
				var b = (KeyHoldTrigger)behaviour;

				return lr => b.Initialise(new KeyHoldTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar, tc, scope),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(KeyDownTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (KeyDownTriggerInfo)info;
				var b = (KeyDownTrigger)behaviour;

				return lr => b.Initialise(new KeyDownTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar, tc, scope),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(KeyUpTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (KeyUpTriggerInfo)info;
				var b = (KeyUpTrigger)behaviour;

				return lr => b.Initialise(new KeyUpTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar, tc, scope),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(TapTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (TapTriggerInfo)info;
				var b = (Tap)behaviour;
				return lr => b.Initialise(new TapTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(DoubleTapTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (DoubleTapTriggerInfo)info;
				var b = (DoubleTap)behaviour;
				return lr => b.Initialise(new DoubleTapTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(LongPressTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (LongPressTriggerInfo)info;
				var b = (LongPress)behaviour;
				return lr => b.Initialise(new LongPressTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(SwipeTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (SwipeTriggerInfo)info;
				var b = (Swipe)behaviour;
				return lr => b.Initialise(new SwipeTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(DragTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (DragTriggerInfo)info;
				var b = (Drag)behaviour;
				return lr => b.Initialise(new DragTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(PinchTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (PinchTriggerInfo)info;
				var b = (Pinch)behaviour;
				return lr => b.Initialise(new PinchTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(RotateTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (RotateTriggerInfo)info;
				var b = (Rotate)behaviour;
				return lr => b.Initialise(new RotateTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(OnStartTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (OnStartTriggerInfo)info;
				var b = (OnStartTrigger)behaviour;
				return lr => b.Initialise(new OnStartTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(TimerTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (TimerTriggerInfo)info;
				var b = (TimerTrigger)behaviour;

				return lr => b.Initialise(new TimerTriggerData(i.Id,
					i.Delay.Resolve(vr, cr, ar, tc, scope),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(DeferredTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (DeferredTriggerInfo)info;
				var b = (DeferredTrigger)behaviour;

				return lr => b.Initialise(new DeferredTriggerData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Delay.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(IntervalTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (IntervalTriggerInfo)info;
				var b = (IntervalTrigger)behaviour;

				return lr => b.Initialise(new IntervalTriggerData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Interval.Resolve(vr, cr, ar, tc, scope),
					i.Count.Resolve(vr, cr, ar, tc, scope),
					i.AutoStart.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(EveryFrameTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (EveryFrameTriggerInfo)info;
				var b = (EveryFrameTrigger)behaviour;
				return lr => b.Initialise(new EveryFrameTriggerData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(CollisionEnterTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (CollisionEnterTriggerInfo)info;
				var b = (CollisionEnter)behaviour;
				b.TriggerContext = tc;

				return lr => b.Initialise(new CollisionEnterTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(CollisionExitTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (CollisionExitTriggerInfo)info;
				var b = (CollisionExit)behaviour;
				b.TriggerContext = tc;

				return lr => b.Initialise(new CollisionExitTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(CollisionStayTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (CollisionStayTriggerInfo)info;
				var b = (CollisionStay)behaviour;
				b.TriggerContext = tc;

				return lr => b.Initialise(new CollisionStayTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(TriggerEnterTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (TriggerEnterTriggerInfo)info;
				var b = (TriggerEnter)behaviour;
				b.TriggerContext = tc;

				return lr => b.Initialise(new TriggerEnterTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(TriggerExitTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (TriggerExitTriggerInfo)info;
				var b = (TriggerExit)behaviour;
				b.TriggerContext = tc;

				return lr => b.Initialise(new TriggerExitTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(ConditionTriggerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ConditionTriggerInfo)info;
				var b = (Condition)behaviour;

				return lr => b.Initialise(new ConditionData(i.Id,
					i.Condition.Resolve(vr, cr, ar, tc, scope),
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(CameraInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (CameraInfo)info;
				var b = (CameraBehaviour)behaviour;

				return lr => b.Initialise(new CameraData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.View.Resolve(vr, cr, ar, tc, scope),
					i.Size.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(SpawnerInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (SpawnerInfo)info;
				var b = (SpawnerBehaviour)behaviour;
				b.Spawner = es;

				return lr => b.Initialise(new SpawnerData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.TemplateId.Resolve(vr, cr, ar, tc, scope),
					i.Position.Resolve(vr, cr, ar, tc, scope),
					i.Rotation.Resolve(vr, cr, ar, tc, scope),
					i.Parameters.ToDictionary(kv => kv.Key,
						kv => (IValueProvider)kv.Value.Resolve(vr, cr, ar, tc, scope))));
			},
			[typeof(DestroyInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (DestroyInfo)info;
				var b = (DestroyBehaviour)behaviour;
				b.Spawner = es;
				return lr => b.Initialise(new DestroyData(i.Id, i.Listeners.ToActions(lr, vr, cr, ar, tc, scope)));
			},
			[typeof(VariableSetterInfo<Vector3>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (VariableSetterInfo<Vector3>)info;
				var b = (Vector3Setter)behaviour;

				return lr => b.Initialise(new VariableSetterData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(VariableSetterInfo<int>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (VariableSetterInfo<int>)info;
				var b = (IntSetter)behaviour;

				return lr => b.Initialise(new VariableSetterData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(VariableSetterInfo<float>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (VariableSetterInfo<float>)info;
				var b = (FloatSetter)behaviour;

				return lr => b.Initialise(new VariableSetterData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(VariableSetterInfo<bool>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (VariableSetterInfo<bool>)info;
				var b = (BoolSetter)behaviour;

				return lr => b.Initialise(new VariableSetterData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(VariableSetterInfo<string>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (VariableSetterInfo<string>)info;
				var b = (StringSetter)behaviour;

				return lr => b.Initialise(new VariableSetterData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.ValueToSet.Resolve(vr, cr, ar, tc, scope),
					i.ValueToGet.Resolve(vr, cr, ar, tc, scope)));
			},

			// --- List operations: Vector3 ---
			[typeof(ListAddInfo<Vector3>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListAddInfo<Vector3>)info;
				var b = (Vector3ListAdd)behaviour;
				return lr => b.Initialise(new ListAddData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListRemoveAtInfo<Vector3>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListRemoveAtInfo<Vector3>)info;
				var b = (Vector3ListRemoveAt)behaviour;
				return lr => b.Initialise(new ListRemoveAtData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListSetAtInfo<Vector3>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListSetAtInfo<Vector3>)info;
				var b = (Vector3ListSetAt)behaviour;
				return lr => b.Initialise(new ListSetAtData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListClearInfo<Vector3>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListClearInfo<Vector3>)info;
				var b = (Vector3ListClear)behaviour;
				return lr => b.Initialise(new ListClearData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope)));
			},

			// --- List operations: int ---
			[typeof(ListAddInfo<int>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListAddInfo<int>)info;
				var b = (IntListAdd)behaviour;
				return lr => b.Initialise(new ListAddData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListRemoveAtInfo<int>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListRemoveAtInfo<int>)info;
				var b = (IntListRemoveAt)behaviour;
				return lr => b.Initialise(new ListRemoveAtData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListSetAtInfo<int>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListSetAtInfo<int>)info;
				var b = (IntListSetAt)behaviour;
				return lr => b.Initialise(new ListSetAtData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListClearInfo<int>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListClearInfo<int>)info;
				var b = (IntListClear)behaviour;
				return lr => b.Initialise(new ListClearData<int>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope)));
			},

			// --- List operations: float ---
			[typeof(ListAddInfo<float>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListAddInfo<float>)info;
				var b = (FloatListAdd)behaviour;
				return lr => b.Initialise(new ListAddData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListRemoveAtInfo<float>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListRemoveAtInfo<float>)info;
				var b = (FloatListRemoveAt)behaviour;
				return lr => b.Initialise(new ListRemoveAtData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListSetAtInfo<float>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListSetAtInfo<float>)info;
				var b = (FloatListSetAt)behaviour;
				return lr => b.Initialise(new ListSetAtData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListClearInfo<float>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListClearInfo<float>)info;
				var b = (FloatListClear)behaviour;
				return lr => b.Initialise(new ListClearData<float>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope)));
			},

			// --- List operations: bool ---
			[typeof(ListAddInfo<bool>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListAddInfo<bool>)info;
				var b = (BoolListAdd)behaviour;
				return lr => b.Initialise(new ListAddData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListRemoveAtInfo<bool>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListRemoveAtInfo<bool>)info;
				var b = (BoolListRemoveAt)behaviour;
				return lr => b.Initialise(new ListRemoveAtData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListSetAtInfo<bool>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListSetAtInfo<bool>)info;
				var b = (BoolListSetAt)behaviour;
				return lr => b.Initialise(new ListSetAtData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListClearInfo<bool>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListClearInfo<bool>)info;
				var b = (BoolListClear)behaviour;
				return lr => b.Initialise(new ListClearData<bool>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope)));
			},

			// --- List operations: string ---
			[typeof(ListAddInfo<string>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListAddInfo<string>)info;
				var b = (StringListAdd)behaviour;
				return lr => b.Initialise(new ListAddData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListRemoveAtInfo<string>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListRemoveAtInfo<string>)info;
				var b = (StringListRemoveAt)behaviour;
				return lr => b.Initialise(new ListRemoveAtData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListSetAtInfo<string>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListSetAtInfo<string>)info;
				var b = (StringListSetAt)behaviour;
				return lr => b.Initialise(new ListSetAtData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListClearInfo<string>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListClearInfo<string>)info;
				var b = (StringListClear)behaviour;
				return lr => b.Initialise(new ListClearData<string>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope)));
			},

			// --- List operations: Color ---
			[typeof(ListAddInfo<Color>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListAddInfo<Color>)info;
				var b = (ColourListAdd)behaviour;
				return lr => b.Initialise(new ListAddData<Color>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListRemoveAtInfo<Color>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListRemoveAtInfo<Color>)info;
				var b = (ColourListRemoveAt)behaviour;
				return lr => b.Initialise(new ListRemoveAtData<Color>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListSetAtInfo<Color>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListSetAtInfo<Color>)info;
				var b = (ColourListSetAt)behaviour;
				return lr => b.Initialise(new ListSetAtData<Color>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope),
					i.Index.Resolve(vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(ListClearInfo<Color>)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ListClearInfo<Color>)info;
				var b = (ColourListClear)behaviour;
				return lr => b.Initialise(new ListClearData<Color>(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.List.Resolve(vr, cr, ar, tc, scope)));
			},

			[typeof(SpriteInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (SpriteInfo)info;
				var b = (SpriteBehaviour)behaviour;

				return lr => b.Initialise(new SpriteData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Sprite.Resolve(vr, cr, ar, tc, scope),
					i.Size.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(AudioSourceInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (AudioSourceInfo)info;
				var b = (AudioSourceBehaviour)behaviour;

				return lr => b.Initialise(new AudioSourceData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Clip.Resolve(vr, cr, ar, tc, scope),
					i.PlayOnStart.Resolve(vr, cr, ar, tc, scope),
					i.Loop.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(SphereGizmoInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (SphereGizmoInfo)info;
				var b = (SphereGizmoBehaviour)behaviour;

				return lr => b.Initialise(new SphereGizmoData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Radius.Resolve(vr, cr, ar, tc, scope),
					i.IsWire.Resolve(vr, cr, ar, tc, scope),
					i.Colour.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(CubeGizmoInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (CubeGizmoInfo)info;
				var b = (CubeGizmoBehaviour)behaviour;

				return lr => b.Initialise(new CubeGizmoData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Size.Resolve(vr, cr, ar, tc, scope),
					i.IsWire.Resolve(vr, cr, ar, tc, scope),
					i.Colour.Resolve(vr, cr, ar, tc, scope)));
			},
			[typeof(TextLabelInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (TextLabelInfo)info;
				var b = (TextLabel)behaviour;
				return lr => b.Initialise(new TextLabelData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Text.Resolve(vr, cr, ar, tc, scope),
					i.Label.Resolve(vr, cr, ar, tc, scope),
					i.FontSize.Resolve(vr, cr, ar, tc, scope),
					i.Rect));
			},
			[typeof(ProgressBarInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (ProgressBarInfo)info;
				var b = (ProgressBar)behaviour;
				return lr => b.Initialise(new ProgressBarData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Value.Resolve(vr, cr, ar, tc, scope),
					i.Rect));
			},
			[typeof(UIImageInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (UIImageInfo)info;
				var b = (UIImage)behaviour;
				return lr => b.Initialise(new UIImageData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Colour.Resolve(vr, cr, ar, tc, scope),
					i.Rect));
			},
			[typeof(UIButtonInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (UIButtonInfo)info;
				var b = (UIButton)behaviour;
				b.TriggerContext = tc;
				return lr => b.Initialise(new UIButtonData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Label.Resolve(vr, cr, ar, tc, scope),
					i.Rect));
			},
			[typeof(UIToggleInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (UIToggleInfo)info;
				var b = (UIToggle)behaviour;
				b.TriggerContext = tc;
				return lr => b.Initialise(new UIToggleData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.InitialValue.Resolve(vr, cr, ar, tc, scope),
					i.Label.Resolve(vr, cr, ar, tc, scope),
					i.Rect));
			},
			[typeof(UISliderInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (UISliderInfo)info;
				var b = (UISlider)behaviour;
				b.TriggerContext = tc;
				return lr => b.Initialise(new UISliderData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.InitialValue.Resolve(vr, cr, ar, tc, scope),
					i.MinValue.Resolve(vr, cr, ar, tc, scope),
					i.MaxValue.Resolve(vr, cr, ar, tc, scope),
					i.Rect));
			},
			[typeof(UIInputFieldInfo)] = (behaviour, info, vr, cr, es, ar, tc, scope) =>
			{
				var i = (UIInputFieldInfo)info;
				var b = (UIInputField)behaviour;
				b.TriggerContext = tc;
				return lr => b.Initialise(new UIInputFieldData(i.Id,
					i.Listeners.ToActions(lr, vr, cr, ar, tc, scope),
					i.Rect));
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

		public static GameBehaviour Instantiate(GameObject gameObject, BehaviourInfo behaviourInfo)
		{
			if (!MonoBehaviourByInfo.TryGetValue(behaviourInfo.GetType(), out var componentType))
			{
				throw new ArgumentException($"Unsupported behaviour info type '{behaviourInfo.GetType()}'");
			}

			return (GameBehaviour)gameObject.AddComponent(componentType);
		}

		public static InitialiseBehaviourEvent BuildInitialise(
			GameBehaviour behaviour,
			BehaviourInfo behaviourInfo,
			VariableRegistry variableRegistry,
			CompiledExpressionsRegistry compiledExpressionRegistry,
			IEntitySpawner entitySpawner,
			AssetRegistry assets,
			TriggerContext triggerContext,
			EntityVariableScope scope)
		{
			if (!Builders.TryGetValue(behaviourInfo.GetType(), out var initialiser))
			{
				throw new ArgumentException($"Unsupported behaviour info type '{behaviourInfo.GetType()}'");
			}

			return initialiser(behaviour,
				behaviourInfo,
				variableRegistry,
				compiledExpressionRegistry,
				entitySpawner,
				assets,
				triggerContext,
				scope);
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
