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
using Assembler.Behaviours.Visual;
using Assembler.Behaviours.Triggers;
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

			if (behaviour is INeedsTriggerContext needsTriggerContext)
			{
				needsTriggerContext.TriggerContext = ctx.Resolution.TriggerContext;
			}
			
			if (behaviour is INeedsSpawner needsSpawner)
			{
				needsSpawner.Spawner = ctx.Spawner;
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
				[typeof(RigidbodyInfo)] = new(typeof(RigidbodyBehaviour), (go, info, ctx) =>
				{
					var i = (RigidbodyInfo)info;
					var b = go.AddComponent<RigidbodyBehaviour>();
					return (b, lr => b.Initialise(new RigidbodyData(i.Id)
					{
						UseGravity = i.UseGravity.Resolve(ctx.Resolution)
					}, i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(VelocityInfo)] = new(typeof(Velocity), (go, info, ctx) =>
				{
					var i = (VelocityInfo)info;
					var b = go.AddComponent<Velocity>();
					return (b, lr => b.Initialise(new VelocityData(i.Id,
						i.Velocity.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
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
					return (b, lr => b.Initialise(new TapTriggerData(i.Id), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(DoubleTapTriggerInfo)] = new(typeof(DoubleTap), (go, info, ctx) =>
				{
					var i = (DoubleTapTriggerInfo)info;
					var b = go.AddComponent<DoubleTap>();
					return (b, lr => b.Initialise(new DoubleTapTriggerData(i.Id), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(LongPressTriggerInfo)] = new(typeof(LongPress), (go, info, ctx) =>
				{
					var i = (LongPressTriggerInfo)info;
					var b = go.AddComponent<LongPress>();
					return (b, lr => b.Initialise(new LongPressTriggerData(i.Id), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(SwipeTriggerInfo)] = new(typeof(Swipe), (go, info, ctx) =>
				{
					var i = (SwipeTriggerInfo)info;
					var b = go.AddComponent<Swipe>();
					return (b, lr => b.Initialise(new SwipeTriggerData(i.Id), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(DragTriggerInfo)] = new(typeof(Drag), (go, info, ctx) =>
				{
					var i = (DragTriggerInfo)info;
					var b = go.AddComponent<Drag>();
					return (b, lr => b.Initialise(new DragTriggerData(i.Id), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(PinchTriggerInfo)] = new(typeof(Pinch), (go, info, ctx) =>
				{
					var i = (PinchTriggerInfo)info;
					var b = go.AddComponent<Pinch>();
					return (b, lr => b.Initialise(new PinchTriggerData(i.Id), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(RotateTriggerInfo)] = new(typeof(Assembler.Behaviours.Triggers.Input.Rotate), (go, info, ctx) =>
				{
					var i = (RotateTriggerInfo)info;
					var b = go.AddComponent<Assembler.Behaviours.Triggers.Input.Rotate>();
					return (b, lr => b.Initialise(new RotateTriggerData(i.Id), i.Listeners.ToListeners(lr, ctx.Resolution)));
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
						i.Size.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
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
				[typeof(TextLabelInfo)] = new(typeof(TextLabel), (go, info, ctx) =>
				{
					var i = (TextLabelInfo)info;
					var b = go.AddComponent<TextLabel>();
					return (b, lr => b.Initialise(new TextLabelData(i.Id,
						i.Text.Resolve(ctx.Resolution),
						i.Label.Resolve(ctx.Resolution),
						i.FontSize.Resolve(ctx.Resolution),
						i.Rect), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(ProgressBarInfo)] = new(typeof(ProgressBar), (go, info, ctx) =>
				{
					var i = (ProgressBarInfo)info;
					var b = go.AddComponent<ProgressBar>();
					return (b, lr => b.Initialise(new ProgressBarData(i.Id,
						i.Value.Resolve(ctx.Resolution),
						i.Rect), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(UIImageInfo)] = new(typeof(UIImage), (go, info, ctx) =>
				{
					var i = (UIImageInfo)info;
					var b = go.AddComponent<UIImage>();
					return (b, lr => b.Initialise(new UIImageData(i.Id,
						i.Colour.Resolve(ctx.Resolution),
						i.Rect), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(UIButtonInfo)] = new(typeof(UIButton), (go, info, ctx) =>
				{
					var i = (UIButtonInfo)info;
					var b = go.AddComponent<UIButton>();
					return (b, lr => b.Initialise(new UIButtonData(i.Id,
						i.Label.Resolve(ctx.Resolution),
						i.Rect), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(UIToggleInfo)] = new(typeof(UIToggle), (go, info, ctx) =>
				{
					var i = (UIToggleInfo)info;
					var b = go.AddComponent<UIToggle>();
					return (b, lr => b.Initialise(new UIToggleData(i.Id,
						i.InitialValue.Resolve(ctx.Resolution),
						i.Label.Resolve(ctx.Resolution),
						i.Rect), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(UISliderInfo)] = new(typeof(UISlider), (go, info, ctx) =>
				{
					var i = (UISliderInfo)info;
					var b = go.AddComponent<UISlider>();
					return (b, lr => b.Initialise(new UISliderData(i.Id,
						i.InitialValue.Resolve(ctx.Resolution),
						i.MinValue.Resolve(ctx.Resolution),
						i.MaxValue.Resolve(ctx.Resolution),
						i.Rect), i.Listeners.ToListeners(lr, ctx.Resolution)));
				}),
				[typeof(UIInputFieldInfo)] = new(typeof(UIInputField), (go, info, ctx) =>
				{
					var i = (UIInputFieldInfo)info;
					var b = go.AddComponent<UIInputField>();
					return (b, lr => b.Initialise(new UIInputFieldData(i.Id,
						i.Rect), i.Listeners.ToListeners(lr, ctx.Resolution)));
				})
			};

			RegisterVariableSetter<Vector3, Vector3Setter>(map);
			RegisterVariableSetter<int, IntSetter>(map);
			RegisterVariableSetter<float, FloatSetter>(map);
			RegisterVariableSetter<bool, BoolSetter>(map);
			RegisterVariableSetter<string, StringSetter>(map);

			RegisterListOps<Vector3, Vector3ListAdd, Vector3ListRemoveAt, Vector3ListSetAt, Vector3ListClear>(map);
			RegisterListOps<int, IntListAdd, IntListRemoveAt, IntListSetAt, IntListClear>(map);
			RegisterListOps<float, FloatListAdd, FloatListRemoveAt, FloatListSetAt, FloatListClear>(map);
			RegisterListOps<bool, BoolListAdd, BoolListRemoveAt, BoolListSetAt, BoolListClear>(map);
			RegisterListOps<string, StringListAdd, StringListRemoveAt, StringListSetAt, StringListClear>(map);
			RegisterListOps<Color, ColourListAdd, ColourListRemoveAt, ColourListSetAt, ColourListClear>(map);

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

		private static void RegisterListOps<T, TAdd, TRemoveAt, TSetAt, TClear>(IDictionary<Type, BuilderEntry> map)
			where TAdd : GameBehaviour<ListAddData<T>>
			where TRemoveAt : GameBehaviour<ListRemoveAtData<T>>
			where TSetAt : GameBehaviour<ListSetAtData<T>>
			where TClear : GameBehaviour<ListClearData<T>>
		{
			map[typeof(ListAddInfo<T>)] = new(typeof(TAdd), (go, info, ctx) =>
			{
				var i = (ListAddInfo<T>)info;
				var b = go.AddComponent<TAdd>();
				return (b, lr => b.Initialise(new ListAddData<T>(i.Id,
					i.List.Resolve(ctx.Resolution),
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
			map[typeof(ListSetAtInfo<T>)] = new(typeof(TSetAt), (go, info, ctx) =>
			{
				var i = (ListSetAtInfo<T>)info;
				var b = go.AddComponent<TSetAt>();
				return (b, lr => b.Initialise(new ListSetAtData<T>(i.Id,
					i.List.Resolve(ctx.Resolution),
					i.Index.Resolve(ctx.Resolution),
					i.Value.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
			});
			map[typeof(ListClearInfo<T>)] = new(typeof(TClear), (go, info, ctx) =>
			{
				var i = (ListClearInfo<T>)info;
				var b = go.AddComponent<TClear>();
				return (b, lr => b.Initialise(new ListClearData<T>(i.Id,
					i.List.Resolve(ctx.Resolution)), i.Listeners.ToListeners(lr, ctx.Resolution)));
			});
		}

		private static IReadOnlyList<Listener> ToListeners(this IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyBehaviourRegistry listenerRegistry,
			ResolutionContext ctx) =>
			listeners.Select(l => (Listener)(l switch
			{
				DirectListenerInfo direct => new DirectListener(
					listenerRegistry[direct.BehaviourDescriptor],
					direct.OutputMapping,
					ctx.TriggerContext),
				EntityTaggedListenerInfo entityTagged => new EntityTaggedListener(
					entityTagged.EntityTag.Resolve(ctx),
					entityTagged.BehaviourId,
					listenerRegistry.GetByEntityTagAndBehaviourId,
					entityTagged.OutputMapping,
					ctx.TriggerContext),
				BehaviourTaggedListenerInfo behaviourTagged => new BehaviourTaggedListener(
					behaviourTagged.BehaviourTag.Resolve(ctx),
					tag => listenerRegistry.GetByBehaviourTag(tag),
					behaviourTagged.OutputMapping,
					ctx.TriggerContext),
				_ => throw new ArgumentException($"Unsupported listener type '{l.GetType()}'")
			})).ToArray();
	}
}
