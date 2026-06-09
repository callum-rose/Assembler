using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.AI;
using Assembler.Behaviours.Animations;
using Assembler.Behaviours.Audio;
using Assembler.Behaviours.Camera;
using Assembler.Behaviours.Debug;
using Assembler.Behaviours.ListOperations;
using Assembler.Behaviours.Movement;
using Assembler.Behaviours.Physics;
using Assembler.Behaviours.Rotation;
using Assembler.Behaviours.Spawners;
using Assembler.Behaviours.Sprites;
using Assembler.Behaviours.Time;
using Assembler.Behaviours.Triggers;
using Assembler.Behaviours.Triggers.Conditionals;
using Assembler.Behaviours.Triggers.Input;
using Assembler.Behaviours.Triggers.Input.Touch;
using Assembler.Behaviours.Triggers.Physical;
using Assembler.Behaviours.Triggers.Timing;
using Assembler.Behaviours.Triggers.Variables;
using Assembler.Behaviours.UI;
using Assembler.Behaviours.VariableUpdaters;
using Assembler.Behaviours.Visual;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Time;
using UnityEngine;
using Rotate = Assembler.Behaviours.Rotation.Rotate;

namespace Assembler.Building
{

	public static class GameBehaviourFactory
	{
		private delegate (GameBehaviour, InitialiseBehaviourEvent) BehaviourBuilder(
			GameObject go,
			BehaviourInfo info,
			BehaviourBuildContext ctx);

		private sealed record BuilderEntry(Type MonoBehaviourType, BehaviourBuilder Build);

		private readonly static Dictionary<Type, BuilderEntry> Builders = CreateBuilders();

		// Maps each BehaviourInfo type to the concrete GameBehaviour MonoBehaviour that the Builders dictionary
		// instantiates for it. Used by doc generation (Editor/BehaviourDocs.cs) to locate the XML doc
		// comments authored on the MonoBehaviour (summary, property descriptions, trigger outputs).
		public readonly static IReadOnlyDictionary<Type, Type> MonoBehaviourByInfo =
			Builders.ToDictionary(kv => kv.Key, kv => kv.Value.MonoBehaviourType);

		public static (GameBehaviour, InitialiseBehaviourEvent) Create(
			GameObject gameObject,
			BehaviourInfo behaviourInfo,
			BehaviourBuildContext ctx)
		{
			if (!Builders.TryGetValue(behaviourInfo.GetType(), out var entry))
			{
				throw new ArgumentException($"Unsupported behaviour info type '{behaviourInfo.GetType()}'");
			}

			var (behaviour, initialise) = entry.Build(gameObject, behaviourInfo, ctx);

			if (behaviour is INeedsSpawner needsSpawner)
			{
				needsSpawner.Spawner = ctx.Spawner;
			}

			if (behaviour is INeedsGameClock needsClock)
			{
				needsClock.Clock = ctx.Clock;
			}

			return (behaviour, initialise);
		}

		private static Dictionary<Type, BuilderEntry> CreateBuilders()
		{
			var map = new Dictionary<Type, BuilderEntry>
			{
				[typeof(BoxColliderInfo)] = new(typeof(AutoAddBoxColliderBehaviour), (go, info, ctx) =>
				{
					var i = (BoxColliderInfo)info;
					var b = go.AddComponent<AutoAddBoxColliderBehaviour>();
					return (b, lr => b.Initialise(new BoxColliderData(i.Id,
						i.Size.Resolve(ctx.Resolution),
						i.IsTrigger.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(SphereColliderInfo)] = new(typeof(AutoAddSphereColliderBehaviour), (go, info, ctx) =>
				{
					var i = (SphereColliderInfo)info;
					var b = go.AddComponent<AutoAddSphereColliderBehaviour>();
					return (b, lr => b.Initialise(new SphereColliderData(i.Id,
						i.Radius.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(CapsuleColliderInfo)] = new(typeof(AutoAddCapsuleColliderBehaviour), (go, info, ctx) =>
				{
					var i = (CapsuleColliderInfo)info;
					var b = go.AddComponent<AutoAddCapsuleColliderBehaviour>();
					return (b, lr => b.Initialise(new CapsuleColliderData(i.Id)
					{
						Radius = i.Radius.Resolve(ctx.Resolution),
						Height = i.Height.Resolve(ctx.Resolution),
						Direction = i.Direction.Resolve(ctx.Resolution),
						IsTrigger = i.IsTrigger.Resolve(ctx.Resolution)
					}, i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(MeshColliderInfo)] = new(typeof(AutoAddMeshColliderBehaviour), (go, info, ctx) =>
				{
					var i = (MeshColliderInfo)info;
					var b = go.AddComponent<AutoAddMeshColliderBehaviour>();
					return (b, lr => b.Initialise(new MeshColliderData(i.Id)
					{
						Convex = i.Convex.Resolve(ctx.Resolution),
						IsTrigger = i.IsTrigger.Resolve(ctx.Resolution)
					}, i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(AddForceInfo)] = new(typeof(AddForceBehaviour), (go, info, ctx) =>
				{
					var i = (AddForceInfo)info;
					var b = go.AddComponent<AddForceBehaviour>();
					return (b, lr => b.Initialise(new AddForceData(i.Id,
						i.Force.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(AddImpulseInfo)] = new(typeof(AddImpulseBehaviour), (go, info, ctx) =>
				{
					var i = (AddImpulseInfo)info;
					var b = go.AddComponent<AddImpulseBehaviour>();
					return (b, lr => b.Initialise(new AddImpulseData(i.Id,
						i.Impulse.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(AddTorqueInfo)] = new(typeof(AddTorqueBehaviour), (go, info, ctx) =>
				{
					var i = (AddTorqueInfo)info;
					var b = go.AddComponent<AddTorqueBehaviour>();
					return (b, lr => b.Initialise(new AddTorqueData(i.Id,
						i.Torque.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(SetVelocityInfo)] = new(typeof(SetVelocityBehaviour), (go, info, ctx) =>
				{
					var i = (SetVelocityInfo)info;
					var b = go.AddComponent<SetVelocityBehaviour>();
					return (b, lr => b.Initialise(new SetVelocityData(i.Id,
						i.Velocity.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(SetAngularVelocityInfo)] = new(typeof(SetAngularVelocityBehaviour), (go, info, ctx) =>
				{
					var i = (SetAngularVelocityInfo)info;
					var b = go.AddComponent<SetAngularVelocityBehaviour>();
					return (b, lr => b.Initialise(new SetAngularVelocityData(i.Id,
						i.AngularVelocity.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(RigidbodyInfo)] = new(typeof(RigidbodyBehaviour), (go, info, ctx) =>
				{
					var i = (RigidbodyInfo)info;
					var b = go.AddComponent<RigidbodyBehaviour>();
					return (b, lr => b.Initialise(new RigidbodyData(i.Id)
					{
						UseGravity = i.UseGravity.Resolve(ctx.Resolution),
						IsKinematic = i.IsKinematic.Resolve(ctx.Resolution),
						Mass = i.Mass.Resolve(ctx.Resolution),
						LinearDamping = i.LinearDamping.Resolve(ctx.Resolution),
						AngularDamping = i.AngularDamping.Resolve(ctx.Resolution),
						FreezePosition = i.FreezePosition.Resolve(ctx.Resolution),
						FreezeRotation = i.FreezeRotation.Resolve(ctx.Resolution)
					}, i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(VelocityInfo)] = new(typeof(Velocity), (go, info, ctx) =>
				{
					var i = (VelocityInfo)info;
					var b = go.AddComponent<Velocity>();
					return (b, lr => b.Initialise(new VelocityData(i.Id,
						i.Velocity.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(AccelerationInfo)] = new(typeof(Acceleration), (go, info, ctx) =>
				{
					var i = (AccelerationInfo)info;
					var b = go.AddComponent<Acceleration>();
					return (b, lr => b.Initialise(new AccelerationData(i.Id,
						i.Acceleration.Resolve(ctx.Resolution),
						i.Velocity.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(DragInfo)] = new(typeof(DragBehaviour), (go, info, ctx) =>
				{
					var i = (DragInfo)info;
					var b = go.AddComponent<DragBehaviour>();
					return (b, lr => b.Initialise(new DragData(i.Id,
						i.Velocity.Resolve(ctx.Resolution),
						i.Coefficient.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(SpeedLimitInfo)] = new(typeof(SpeedLimit), (go, info, ctx) =>
				{
					var i = (SpeedLimitInfo)info;
					var b = go.AddComponent<SpeedLimit>();
					return (b, lr => b.Initialise(new SpeedLimitData(i.Id,
						i.Velocity.Resolve(ctx.Resolution),
						i.Max.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(MoveTowardsInfo)] = new(typeof(MoveTowards), (go, info, ctx) =>
				{
					var i = (MoveTowardsInfo)info;
					var b = go.AddComponent<MoveTowards>();
					return (b, lr => b.Initialise(new MoveTowardsData(i.Id,
						i.Target.Resolve(ctx.Resolution),
						i.Speed.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(SmoothMoveInfo)] = new(typeof(SmoothMove), (go, info, ctx) =>
				{
					var i = (SmoothMoveInfo)info;
					var b = go.AddComponent<SmoothMove>();
					return (b, lr => b.Initialise(new SmoothMoveData(i.Id,
						i.Target.Resolve(ctx.Resolution),
						i.SmoothTime.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(ClampPositionInfo)] = new(typeof(ClampPosition), (go, info, ctx) =>
				{
					var i = (ClampPositionInfo)info;
					var b = go.AddComponent<ClampPosition>();
					return (b, lr => b.Initialise(new ClampPositionData(i.Id,
						i.Min.Resolve(ctx.Resolution),
						i.Max.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(WrapPositionInfo)] = new(typeof(WrapPosition), (go, info, ctx) =>
				{
					var i = (WrapPositionInfo)info;
					var b = go.AddComponent<WrapPosition>();
					return (b, lr => b.Initialise(new WrapPositionData(i.Id,
						i.Min.Resolve(ctx.Resolution),
						i.Max.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(TranslateInfo)] = new(typeof(Translate), (go, info, ctx) =>
				{
					var i = (TranslateInfo)info;
					var b = go.AddComponent<Translate>();
					return (b, lr => b.Initialise(new TranslateData(i.Id,
						i.Displacement.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(AngularVelocityInfo)] = new(typeof(AngularVelocity), (go, info, ctx) =>
				{
					var i = (AngularVelocityInfo)info;
					var b = go.AddComponent<AngularVelocity>();
					return (b, lr => b.Initialise(new AngularVelocityData(i.Id,
						i.AngularVelocity.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(RotateInfo)] = new(typeof(Rotate), (go, info, ctx) =>
				{
					var i = (RotateInfo)info;
					var b = go.AddComponent<Rotate>();
					return (b, lr => b.Initialise(new RotateData(i.Id,
						i.Displacement.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(SetRotationInfo)] = new(typeof(SetRotation), (go, info, ctx) =>
				{
					var i = (SetRotationInfo)info;
					var b = go.AddComponent<SetRotation>();
					return (b, lr => b.Initialise(new SetRotationData(i.Id,
						i.ValueExpression.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(MoveAnimationInfo)] = new(typeof(MoveAnimation), (go, info, ctx) =>
					BuildTransformAnimation<MoveAnimationInfo, MoveAnimation>(go, (MoveAnimationInfo)info, ctx,
						i => i.Start, i => i.End, i => i.Duration, i => i.Easing)),
				[typeof(ScaleAnimationInfo)] = new(typeof(ScaleAnimation), (go, info, ctx) =>
					BuildTransformAnimation<ScaleAnimationInfo, ScaleAnimation>(go, (ScaleAnimationInfo)info, ctx,
						i => i.Start, i => i.End, i => i.Duration, i => i.Easing)),
				[typeof(RotateAnimationInfo)] = new(typeof(RotateAnimation), (go, info, ctx) =>
					BuildTransformAnimation<RotateAnimationInfo, RotateAnimation>(go, (RotateAnimationInfo)info, ctx,
						i => i.Start, i => i.End, i => i.Duration, i => i.Easing)),
				[typeof(SetPositionInfo)] = new(typeof(SetPosition), (go, info, ctx) =>
				{
					var i = (SetPositionInfo)info;
					var b = go.AddComponent<SetPosition>();
					return (b, lr => b.Initialise(new SetPositionData(i.Id,
						i.ValueExpression.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(KeyHoldTriggerInfo)] = new(typeof(KeyHoldTrigger), (go, info, ctx) =>
				{
					var i = (KeyHoldTriggerInfo)info;
					var b = go.AddComponent<KeyHoldTrigger>();
					return (b, lr => b.Initialise(new KeyHoldTriggerData(i.Id,
						i.Key.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(KeyDownTriggerInfo)] = new(typeof(KeyDownTrigger), (go, info, ctx) =>
				{
					var i = (KeyDownTriggerInfo)info;
					var b = go.AddComponent<KeyDownTrigger>();
					return (b, lr => b.Initialise(new KeyDownTriggerData(i.Id,
						i.Key.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(KeyUpTriggerInfo)] = new(typeof(KeyUpTrigger), (go, info, ctx) =>
				{
					var i = (KeyUpTriggerInfo)info;
					var b = go.AddComponent<KeyUpTrigger>();
					return (b, lr => b.Initialise(new KeyUpTriggerData(i.Id,
						i.Key.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(MouseButtonTriggerInfo)] = new(typeof(MouseButtonTrigger), (go, info, ctx) =>
				{
					var i = (MouseButtonTriggerInfo)info;
					var b = go.AddComponent<MouseButtonTrigger>();
					return (b, lr => b.Initialise(new MouseButtonTriggerData(i.Id,
						i.Button.Resolve(ctx.Resolution),
						i.Phase.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(MousePositionTriggerInfo)] = new(typeof(MousePositionTrigger), (go, info, ctx) =>
				{
					var i = (MousePositionTriggerInfo)info;
					var b = go.AddComponent<MousePositionTrigger>();
					return (b, lr => b.Initialise(new MousePositionTriggerData(i.Id),
						i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(ScrollWheelTriggerInfo)] = new(typeof(ScrollWheelTrigger), (go, info, ctx) =>
				{
					var i = (ScrollWheelTriggerInfo)info;
					var b = go.AddComponent<ScrollWheelTrigger>();
					return (b, lr => b.Initialise(new ScrollWheelTriggerData(i.Id),
						i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(AxisTriggerInfo)] = new(typeof(AxisTrigger), (go, info, ctx) =>
				{
					var i = (AxisTriggerInfo)info;
					var b = go.AddComponent<AxisTrigger>();
					return (b, lr => b.Initialise(new AxisTriggerData(i.Id,
						i.XAxis.Resolve(ctx.Resolution),
						i.YAxis.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(InputActionTriggerInfo)] = new(typeof(InputActionTrigger), (go, info, ctx) =>
				{
					var i = (InputActionTriggerInfo)info;
					var b = go.AddComponent<InputActionTrigger>();

					var actionName = i.Action.Resolve(ctx.Resolution).Get();

					if (!ctx.Controls.Actions.TryGetValue(actionName, out var actionInfo))
					{
						throw new ArgumentException(
							$"Input action '{actionName}' referenced by behaviour '{i.Id}' is not declared in Controls.");
					}

					var liveAction = ctx.ControlsAsset.FindAction(actionName)
						?? throw new ArgumentException(
							$"No InputAction '{actionName}' was built for behaviour '{i.Id}'.");

					return (b, lr => b.Initialise(new InputActionTriggerData(i.Id,
						actionName,
						actionInfo.Kind,
						actionInfo.Phase,
						liveAction), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(GamepadButtonTriggerInfo)] = new(typeof(GamepadButtonTrigger), (go, info, ctx) =>
				{
					var i = (GamepadButtonTriggerInfo)info;
					var b = go.AddComponent<GamepadButtonTrigger>();
					return (b, lr => b.Initialise(new GamepadButtonTriggerData(i.Id,
						i.Button.Resolve(ctx.Resolution),
						i.Mode.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(TapTriggerInfo)] = new(typeof(Tap), (go, info, ctx) =>
				{
					var i = (TapTriggerInfo)info;
					var b = go.AddComponent<Tap>();
					return (b, lr => b.Initialise(new TapTriggerData(i.Id,
						i.MaxDuration.Resolve(ctx.Resolution),
						i.MaxMovement.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(DoubleTapTriggerInfo)] = new(typeof(DoubleTap), (go, info, ctx) =>
				{
					var i = (DoubleTapTriggerInfo)info;
					var b = go.AddComponent<DoubleTap>();
					return (b, lr => b.Initialise(new DoubleTapTriggerData(i.Id,
						i.MaxInterval.Resolve(ctx.Resolution),
						i.MaxMovement.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(LongPressTriggerInfo)] = new(typeof(LongPress), (go, info, ctx) =>
				{
					var i = (LongPressTriggerInfo)info;
					var b = go.AddComponent<LongPress>();
					return (b, lr => b.Initialise(new LongPressTriggerData(i.Id,
						i.Duration.Resolve(ctx.Resolution),
						i.MaxMovement.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(SwipeTriggerInfo)] = new(typeof(Swipe), (go, info, ctx) =>
				{
					var i = (SwipeTriggerInfo)info;
					var b = go.AddComponent<Swipe>();
					return (b, lr => b.Initialise(new SwipeTriggerData(i.Id,
						i.MinDistance.Resolve(ctx.Resolution),
						i.MaxDuration.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(DragTriggerInfo)] = new(typeof(Drag), (go, info, ctx) =>
				{
					var i = (DragTriggerInfo)info;
					var b = go.AddComponent<Drag>();
					return (b, lr => b.Initialise(new DragTriggerData(i.Id,
						i.Threshold.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(PinchAndRotateTriggerInfo)] = new(typeof(PinchAndRotate), (go, info, ctx) =>
				{
					var i = (PinchAndRotateTriggerInfo)info;
					var b = go.AddComponent<PinchAndRotate>();
					return (b, lr => b.Initialise(new PinchAndRotateTriggerData(i.Id), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(OnStartTriggerInfo)] = new(typeof(OnStartTrigger), (go, info, ctx) =>
				{
					var i = (OnStartTriggerInfo)info;
					var b = go.AddComponent<OnStartTrigger>();
					return (b, lr => b.Initialise(new OnStartTriggerData(i.Id), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(TimerTriggerInfo)] = new(typeof(TimerTrigger), (go, info, ctx) =>
				{
					var i = (TimerTriggerInfo)info;
					var b = go.AddComponent<TimerTrigger>();
					return (b, lr => b.Initialise(new TimerTriggerData(i.Id,
						i.Delay.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(DeferredTriggerInfo)] = new(typeof(DeferredTrigger), (go, info, ctx) =>
				{
					var i = (DeferredTriggerInfo)info;
					var b = go.AddComponent<DeferredTrigger>();
					return (b, lr => b.Initialise(new DeferredTriggerData(i.Id,
						i.Delay.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(DebouncedTriggerInfo)] = new(typeof(DebouncedTrigger), (go, info, ctx) =>
				{
					var i = (DebouncedTriggerInfo)info;
					var b = go.AddComponent<DebouncedTrigger>();
					return (b, lr => b.Initialise(new DebouncedTriggerData(i.Id,
						i.Interval.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(ThrottledTriggerInfo)] = new(typeof(ThrottledTrigger), (go, info, ctx) =>
				{
					var i = (ThrottledTriggerInfo)info;
					var b = go.AddComponent<ThrottledTrigger>();
					return (b, lr => b.Initialise(new ThrottledTriggerData(i.Id,
						i.Rate.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(IntervalTriggerInfo)] = new(typeof(IntervalTrigger), (go, info, ctx) =>
				{
					var i = (IntervalTriggerInfo)info;
					var b = go.AddComponent<IntervalTrigger>();
					return (b, lr => b.Initialise(new IntervalTriggerData(i.Id,
						i.Interval.Resolve(ctx.Resolution),
						i.Count.Resolve(ctx.Resolution),
						i.AutoStart.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(EveryFrameTriggerInfo)] = new(typeof(EveryFrameTrigger), (go, info, ctx) =>
				{
					var i = (EveryFrameTriggerInfo)info;
					var b = go.AddComponent<EveryFrameTrigger>();
					return (b, lr => b.Initialise(new EveryFrameTriggerData(i.Id), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(CollisionEnterTriggerInfo)] = new(typeof(CollisionEnter), (go, info, ctx) =>
				{
					var i = (CollisionEnterTriggerInfo)info;
					var b = go.AddComponent<CollisionEnter>();
					return (b, lr => b.Initialise(new CollisionEnterTriggerData(i.Id,
						i.TagsToDetect), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(CollisionExitTriggerInfo)] = new(typeof(CollisionExit), (go, info, ctx) =>
				{
					var i = (CollisionExitTriggerInfo)info;
					var b = go.AddComponent<CollisionExit>();
					return (b, lr => b.Initialise(new CollisionExitTriggerData(i.Id,
						i.TagsToDetect), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(CollisionStayTriggerInfo)] = new(typeof(CollisionStay), (go, info, ctx) =>
				{
					var i = (CollisionStayTriggerInfo)info;
					var b = go.AddComponent<CollisionStay>();
					return (b, lr => b.Initialise(new CollisionStayTriggerData(i.Id,
						i.TagsToDetect), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(TriggerEnterTriggerInfo)] = new(typeof(TriggerEnter), (go, info, ctx) =>
				{
					var i = (TriggerEnterTriggerInfo)info;
					var b = go.AddComponent<TriggerEnter>();
					return (b, lr => b.Initialise(new TriggerEnterTriggerData(i.Id,
						i.TagsToDetect), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(TriggerExitTriggerInfo)] = new(typeof(TriggerExit), (go, info, ctx) =>
				{
					var i = (TriggerExitTriggerInfo)info;
					var b = go.AddComponent<TriggerExit>();
					return (b, lr => b.Initialise(new TriggerExitTriggerData(i.Id,
						i.TagsToDetect), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(ConditionGateInfo)] = new(typeof(ConditionGate), (go, info, ctx) =>
				{
					var i = (ConditionGateInfo)info;
					var b = go.AddComponent<ConditionGate>();
					return (b, lr => b.Initialise(new ConditionGateData(i.Id,
						i.Condition.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(InverseConditionGateInfo)] = new(typeof(InverseConditionGate), (go, info, ctx) =>
				{
					var i = (InverseConditionGateInfo)info;
					var b = go.AddComponent<InverseConditionGate>();
					return (b, lr => b.Initialise(new ConditionGateData(i.Id,
						i.Condition.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(ExclusiveTriggerInfo)] = new(typeof(ExclusiveTrigger), (go, info, ctx) =>
				{
					var i = (ExclusiveTriggerInfo)info;
					var b = go.AddComponent<ExclusiveTrigger>();
					b.Registry = ctx.ExclusiveGroups;
					return (b, lr => b.Initialise(new ExclusiveTriggerData(i.Id,
						i.Group.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(CameraInfo)] = new(typeof(CameraBehaviour), (go, info, ctx) =>
				{
					var i = (CameraInfo)info;
					var b = go.AddComponent<CameraBehaviour>();
					return (b, lr => b.Initialise(new CameraData(i.Id,
						i.View.Resolve(ctx.Resolution),
						i.Size.Resolve(ctx.Resolution),
						i.DefaultBlend.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(CameraFollowInfo)] = new(typeof(CameraFollow), (go, info, ctx) =>
				{
					var i = (CameraFollowInfo)info;
					var b = go.AddComponent<CameraFollow>();
					return (b, lr =>
					{
						var res = ctx.Resolution;

						// Closure over the live registry: an entity tag -> the distinct transforms of matching
						// entities, re-queried on every read so tag targets catch entities spawned after build.
						IReadOnlyList<Transform> ResolveByEntityTag(string tag) =>
							lr.GetByEntityTag(tag).Select(x => x.transform).Distinct().ToArray();

						b.Initialise(new CameraFollowData(i.Id,
							CameraTargetResolver.Resolve(i.Target, res, ResolveByEntityTag),
							CameraTargetResolver.Resolve(i.LookAt, res, ResolveByEntityTag),
							i.Mode.Resolve(res),
							i.Priority.Resolve(res),
							i.Lens.Resolve(res),
							i.Damping.Resolve(res),
							i.DeadZone.Resolve(res),
							i.CameraDistance.Resolve(res),
							i.ScreenOffset.Resolve(res),
							i.FollowOffset.Resolve(res)), i.Listeners.ToListeners(lr, res));
					});
				}),
				[typeof(SpawnerInfo)] = new(typeof(SpawnerBehaviour), (go, info, ctx) =>
				{
					var i = (SpawnerInfo)info;
					var b = go.AddComponent<SpawnerBehaviour>();
					return (b, lr => b.Initialise(new SpawnerData(i.Id,
						i.TemplateId.Resolve(ctx.Resolution),
						i.Position.Resolve(ctx.Resolution),
						i.Rotation.Resolve(ctx.Resolution),
						i.Parameters.ToDictionary(kv => kv.Key,
							kv => (IValueProvider)kv.Value.Resolve(ctx.Resolution))), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(DestroyInfo)] = new(typeof(DestroyBehaviour), (go, info, ctx) =>
				{
					var i = (DestroyInfo)info;
					var b = go.AddComponent<DestroyBehaviour>();
					return (b, lr => b.Initialise(new DestroyData(i.Id), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(EndGameInfo)] = new(typeof(EndGame), (go, info, ctx) =>
				{
					var i = (EndGameInfo)info;
					var b = go.AddComponent<EndGame>();
					return (b, lr => b.Initialise(new EndGameData(i.Id), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(SpriteInfo)] = new(typeof(SpriteBehaviour), (go, info, ctx) =>
				{
					var i = (SpriteInfo)info;
					var b = go.AddComponent<SpriteBehaviour>();
					return (b, lr => b.Initialise(new SpriteData(i.Id,
						i.Sprite.Resolve(ctx.Resolution),
						i.Size.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(VoxelMeshInfo)] = new(typeof(VoxelMesh), (go, info, ctx) =>
				{
					var i = (VoxelMeshInfo)info;
					var b = go.AddComponent<VoxelMesh>();
					return (b, lr => b.Initialise(new VoxelMeshData(i.Id,
						i.Mesh.Resolve(ctx.Resolution),
						i.Scale.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(PrimitiveInfo)] = new(typeof(Primitive), (go, info, ctx) =>
				{
					var i = (PrimitiveInfo)info;
					var b = go.AddComponent<Primitive>();
					return (b, lr => b.Initialise(new PrimitiveData(i.Id,
						i.Shape.Resolve(ctx.Resolution),
						i.Colour.Resolve(ctx.Resolution),
						i.Size.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(ActivePollInfo)] = new(typeof(ActivePoll), (go, info, ctx) =>
				{
					var i = (ActivePollInfo)info;
					var b = go.AddComponent<ActivePoll>();
					return (b, lr => b.Initialise(new ActivePollData(i.Id,
						i.Active.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(SetActiveInfo)] = new(typeof(SetActive), (go, info, ctx) =>
				{
					var i = (SetActiveInfo)info;
					var b = go.AddComponent<SetActive>();
					return (b, lr => b.Initialise(new SetActiveData(i.Id,
						i.Active.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(SetTimeScaleInfo)] = new(typeof(SetTimeScale), (go, info, ctx) =>
				{
					var i = (SetTimeScaleInfo)info;
					var b = go.AddComponent<SetTimeScale>();
					return (b, lr => b.Initialise(new SetTimeScaleData(i.Id,
						i.Scale.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(ToggleActiveInfo)] = new(typeof(ToggleActive), (go, info, ctx) =>
				{
					var i = (ToggleActiveInfo)info;
					var b = go.AddComponent<ToggleActive>();
					return (b, lr => b.Initialise(new ToggleActiveData(i.Id),
						i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(AudioSourceInfo)] = new(typeof(AudioSourceBehaviour), (go, info, ctx) =>
				{
					var i = (AudioSourceInfo)info;
					var b = go.AddComponent<AudioSourceBehaviour>();
					return (b, lr => b.Initialise(new AudioSourceData(i.Id,
						i.Clip.Resolve(ctx.Resolution),
						i.PlayOnStart.Resolve(ctx.Resolution),
						i.Loop.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(SphereGizmoInfo)] = new(typeof(SphereGizmoBehaviour), (go, info, ctx) =>
				{
					var i = (SphereGizmoInfo)info;
					var b = go.AddComponent<SphereGizmoBehaviour>();
					return (b, lr => b.Initialise(new SphereGizmoData(i.Id,
						i.Radius.Resolve(ctx.Resolution),
						i.IsWire.Resolve(ctx.Resolution),
						i.Colour.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(CubeGizmoInfo)] = new(typeof(CubeGizmoBehaviour), (go, info, ctx) =>
				{
					var i = (CubeGizmoInfo)info;
					var b = go.AddComponent<CubeGizmoBehaviour>();
					return (b, lr => b.Initialise(new CubeGizmoData(i.Id,
						i.Size.Resolve(ctx.Resolution),
						i.IsWire.Resolve(ctx.Resolution),
						i.Colour.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(LineGizmoInfo)] = new(typeof(LineGizmoBehaviour), (go, info, ctx) =>
				{
					var i = (LineGizmoInfo)info;
					var b = go.AddComponent<LineGizmoBehaviour>();
					return (b, lr => b.Initialise(new LineGizmoData(i.Id,
						i.Start.Resolve(ctx.Resolution),
						i.End.Resolve(ctx.Resolution),
						i.Colour.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(UICanvasInfo)] = new(typeof(UICanvas), (go, info, ctx) =>
				{
					var i = (UICanvasInfo)info;
					var b = go.AddComponent<UICanvas>();
					return (b, lr => b.Initialise(new UICanvasData(i.Id,
						i.MatchWidthOrHeight.Resolve(ctx.Resolution),
						i.ReferenceResolution.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(UIContainerInfo)] = new(typeof(UIContainer), (go, info, ctx) =>
				{
					var i = (UIContainerInfo)info;
					var b = go.AddComponent<UIContainer>();
					return (b, lr => b.Initialise(new UIContainerData(i.Id,
						i.Direction.Resolve(ctx.Resolution),
						i.Spacing.Resolve(ctx.Resolution),
						i.Padding.Resolve(ctx.Resolution),
						i.ChildAlignment.Resolve(ctx.Resolution),
						i.FitContent.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(TextLabelInfo)] = new(typeof(TextLabel), (go, info, ctx) =>
				{
					var i = (TextLabelInfo)info;
					var b = go.AddComponent<TextLabel>();
					var prefab = RequireUiPrefab(ctx, lib => lib.LabelPrefab, "text label");
					return (b, lr => b.Initialise(new TextLabelData(i.Id,
						i.Text.Resolve(ctx.Resolution),
						i.FontSize.Resolve(ctx.Resolution),
						i.PreferredWidth.Resolve(ctx.Resolution),
						i.PreferredHeight.Resolve(ctx.Resolution),
						prefab), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(UIButtonInfo)] = new(typeof(UIButton), (go, info, ctx) =>
				{
					var i = (UIButtonInfo)info;
					var b = go.AddComponent<UIButton>();
					var prefab = RequireUiPrefab(ctx, lib => lib.ButtonPrefab, "ui button");
					return (b, lr => b.Initialise(new UIButtonData(i.Id,
						i.Label.Resolve(ctx.Resolution),
						i.PreferredWidth.Resolve(ctx.Resolution),
						i.PreferredHeight.Resolve(ctx.Resolution),
						prefab), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(UISliderInfo)] = new(typeof(UISlider), (go, info, ctx) =>
				{
					var i = (UISliderInfo)info;
					var b = go.AddComponent<UISlider>();
					var prefab = RequireUiPrefab(ctx, lib => lib.SliderPrefab, "ui slider");
					return (b, lr => b.Initialise(new UISliderData(i.Id,
						i.InitialValue.Resolve(ctx.Resolution),
						i.MinValue.Resolve(ctx.Resolution),
						i.MaxValue.Resolve(ctx.Resolution),
						i.PreferredWidth.Resolve(ctx.Resolution),
						i.PreferredHeight.Resolve(ctx.Resolution),
						prefab), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(StateMachineInfo)] = new(typeof(StateMachine), (go, info, ctx) =>
				{
					var i = (StateMachineInfo)info;
					var b = go.AddComponent<StateMachine>();

					// Declare the state variable up-front (Create phase) so other behaviours referencing it
					// via !var resolve regardless of initialisation order. Seeded to the initial state;
					// respected if the entity already declares it (e.g. restored from a save).
					var scope = ctx.Resolution.Scope;
					if (!scope.TryGet(i.StateVariable, out _))
					{
						scope.Create(new ValueInfo(i.StateVariable, new StringValue(i.Initial)));
					}

					return (b, lr =>
					{
						var res = ctx.Resolution;
						var current = res.Variables.Get<string>(i.StateVariable, scope);
						var transitions = i.Transitions
							.Select(t => new StateTransition(t.From, t.To, t.When.Resolve(res)))
							.ToArray();
						var states = i.States.ToDictionary(s => s.Name,
							s => new StateMachineState(s.Name,
								s.OnEnter.ToListeners(lr, res),
								s.OnExit.ToListeners(lr, res)));
						b.Initialise(new StateMachineData(i.Id, current, i.Initial, transitions, states),
							i.Listeners.ToListeners(lr, res));
					}
					);
				})
			};

			RegisterVariableSetter<Vector3, Vector3Setter>(map);
			RegisterVariableSetter<int, IntSetter>(map);
			RegisterVariableSetter<float, FloatSetter>(map);
			RegisterVariableSetter<bool, BoolSetter>(map);
			RegisterVariableSetter<string, StringSetter>(map);
			RegisterVariableSetter<Color, ColourSetter>(map);

			RegisterVariableChangedTrigger<int, IntVariableChangedTrigger>(map);
			RegisterVariableChangedTrigger<float, FloatVariableChangedTrigger>(map);
			RegisterVariableChangedTrigger<bool, BoolVariableChangedTrigger>(map);
			RegisterVariableChangedTrigger<string, StringVariableChangedTrigger>(map);
			RegisterVariableChangedTrigger<Vector3, Vector3VariableChangedTrigger>(map);
			RegisterVariableChangedTrigger<Color, ColourVariableChangedTrigger>(map);

			RegisterListOps<Vector3, Vector3ListAdd, Vector3ListInsert, Vector3ListRemoveAt, Vector3ListRemove, Vector3ListSetAt, Vector3ListSet, Vector3ListAddRange, Vector3ListClear, Vector3ListLoopTrigger>(map);
			RegisterListOps<int, IntListAdd, IntListInsert, IntListRemoveAt, IntListRemove, IntListSetAt, IntListSet, IntListAddRange, IntListClear, IntListLoopTrigger>(map);
			RegisterListOps<float, FloatListAdd, FloatListInsert, FloatListRemoveAt, FloatListRemove, FloatListSetAt, FloatListSet, FloatListAddRange, FloatListClear, FloatListLoopTrigger>(map);
			RegisterListOps<bool, BoolListAdd, BoolListInsert, BoolListRemoveAt, BoolListRemove, BoolListSetAt, BoolListSet, BoolListAddRange, BoolListClear, BoolListLoopTrigger>(map);
			RegisterListOps<string, StringListAdd, StringListInsert, StringListRemoveAt, StringListRemove, StringListSetAt, StringListSet, StringListAddRange, StringListClear, StringListLoopTrigger>(map);
			RegisterListOps<Color, ColourListAdd, ColourListInsert, ColourListRemoveAt, ColourListRemove, ColourListSetAt, ColourListSet, ColourListAddRange, ColourListClear, ColourListLoopTrigger>(map);

			return map;
		}

		private static (GameBehaviour, InitialiseBehaviourEvent) BuildTransformAnimation<TInfo, TBehaviour>(
			GameObject go,
			TInfo info,
			BehaviourBuildContext ctx,
			Func<TInfo, ValueSource<Vector3>> start,
			Func<TInfo, ValueSource<Vector3>> end,
			Func<TInfo, ValueSource<float>> duration,
			Func<TInfo, ValueSource<string>> easing)
			where TInfo : BehaviourInfo
			where TBehaviour : GameBehaviour<TransformAnimationData>
		{
			var b = go.AddComponent<TBehaviour>();
			return (b, lr => b.Initialise(new TransformAnimationData(info.Id,
				start(info).Resolve(ctx.Resolution),
				end(info).Resolve(ctx.Resolution),
				duration(info).Resolve(ctx.Resolution),
				easing(info).Resolve(ctx.Resolution)), info.Listeners.ToListeners(lr, ctx.Resolution)));
		}

		private static void RegisterVariableSetter<T, TBehaviour>(IDictionary<Type, BuilderEntry> map)
			where TBehaviour : GameBehaviour<VariableSetterData<T>>
		{
			map[typeof(VariableSetterInfo<T>)] = new(typeof(TBehaviour), (go, info, ctx) =>
			{
				var i = (VariableSetterInfo<T>)info;
				var b = go.AddComponent<TBehaviour>();
				return (b, lr => b.Initialise(new VariableSetterData<T>(i.Id,
					i.ValueToSet.Resolve(ctx.Resolution),
					i.ValueToGet.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
			});
		}

		private static void RegisterVariableChangedTrigger<T, TBehaviour>(IDictionary<Type, BuilderEntry> map)
			where TBehaviour : GameBehaviour<VariableChangedTriggerData<T>>
		{
			map[typeof(VariableChangedTriggerInfo<T>)] = new(typeof(TBehaviour), (go, info, ctx) =>
			{
				var i = (VariableChangedTriggerInfo<T>)info;
				var b = go.AddComponent<TBehaviour>();
				return (b, lr => b.Initialise(new VariableChangedTriggerData<T>(i.Id,
					i.Variable.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
			});
		}

		private static void RegisterListOps<T, TAdd, TInsert, TRemoveAt, TRemove, TSetAt, TSet, TAddRange, TClear, TLoop>(IDictionary<Type, BuilderEntry> map)
			where TAdd : GameBehaviour<ListAddData<T>>
			where TInsert : GameBehaviour<ListInsertData<T>>
			where TRemoveAt : GameBehaviour<ListRemoveAtData<T>>
			where TRemove : GameBehaviour<ListRemoveData<T>>
			where TSetAt : GameBehaviour<ListSetAtData<T>>
			where TSet : GameBehaviour<ListSetData<T>>
			where TAddRange : GameBehaviour<ListAddRangeData<T>>
			where TClear : GameBehaviour<ListClearData<T>>
			where TLoop : GameBehaviour<ListLoopTriggerData<T>>
		{
			map[typeof(ListLoopTriggerInfo<T>)] = new(typeof(TLoop), (go, info, ctx) =>
			{
				var i = (ListLoopTriggerInfo<T>)info;
				var b = go.AddComponent<TLoop>();
				return (b, lr => b.Initialise(new ListLoopTriggerData<T>(i.Id,
					i.List.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
			});
			map[typeof(ListAddInfo<T>)] = new(typeof(TAdd), (go, info, ctx) =>
			{
				var i = (ListAddInfo<T>)info;
				var b = go.AddComponent<TAdd>();
				return (b, lr => b.Initialise(new ListAddData<T>(i.Id,
					i.List.Resolve(ctx.Resolution),
					i.Value.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
			});
			map[typeof(ListInsertInfo<T>)] = new(typeof(TInsert), (go, info, ctx) =>
			{
				var i = (ListInsertInfo<T>)info;
				var b = go.AddComponent<TInsert>();
				return (b, lr => b.Initialise(new ListInsertData<T>(i.Id,
					i.List.Resolve(ctx.Resolution),
					i.Index.Resolve(ctx.Resolution),
					i.Value.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
			});
			map[typeof(ListRemoveAtInfo<T>)] = new(typeof(TRemoveAt), (go, info, ctx) =>
			{
				var i = (ListRemoveAtInfo<T>)info;
				var b = go.AddComponent<TRemoveAt>();
				return (b, lr => b.Initialise(new ListRemoveAtData<T>(i.Id,
					i.List.Resolve(ctx.Resolution),
					i.Index.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
			});
			map[typeof(ListRemoveInfo<T>)] = new(typeof(TRemove), (go, info, ctx) =>
			{
				var i = (ListRemoveInfo<T>)info;
				var b = go.AddComponent<TRemove>();
				return (b, lr => b.Initialise(new ListRemoveData<T>(i.Id,
					i.List.Resolve(ctx.Resolution),
					i.Value.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
			});
			map[typeof(ListSetAtInfo<T>)] = new(typeof(TSetAt), (go, info, ctx) =>
			{
				var i = (ListSetAtInfo<T>)info;
				var b = go.AddComponent<TSetAt>();
				return (b, lr => b.Initialise(new ListSetAtData<T>(i.Id,
					i.List.Resolve(ctx.Resolution),
					i.Index.Resolve(ctx.Resolution),
					i.Value.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
			});
			map[typeof(ListSetInfo<T>)] = new(typeof(TSet), (go, info, ctx) =>
			{
				var i = (ListSetInfo<T>)info;
				var b = go.AddComponent<TSet>();
				return (b, lr => b.Initialise(new ListSetData<T>(i.Id,
					i.List.Resolve(ctx.Resolution),
					i.Value.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
			});
			map[typeof(ListAddRangeInfo<T>)] = new(typeof(TAddRange), (go, info, ctx) =>
			{
				var i = (ListAddRangeInfo<T>)info;
				var b = go.AddComponent<TAddRange>();
				return (b, lr => b.Initialise(new ListAddRangeData<T>(i.Id,
					i.List.Resolve(ctx.Resolution),
					i.Other.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
			});
			map[typeof(ListClearInfo<T>)] = new(typeof(TClear), (go, info, ctx) =>
			{
				var i = (ListClearInfo<T>)info;
				var b = go.AddComponent<TClear>();
				return (b, lr => b.Initialise(new ListClearData<T>(i.Id,
					i.List.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
			});
		}

		private static GameObject RequireUiPrefab(
			BehaviourBuildContext ctx,
			Func<UiPrefabLibrary, GameObject> select,
			string blockName)
		{
			var prefab = select(ctx.UiPrefabs);

			if (prefab == null)
			{
				throw new InvalidOperationException(
					$"The '{blockName}' UI block's prefab is not assigned in the UiPrefabLibrary. " +
					"Run 'Assembler > UI > Generate UI Prefabs' or assign it on the asset.");
			}

			return prefab;
		}

		private static IReadOnlyList<Listener> ToListeners(this IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyBehaviourRegistry listenerRegistry,
			ResolutionContext ctx) =>
			listeners.Select(l => (Listener)(l switch
			{
				DirectListenerInfo direct => new DirectListener(
					listenerRegistry[direct.BehaviourDescriptor],
					direct.OutputMapping),
				EntityTaggedListenerInfo entityTagged => new EntityTaggedListener(
					entityTagged.EntityTag.Resolve(ctx),
					entityTagged.BehaviourId,
					listenerRegistry.GetByEntityTagAndBehaviourId,
					entityTagged.OutputMapping),
				BehaviourTaggedListenerInfo behaviourTagged => new BehaviourTaggedListener(
					behaviourTagged.BehaviourTag.Resolve(ctx),
					tag => listenerRegistry.GetByBehaviourTag(tag),
					behaviourTagged.OutputMapping),
				GameOverListenerInfo gameOver => new DirectListener(
					listenerRegistry[new BehaviourDescriptor(
						GameOverController.EntityId, GameOverController.EndBehaviourId)],
					gameOver.OutputMapping),
				_ => throw new ArgumentException($"Unsupported listener type '{l.GetType()}'")
			})).ToArray();
	}
}
