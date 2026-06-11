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
using Assembler.Core;
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

			if (behaviour is INeedsEntityQuery needsQuery)
			{
				needsQuery.Query = ctx.EntityQuery;
			}

			if (behaviour is INeedsLineOfSight needsSight)
			{
				needsSight.Sight = ctx.Sight;
			}

			if (behaviour is INeedsNavigation needsNav)
			{
				needsNav.Nav = ctx.Nav;
			}

			return (behaviour, initialise);
		}

		private static Dictionary<Type, BuilderEntry> CreateBuilders()
		{
			var map = new Dictionary<Type, BuilderEntry>
			{
				[typeof(BoxColliderInfo)] = Entry<BoxColliderInfo, AutoAddBoxColliderBehaviour, BoxColliderData>(
					(i, ctx) => new BoxColliderData(i.Id)
					{
						Size = i.Size.Resolve(ctx.Resolution),
						IsTrigger = i.IsTrigger.Resolve(ctx.Resolution),
						Bounciness = i.Bounciness.Resolve(ctx.Resolution),
						DynamicFriction = i.DynamicFriction.Resolve(ctx.Resolution),
						StaticFriction = i.StaticFriction.Resolve(ctx.Resolution)
					}),
				[typeof(SphereColliderInfo)] = Entry<SphereColliderInfo, AutoAddSphereColliderBehaviour, SphereColliderData>(
					(i, ctx) => new SphereColliderData(i.Id)
					{
						Radius = i.Radius.Resolve(ctx.Resolution),
						IsTrigger = i.IsTrigger.Resolve(ctx.Resolution),
						Bounciness = i.Bounciness.Resolve(ctx.Resolution),
						DynamicFriction = i.DynamicFriction.Resolve(ctx.Resolution),
						StaticFriction = i.StaticFriction.Resolve(ctx.Resolution)
					}),
				[typeof(CapsuleColliderInfo)] = Entry<CapsuleColliderInfo, AutoAddCapsuleColliderBehaviour, CapsuleColliderData>(
					(i, ctx) => new CapsuleColliderData(i.Id)
					{
						Radius = i.Radius.Resolve(ctx.Resolution),
						Height = i.Height.Resolve(ctx.Resolution),
						Direction = i.Direction.Resolve(ctx.Resolution),
						IsTrigger = i.IsTrigger.Resolve(ctx.Resolution),
						Bounciness = i.Bounciness.Resolve(ctx.Resolution),
						DynamicFriction = i.DynamicFriction.Resolve(ctx.Resolution),
						StaticFriction = i.StaticFriction.Resolve(ctx.Resolution)
					}),
				[typeof(MeshColliderInfo)] = Entry<MeshColliderInfo, AutoAddMeshColliderBehaviour, MeshColliderData>(
					(i, ctx) => new MeshColliderData(i.Id)
					{
						Convex = i.Convex.Resolve(ctx.Resolution),
						IsTrigger = i.IsTrigger.Resolve(ctx.Resolution),
						Bounciness = i.Bounciness.Resolve(ctx.Resolution),
						DynamicFriction = i.DynamicFriction.Resolve(ctx.Resolution),
						StaticFriction = i.StaticFriction.Resolve(ctx.Resolution)
					}),
				[typeof(AddForceInfo)] = Entry<AddForceInfo, AddForceBehaviour, AddForceData>(
					(i, ctx) => new AddForceData(i.Id,
						i.Force.Resolve(ctx.Resolution))),
				[typeof(AddImpulseInfo)] = Entry<AddImpulseInfo, AddImpulseBehaviour, AddImpulseData>(
					(i, ctx) => new AddImpulseData(i.Id,
						i.Impulse.Resolve(ctx.Resolution))),
				[typeof(AddTorqueInfo)] = Entry<AddTorqueInfo, AddTorqueBehaviour, AddTorqueData>(
					(i, ctx) => new AddTorqueData(i.Id,
						i.Torque.Resolve(ctx.Resolution))),
				[typeof(SetVelocityInfo)] = Entry<SetVelocityInfo, SetVelocityBehaviour, SetVelocityData>(
					(i, ctx) => new SetVelocityData(i.Id,
						i.Velocity.Resolve(ctx.Resolution))),
				[typeof(SetAngularVelocityInfo)] = Entry<SetAngularVelocityInfo, SetAngularVelocityBehaviour, SetAngularVelocityData>(
					(i, ctx) => new SetAngularVelocityData(i.Id,
						i.AngularVelocity.Resolve(ctx.Resolution))),
				[typeof(RigidbodyInfo)] = Entry<RigidbodyInfo, RigidbodyBehaviour, RigidbodyData>(
					(i, ctx) => new RigidbodyData(i.Id)
					{
						UseGravity = i.UseGravity.Resolve(ctx.Resolution),
						IsKinematic = i.IsKinematic.Resolve(ctx.Resolution),
						Mass = i.Mass.Resolve(ctx.Resolution),
						LinearDamping = i.LinearDamping.Resolve(ctx.Resolution),
						AngularDamping = i.AngularDamping.Resolve(ctx.Resolution),
						FreezePosition = i.FreezePosition.Resolve(ctx.Resolution),
						FreezeRotation = i.FreezeRotation.Resolve(ctx.Resolution),
						CentreOfMass = i.CentreOfMass.Resolve(ctx.Resolution)
					}),
				[typeof(VelocityInfo)] = Entry<VelocityInfo, Velocity, VelocityData>(
					(i, ctx) => new VelocityData(i.Id,
						i.Velocity.Resolve(ctx.Resolution))),
				[typeof(AccelerationInfo)] = Entry<AccelerationInfo, Acceleration, AccelerationData>(
					(i, ctx) => new AccelerationData(i.Id,
						i.Acceleration.Resolve(ctx.Resolution),
						i.Velocity.ResolveWritable(ctx.Resolution))),
				[typeof(DragInfo)] = Entry<DragInfo, DragBehaviour, DragData>(
					(i, ctx) => new DragData(i.Id,
						i.Velocity.ResolveWritable(ctx.Resolution),
						i.Coefficient.Resolve(ctx.Resolution))),
				[typeof(SpeedLimitInfo)] = Entry<SpeedLimitInfo, SpeedLimit, SpeedLimitData>(
					(i, ctx) => new SpeedLimitData(i.Id,
						i.Velocity.ResolveWritable(ctx.Resolution),
						i.Max.Resolve(ctx.Resolution))),
				[typeof(MoveTowardsInfo)] = Entry<MoveTowardsInfo, MoveTowards, MoveTowardsData>(
					(i, ctx) => new MoveTowardsData(i.Id,
						i.Target.Resolve(ctx.Resolution),
						i.Speed.Resolve(ctx.Resolution))),
				[typeof(SmoothMoveInfo)] = Entry<SmoothMoveInfo, SmoothMove, SmoothMoveData>(
					(i, ctx) => new SmoothMoveData(i.Id,
						i.Target.Resolve(ctx.Resolution),
						i.SmoothTime.Resolve(ctx.Resolution))),
				[typeof(ClampPositionInfo)] = Entry<ClampPositionInfo, ClampPosition, ClampPositionData>(
					(i, ctx) => new ClampPositionData(i.Id,
						i.Min.Resolve(ctx.Resolution),
						i.Max.Resolve(ctx.Resolution))),
				[typeof(WrapPositionInfo)] = Entry<WrapPositionInfo, WrapPosition, WrapPositionData>(
					(i, ctx) => new WrapPositionData(i.Id,
						i.Min.Resolve(ctx.Resolution),
						i.Max.Resolve(ctx.Resolution))),
				[typeof(TranslateInfo)] = Entry<TranslateInfo, Translate, TranslateData>(
					(i, ctx) => new TranslateData(i.Id,
						i.Displacement.Resolve(ctx.Resolution))),
				[typeof(AngularVelocityInfo)] = Entry<AngularVelocityInfo, AngularVelocity, AngularVelocityData>(
					(i, ctx) => new AngularVelocityData(i.Id,
						i.AngularVelocity.Resolve(ctx.Resolution))),
				[typeof(RotateInfo)] = Entry<RotateInfo, Rotate, RotateData>(
					(i, ctx) => new RotateData(i.Id,
						i.Displacement.Resolve(ctx.Resolution))),
				[typeof(SetRotationInfo)] = Entry<SetRotationInfo, SetRotation, SetRotationData>(
					(i, ctx) => new SetRotationData(i.Id,
						i.ValueExpression.Resolve(ctx.Resolution))),
				[typeof(MoveAnimationInfo)] = new(typeof(MoveAnimation), (go, info, ctx) =>
					BuildTransformAnimation<MoveAnimationInfo, MoveAnimation>(go, (MoveAnimationInfo)info, ctx,
						i => i.Start, i => i.End, i => i.Duration, i => i.Easing)),
				[typeof(ScaleAnimationInfo)] = new(typeof(ScaleAnimation), (go, info, ctx) =>
					BuildTransformAnimation<ScaleAnimationInfo, ScaleAnimation>(go, (ScaleAnimationInfo)info, ctx,
						i => i.Start, i => i.End, i => i.Duration, i => i.Easing)),
				[typeof(RotateAnimationInfo)] = new(typeof(RotateAnimation), (go, info, ctx) =>
					BuildTransformAnimation<RotateAnimationInfo, RotateAnimation>(go, (RotateAnimationInfo)info, ctx,
						i => i.Start, i => i.End, i => i.Duration, i => i.Easing)),
				[typeof(SetPositionInfo)] = Entry<SetPositionInfo, SetPosition, SetPositionData>(
					(i, ctx) => new SetPositionData(i.Id,
						i.ValueExpression.Resolve(ctx.Resolution))),
				[typeof(KeyHoldTriggerInfo)] = Entry<KeyHoldTriggerInfo, KeyHoldTrigger, KeyHoldTriggerData>(
					(i, ctx) => new KeyHoldTriggerData(i.Id,
						i.Key.Resolve(ctx.Resolution))),
				[typeof(KeyDownTriggerInfo)] = Entry<KeyDownTriggerInfo, KeyDownTrigger, KeyDownTriggerData>(
					(i, ctx) => new KeyDownTriggerData(i.Id,
						i.Key.Resolve(ctx.Resolution))),
				[typeof(KeyUpTriggerInfo)] = Entry<KeyUpTriggerInfo, KeyUpTrigger, KeyUpTriggerData>(
					(i, ctx) => new KeyUpTriggerData(i.Id,
						i.Key.Resolve(ctx.Resolution))),
				[typeof(MouseButtonTriggerInfo)] = Entry<MouseButtonTriggerInfo, MouseButtonTrigger, MouseButtonTriggerData>(
					(i, ctx) => new MouseButtonTriggerData(i.Id,
						i.Button.Resolve(ctx.Resolution),
						i.Phase.Resolve(ctx.Resolution))),
				[typeof(MousePositionTriggerInfo)] = Entry<MousePositionTriggerInfo, MousePositionTrigger, MousePositionTriggerData>(
					(i, ctx) => new MousePositionTriggerData(i.Id)),
				[typeof(ScrollWheelTriggerInfo)] = Entry<ScrollWheelTriggerInfo, ScrollWheelTrigger, ScrollWheelTriggerData>(
					(i, ctx) => new ScrollWheelTriggerData(i.Id)),
				[typeof(AxisTriggerInfo)] = Entry<AxisTriggerInfo, AxisTrigger, AxisTriggerData>(
					(i, ctx) => new AxisTriggerData(i.Id,
						i.XAxis.Resolve(ctx.Resolution),
						i.YAxis.Resolve(ctx.Resolution))),
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
				[typeof(GamepadButtonTriggerInfo)] = Entry<GamepadButtonTriggerInfo, GamepadButtonTrigger, GamepadButtonTriggerData>(
					(i, ctx) => new GamepadButtonTriggerData(i.Id,
						i.Button.Resolve(ctx.Resolution),
						i.Mode.Resolve(ctx.Resolution))),
				[typeof(TapTriggerInfo)] = Entry<TapTriggerInfo, Tap, TapTriggerData>(
					(i, ctx) => new TapTriggerData(i.Id,
						i.MaxDuration.Resolve(ctx.Resolution),
						i.MaxMovement.Resolve(ctx.Resolution))),
				[typeof(DoubleTapTriggerInfo)] = Entry<DoubleTapTriggerInfo, DoubleTap, DoubleTapTriggerData>(
					(i, ctx) => new DoubleTapTriggerData(i.Id,
						i.MaxInterval.Resolve(ctx.Resolution),
						i.MaxMovement.Resolve(ctx.Resolution))),
				[typeof(LongPressTriggerInfo)] = Entry<LongPressTriggerInfo, LongPress, LongPressTriggerData>(
					(i, ctx) => new LongPressTriggerData(i.Id,
						i.Duration.Resolve(ctx.Resolution),
						i.MaxMovement.Resolve(ctx.Resolution))),
				[typeof(SwipeTriggerInfo)] = Entry<SwipeTriggerInfo, Swipe, SwipeTriggerData>(
					(i, ctx) => new SwipeTriggerData(i.Id,
						i.MinDistance.Resolve(ctx.Resolution),
						i.MaxDuration.Resolve(ctx.Resolution))),
				[typeof(DragTriggerInfo)] = Entry<DragTriggerInfo, Drag, DragTriggerData>(
					(i, ctx) => new DragTriggerData(i.Id,
						i.Threshold.Resolve(ctx.Resolution))),
				[typeof(PinchAndRotateTriggerInfo)] = Entry<PinchAndRotateTriggerInfo, PinchAndRotate, PinchAndRotateTriggerData>(
					(i, ctx) => new PinchAndRotateTriggerData(i.Id)),
				[typeof(OnStartTriggerInfo)] = Entry<OnStartTriggerInfo, OnStartTrigger, OnStartTriggerData>(
					(i, ctx) => new OnStartTriggerData(i.Id)),
				[typeof(TimerTriggerInfo)] = Entry<TimerTriggerInfo, TimerTrigger, TimerTriggerData>(
					(i, ctx) => new TimerTriggerData(i.Id,
						i.Delay.Resolve(ctx.Resolution),
						i.AutoStart.Resolve(ctx.Resolution))),
				[typeof(DeferredTriggerInfo)] = Entry<DeferredTriggerInfo, DeferredTrigger, DeferredTriggerData>(
					(i, ctx) => new DeferredTriggerData(i.Id,
						i.Delay.Resolve(ctx.Resolution))),
				[typeof(DebouncedTriggerInfo)] = Entry<DebouncedTriggerInfo, DebouncedTrigger, DebouncedTriggerData>(
					(i, ctx) => new DebouncedTriggerData(i.Id,
						i.Interval.Resolve(ctx.Resolution))),
				[typeof(ThrottledTriggerInfo)] = Entry<ThrottledTriggerInfo, ThrottledTrigger, ThrottledTriggerData>(
					(i, ctx) => new ThrottledTriggerData(i.Id,
						i.Rate.Resolve(ctx.Resolution))),
				[typeof(IntervalTriggerInfo)] = Entry<IntervalTriggerInfo, IntervalTrigger, IntervalTriggerData>(
					(i, ctx) => new IntervalTriggerData(i.Id,
						i.Interval.Resolve(ctx.Resolution),
						i.Count.Resolve(ctx.Resolution),
						i.AutoStart.Resolve(ctx.Resolution))),
				[typeof(EveryFrameTriggerInfo)] = Entry<EveryFrameTriggerInfo, EveryFrameTrigger, EveryFrameTriggerData>(
					(i, ctx) => new EveryFrameTriggerData(i.Id)),
				// The physical collision/trigger behaviours derive from GameBehaviour<PhysicalTriggerData>
				// (via PhysicalTrigger), so TData is the PhysicalTriggerData base — the concrete *TriggerData
				// the lambda builds upcasts to it.
				[typeof(CollisionEnterTriggerInfo)] = Entry<CollisionEnterTriggerInfo, CollisionEnter, PhysicalTriggerData>(
					(i, ctx) => new CollisionEnterTriggerData(i.Id,
						i.TagsToDetect)),
				[typeof(CollisionExitTriggerInfo)] = Entry<CollisionExitTriggerInfo, CollisionExit, PhysicalTriggerData>(
					(i, ctx) => new CollisionExitTriggerData(i.Id,
						i.TagsToDetect)),
				[typeof(CollisionStayTriggerInfo)] = Entry<CollisionStayTriggerInfo, CollisionStay, PhysicalTriggerData>(
					(i, ctx) => new CollisionStayTriggerData(i.Id,
						i.TagsToDetect)),
				[typeof(TriggerEnterTriggerInfo)] = Entry<TriggerEnterTriggerInfo, TriggerEnter, PhysicalTriggerData>(
					(i, ctx) => new TriggerEnterTriggerData(i.Id,
						i.TagsToDetect)),
				[typeof(TriggerExitTriggerInfo)] = Entry<TriggerExitTriggerInfo, TriggerExit, PhysicalTriggerData>(
					(i, ctx) => new TriggerExitTriggerData(i.Id,
						i.TagsToDetect)),
				[typeof(TriggerStayTriggerInfo)] = Entry<TriggerStayTriggerInfo, TriggerStay, PhysicalTriggerData>(
					(i, ctx) => new TriggerStayTriggerData(i.Id,
						i.TagsToDetect)),
				[typeof(ConditionGateInfo)] = Entry<ConditionGateInfo, ConditionGate, ConditionGateData>(
					(i, ctx) => new ConditionGateData(i.Id,
						i.Condition.Resolve(ctx.Resolution))),
				[typeof(InverseConditionGateInfo)] = Entry<InverseConditionGateInfo, InverseConditionGate, ConditionGateData>(
					(i, ctx) => new ConditionGateData(i.Id,
						i.Condition.Resolve(ctx.Resolution))),
				// `condition` is a gate keyed off a declared boolean expression invoked by id + arguments
				// (the named-call ABI), so synthesise the ExpressionSource<bool> here and reuse ConditionGate's
				// data/runtime. The expression name is a literal at author time, read once via the id provider.
				[typeof(ConditionInfo)] = new(typeof(Condition), (go, info, ctx) =>
				{
					var i = (ConditionInfo)info;
					var b = go.AddComponent<Condition>();
					var expressionId = i.ExpressionId.Resolve(ctx.Resolution).Get();
					var condition = new ExpressionSource<bool>(expressionId, i.Arguments).Resolve(ctx.Resolution);
					return (b, lr => b.Initialise(new ConditionGateData(i.Id, condition),
						i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				// `when all` references sibling triggers by id and fires once all have fired. It can't be wired
				// into their listener lists (the inverse direction), so it subscribes to each trigger's fire
				// hook. Resolve the siblings against this entity here, where the live registry is in hand.
				[typeof(WhenAllInfo)] = new(typeof(WhenAll), (go, info, ctx) =>
				{
					var i = (WhenAllInfo)info;
					var b = go.AddComponent<WhenAll>();
					return (b, lr =>
					{
						b.Initialise(new WhenAllData(i.Id, i.TriggerIds),
							i.Listeners.ToListeners(lr, ctx.Resolution));

						var entityId = go.GetComponent<GameEntity>().Id;
						foreach (var triggerId in i.TriggerIds.Distinct())
						{
							b.Observe(ResolveSiblingTrigger(lr, entityId, triggerId, i.Id), triggerId);
						}
					}
					);
				}),
				[typeof(ExclusiveTriggerInfo)] = new(typeof(ExclusiveTrigger), (go, info, ctx) =>
				{
					var i = (ExclusiveTriggerInfo)info;
					var b = go.AddComponent<ExclusiveTrigger>();
					b.Registry = ctx.ExclusiveGroups;
					return (b, lr => b.Initialise(new ExclusiveTriggerData(i.Id,
						i.Group.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(CameraInfo)] = Entry<CameraInfo, CameraBehaviour, CameraData>(
					(i, ctx) => new CameraData(i.Id,
						i.View.Resolve(ctx.Resolution),
						i.Size.Resolve(ctx.Resolution),
						i.DefaultBlend.Resolve(ctx.Resolution))),
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
					}
					);
				}),
				[typeof(SpawnerInfo)] = Entry<SpawnerInfo, SpawnerBehaviour, SpawnerData>(
					(i, ctx) => new SpawnerData(i.Id,
						i.TemplateId.Resolve(ctx.Resolution),
						i.Templates.Select(t => new SpawnTemplate(t.TemplateId, t.Weight.Resolve(ctx.Resolution))).ToArray(),
						i.Selection.Resolve(ctx.Resolution),
						i.Position.Resolve(ctx.Resolution),
						i.Rotation.Resolve(ctx.Resolution),
						i.Parameters.ToDictionary(kv => kv.Key,
							kv => (IValueProvider)kv.Value.Resolve(ctx.Resolution)))),
				[typeof(DestroyInfo)] = Entry<DestroyInfo, DestroyBehaviour, DestroyData>(
					(i, ctx) => new DestroyData(i.Id)),
				[typeof(EndGameInfo)] = Entry<EndGameInfo, EndGame, EndGameData>(
					(i, ctx) => new EndGameData(i.Id)),
				[typeof(SpriteInfo)] = Entry<SpriteInfo, SpriteBehaviour, SpriteData>(
					(i, ctx) => new SpriteData(i.Id,
						i.Sprite.Resolve(ctx.Resolution),
						i.Size.Resolve(ctx.Resolution))),
				[typeof(VoxelMeshInfo)] = Entry<VoxelMeshInfo, VoxelMesh, VoxelMeshData>(
					(i, ctx) => new VoxelMeshData(i.Id,
						i.Mesh.Resolve(ctx.Resolution),
						i.Scale.Resolve(ctx.Resolution))),
				[typeof(PrimitiveInfo)] = Entry<PrimitiveInfo, Primitive, PrimitiveData>(
					(i, ctx) => new PrimitiveData(i.Id,
						i.Shape.Resolve(ctx.Resolution),
						i.Colour.Resolve(ctx.Resolution),
						i.Size.Resolve(ctx.Resolution))),
				[typeof(LightInfo)] = Entry<LightInfo, LightBehaviour, LightData>(
					(i, ctx) => new LightData(i.Id,
						i.Type.Resolve(ctx.Resolution),
						i.Colour.Resolve(ctx.Resolution),
						i.Intensity.Resolve(ctx.Resolution),
						i.Range.Resolve(ctx.Resolution),
						i.SpotAngle.Resolve(ctx.Resolution))),
				[typeof(ActivePollInfo)] = Entry<ActivePollInfo, ActivePoll, ActivePollData>(
					(i, ctx) => new ActivePollData(i.Id,
						i.Active.Resolve(ctx.Resolution))),
				[typeof(SetActiveInfo)] = Entry<SetActiveInfo, SetActive, SetActiveData>(
					(i, ctx) => new SetActiveData(i.Id,
						i.Active.Resolve(ctx.Resolution))),
				[typeof(SetTimeScaleInfo)] = Entry<SetTimeScaleInfo, SetTimeScale, SetTimeScaleData>(
					(i, ctx) => new SetTimeScaleData(i.Id,
						i.Scale.Resolve(ctx.Resolution))),
				[typeof(ToggleActiveInfo)] = Entry<ToggleActiveInfo, ToggleActive, ToggleActiveData>(
					(i, ctx) => new ToggleActiveData(i.Id)),
				[typeof(AudioSourceInfo)] = Entry<AudioSourceInfo, AudioSourceBehaviour, AudioSourceData>(
					(i, ctx) => new AudioSourceData(i.Id,
						i.Clip.Resolve(ctx.Resolution),
						i.PlayOnStart.Resolve(ctx.Resolution),
						i.Loop.Resolve(ctx.Resolution))),
				[typeof(SphereGizmoInfo)] = Entry<SphereGizmoInfo, SphereGizmoBehaviour, SphereGizmoData>(
					(i, ctx) => new SphereGizmoData(i.Id,
						i.Radius.Resolve(ctx.Resolution),
						i.IsWire.Resolve(ctx.Resolution),
						i.Colour.Resolve(ctx.Resolution))),
				[typeof(CubeGizmoInfo)] = Entry<CubeGizmoInfo, CubeGizmoBehaviour, CubeGizmoData>(
					(i, ctx) => new CubeGizmoData(i.Id,
						i.Size.Resolve(ctx.Resolution),
						i.IsWire.Resolve(ctx.Resolution),
						i.Colour.Resolve(ctx.Resolution))),
				[typeof(LineGizmoInfo)] = Entry<LineGizmoInfo, LineGizmoBehaviour, LineGizmoData>(
					(i, ctx) => new LineGizmoData(i.Id,
						i.Start.Resolve(ctx.Resolution),
						i.End.Resolve(ctx.Resolution),
						i.Colour.Resolve(ctx.Resolution))),
				[typeof(UICanvasInfo)] = Entry<UICanvasInfo, UICanvas, UICanvasData>(
					(i, ctx) => new UICanvasData(i.Id,
						i.MatchWidthOrHeight.Resolve(ctx.Resolution),
						i.ReferenceResolution.Resolve(ctx.Resolution))),
				[typeof(UIContainerInfo)] = Entry<UIContainerInfo, UIContainer, UIContainerData>(
					(i, ctx) => new UIContainerData(i.Id,
						i.Direction.Resolve(ctx.Resolution),
						i.Spacing.Resolve(ctx.Resolution),
						i.Padding.Resolve(ctx.Resolution),
						i.ChildAlignment.Resolve(ctx.Resolution),
						i.FitContent.Resolve(ctx.Resolution))),
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
						var current = res.Variables.Get<string>(i.StateVariable, scope).AsWritable();
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
				}),
				[typeof(PerceiveInfo)] = Entry<PerceiveInfo, Perceive, PerceiveData>(
					(i, ctx) => new PerceiveData(i.Id,
						i.Tag.Resolve(ctx.Resolution),
						i.Radius.Resolve(ctx.Resolution),
						i.ConeAngle.Resolve(ctx.Resolution),
						i.Forward.Resolve(ctx.Resolution),
						i.RequireLineOfSight.Resolve(ctx.Resolution),
						i.Obstacles.Resolve(ctx.Resolution),
						i.Interval.Resolve(ctx.Resolution),
						i.TargetId.ResolveWritable(ctx.Resolution),
						i.TargetPosition.ResolveWritable(ctx.Resolution),
						i.HasTarget.ResolveWritable(ctx.Resolution),
						i.LastKnownPosition.ResolveWritable(ctx.Resolution))),
				[typeof(PerceiveAllInfo)] = Entry<PerceiveAllInfo, PerceiveAll, PerceiveAllData>(
					(i, ctx) => new PerceiveAllData(i.Id,
						i.Tag.Resolve(ctx.Resolution),
						i.Radius.Resolve(ctx.Resolution),
						i.ConeAngle.Resolve(ctx.Resolution),
						i.Forward.Resolve(ctx.Resolution),
						i.RequireLineOfSight.Resolve(ctx.Resolution),
						i.Obstacles.Resolve(ctx.Resolution),
						i.Interval.Resolve(ctx.Resolution),
						i.Positions.Resolve(ctx.Resolution),
						i.Ids.Resolve(ctx.Resolution),
						i.Velocities.Resolve(ctx.Resolution),
						i.Count.ResolveWritable(ctx.Resolution))),
				[typeof(SteeringInfo)] = Entry<SteeringInfo, Steering, SteeringData>(
					(i, ctx) => new SteeringData(i.Id,
						i.Forces.Select(f => new SteeringForce(
							f.Force.Resolve(ctx.Resolution),
							f.Weight.Resolve(ctx.Resolution))).ToArray(),
						i.MaxSpeed.Resolve(ctx.Resolution),
						i.Output.ResolveWritable(ctx.Resolution))),
				[typeof(NavigateInfo)] = Entry<NavigateInfo, Navigate, NavigateData>(
					(i, ctx) => new NavigateData(i.Id,
						i.Target.Resolve(ctx.Resolution),
						i.Speed.Resolve(ctx.Resolution),
						i.SlowingRadius.Resolve(ctx.Resolution),
						i.Recompute.Resolve(ctx.Resolution),
						i.Mode.Resolve(ctx.Resolution),
						i.AgentRadius.Resolve(ctx.Resolution),
						i.Output.ResolveWritable(ctx.Resolution))),
				[typeof(GridMoverInfo)] = Entry<GridMoverInfo, GridMover, GridMoverData>(
					(i, ctx) => new GridMoverData(i.Id,
						i.Direction.Resolve(ctx.Resolution),
						i.Speed.Resolve(ctx.Resolution),
						i.AgentRadius.Resolve(ctx.Resolution))),
				[typeof(PatrolInfo)] = Entry<PatrolInfo, Patrol, PatrolData>(
					(i, ctx) => new PatrolData(i.Id,
						i.Waypoints.Resolve(ctx.Resolution),
						i.Loop.Resolve(ctx.Resolution),
						i.PingPong.Resolve(ctx.Resolution),
						i.ArriveRadius.Resolve(ctx.Resolution),
						i.Speed.Resolve(ctx.Resolution),
						i.Output.ResolveWritable(ctx.Resolution),
						i.CurrentIndex.ResolveWritable(ctx.Resolution)))
			};

			RegisterVariableSetter<Vector3, Vector3Setter>(map);
			RegisterVariableSetter<int, IntSetter>(map);
			RegisterVariableSetter<float, FloatSetter>(map);
			RegisterVariableSetter<bool, BoolSetter>(map);
			RegisterVariableSetter<string, StringSetter>(map);
			RegisterVariableSetter<Color, ColourSetter>(map);
			RegisterVariableSetter<Record, RecordSetter>(map);

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
			RegisterListOps<Record, RecordListAdd, RecordListInsert, RecordListRemoveAt, RecordListRemove, RecordListSetAt, RecordListSet, RecordListAddRange, RecordListClear, RecordListLoopTrigger>(map);

			return map;
		}

		// Generic builder for the common "cast info -> add component -> initialise with resolved data" entry.
		// Tying TInfo, TBehaviour and TData together at the type level makes the cast/AddComponent/data
		// triple compiler-checked, so a mismatched pairing fails to compile rather than at runtime.
		private static BuilderEntry Entry<TInfo, TBehaviour, TData>(
			Func<TInfo, BehaviourBuildContext, TData> makeData)
			where TInfo : BehaviourInfo
			where TBehaviour : GameBehaviour<TData>
			where TData : BehaviourData
			=> new(typeof(TBehaviour), (go, info, ctx) =>
			{
				var i = (TInfo)info;
				var b = go.AddComponent<TBehaviour>();
				return (b, lr => b.Initialise(makeData(i, ctx),
					i.Listeners.ToListeners(lr, ctx.Resolution)));
			});

		private static (GameBehaviour, InitialiseBehaviourEvent) BuildTransformAnimation<TInfo, TBehaviour>(
			GameObject go,
			TInfo info,
			BehaviourBuildContext ctx,
			Func<TInfo, ValueSource<Vector3>> start,
			Func<TInfo, ValueSource<Vector3>> end,
			Func<TInfo, ValueSource<float>> duration,
			Func<TInfo, ValueSource<Easing>> easing)
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
					i.ValueToSet.ResolveWritable(ctx.Resolution),
					i.ValueToGet.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
			});
		}

		private static void RegisterVariableChangedTrigger<T, TBehaviour>(IDictionary<Type, BuilderEntry> map)
			where TBehaviour : GameBehaviour<VariableChangedTriggerData<T>>
		{
			map[typeof(VariableChangedTriggerInfo<T>)] = new(typeof(TBehaviour), (go, info, ctx) =>
			{
				var i = (VariableChangedTriggerInfo<T>)info;

				// A change trigger can only observe a writable variable, so VariableId must be a !var reference —
				// a constant/expression/clock value has nothing to subscribe to. Hard-fail rather than silently
				// wiring a trigger that can never fire.
				if (i.VariableId is not ValueReferenceSource<T>)
				{
					throw new ResolveException(
						$"'{i.Id}': a variable changed trigger's VariableId must be a !var reference to a writable variable.");
				}

				var b = go.AddComponent<TBehaviour>();
				return (b, lr =>
				{
					var provider = i.VariableId.Resolve(ctx.Resolution);

					// Guards against a !var of the wrong declared type (resolves to a non-observable adapter, e.g. an
					// int variable referenced as float). The game would not work as declared, so hard-fail.
					if (provider is not IObservableValueProvider<T>)
					{
						throw new ResolveException(
							$"'{i.Id}': VariableId must reference a writable variable of type {typeof(T).Name}.");
					}

					b.Initialise(new VariableChangedTriggerData<T>(i.Id, provider),
						i.Listeners.ToListeners(lr, ctx.Resolution));
				}
				);
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

		// Resolves a `when all` TriggerId to the live behaviour on the same entity, with a descriptor-vocabulary
		// error if it's missing (rather than a bare KeyNotFoundException from the registry indexer).
		private static GameBehaviour ResolveSiblingTrigger(IReadOnlyBehaviourRegistry registry,
			string entityId,
			string triggerId,
			string whenAllId)
		{
			try
			{
				return registry[new BehaviourDescriptor(entityId, triggerId)];
			}
			catch (KeyNotFoundException)
			{
				throw new ArgumentException(
					$"'when all' behaviour '{whenAllId}' references trigger '{triggerId}', " +
					$"which does not exist on entity '{entityId}'.");
			}
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
					entityTagged.BehaviourId is { } behaviourId
						? tag => listenerRegistry.GetByEntityTagAndBehaviourId(tag, behaviourId)
						: listenerRegistry.GetByEntityTag,
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
