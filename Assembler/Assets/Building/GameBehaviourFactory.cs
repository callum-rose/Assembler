using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.Audio;
using Assembler.Behaviours.Debug.UI;
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
			AssetRegistry ar,
			TriggerContext tc);

		private readonly static Dictionary<Type, BehaviourBuilder> Builders = new()
		{
			[typeof(BoxColliderInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (BoxColliderInfo)info;
				var b = go.AddComponent<AutoAddBoxColliderBehaviour>();

				return (b, lr => b.Initialise(new BoxColliderData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.Size.Resolve(vr, cr, ar, tc),
					i.IsTrigger.Resolve(vr, cr, ar, tc))));
			},
			[typeof(SphereColliderInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (SphereColliderInfo)info;
				var b = go.AddComponent<AutoAddSphereColliderBehaviour>();

				return (b, lr => b.Initialise(new SphereColliderData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.Radius.Resolve(vr, cr, ar, tc))));
			},
			[typeof(RigidbodyInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (RigidbodyInfo)info;
				var b = go.AddComponent<RigidbodyBehaviour>();

				return (b, lr => b.Initialise(new RigidbodyData(i.Id, i.Listeners.ToActions(lr, tc))
				{
					UseGravity = i.UseGravity.Resolve(vr, cr, ar, tc)
				}));
			},
			[typeof(VelocityInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (VelocityInfo)info;
				var b = go.AddComponent<Velocity>();

				return (b, lr => b.Initialise(new VelocityData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.Velocity.Resolve(vr, cr, ar, tc))));
			},
			[typeof(TranslateInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (TranslateInfo)info;
				var b = go.AddComponent<Translate>();

				return (b, lr => b.Initialise(new TranslateData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.Displacement.Resolve(vr, cr, ar, tc))));
			},
			[typeof(SetPositionInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (SetPositionInfo)info;
				var b = go.AddComponent<SetPosition>();

				return (b, lr => b.Initialise(new SetPositionData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.ValueExpression.Resolve(vr, cr, ar, tc))));
			},
			[typeof(KeyHoldTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (KeyHoldTriggerInfo)info;
				var b = go.AddComponent<KeyHoldTrigger>();

				return (b, lr => b.Initialise(new KeyHoldTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar, tc),
					i.Listeners.ToActions(lr, tc))));
			},
			[typeof(KeyDownTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (KeyDownTriggerInfo)info;
				var b = go.AddComponent<KeyDownTrigger>();

				return (b, lr => b.Initialise(new KeyDownTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar, tc),
					i.Listeners.ToActions(lr, tc))));
			},
			[typeof(KeyUpTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (KeyUpTriggerInfo)info;
				var b = go.AddComponent<KeyUpTrigger>();

				return (b, lr => b.Initialise(new KeyUpTriggerData(i.Id,
					i.Key.Resolve(vr, cr, ar, tc),
					i.Listeners.ToActions(lr, tc))));
			},
			[typeof(TapTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (TapTriggerInfo)info;
				var b = go.AddComponent<Tap>();
				return (b, lr => b.Initialise(new TapTriggerData(i.Id, i.Listeners.ToActions(lr, tc))));
			},
			[typeof(DoubleTapTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (DoubleTapTriggerInfo)info;
				var b = go.AddComponent<DoubleTap>();
				return (b, lr => b.Initialise(new DoubleTapTriggerData(i.Id, i.Listeners.ToActions(lr, tc))));
			},
			[typeof(LongPressTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (LongPressTriggerInfo)info;
				var b = go.AddComponent<LongPress>();
				return (b, lr => b.Initialise(new LongPressTriggerData(i.Id, i.Listeners.ToActions(lr, tc))));
			},
			[typeof(SwipeTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (SwipeTriggerInfo)info;
				var b = go.AddComponent<Swipe>();
				return (b, lr => b.Initialise(new SwipeTriggerData(i.Id, i.Listeners.ToActions(lr, tc))));
			},
			[typeof(DragTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (DragTriggerInfo)info;
				var b = go.AddComponent<Drag>();
				return (b, lr => b.Initialise(new DragTriggerData(i.Id, i.Listeners.ToActions(lr, tc))));
			},
			[typeof(PinchTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (PinchTriggerInfo)info;
				var b = go.AddComponent<Pinch>();
				return (b, lr => b.Initialise(new PinchTriggerData(i.Id, i.Listeners.ToActions(lr, tc))));
			},
			[typeof(RotateTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (RotateTriggerInfo)info;
				var b = go.AddComponent<Rotate>();
				return (b, lr => b.Initialise(new RotateTriggerData(i.Id, i.Listeners.ToActions(lr, tc))));
			},
			[typeof(OnStartTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (OnStartTriggerInfo)info;
				var b = go.AddComponent<OnStartTrigger>();
				return (b, lr => b.Initialise(new OnStartTriggerData(i.Id, i.Listeners.ToActions(lr, tc))));
			},
			[typeof(TimerTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (TimerTriggerInfo)info;
				var b = go.AddComponent<TimerTrigger>();

				return (b, lr => b.Initialise(new TimerTriggerData(i.Id,
					i.Delay.Resolve(vr, cr, ar, tc),
					i.Listeners.ToActions(lr, tc))));
			},
			[typeof(IntervalTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (IntervalTriggerInfo)info;
				var b = go.AddComponent<IntervalTrigger>();

				return (b, lr => b.Initialise(new IntervalTriggerData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.Interval.Resolve(vr, cr, ar, tc),
					i.Count.Resolve(vr, cr, ar, tc),
					i.AutoStart.Resolve(vr, cr, ar, tc))));
			},
			[typeof(EveryFrameTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (EveryFrameTriggerInfo)info;
				var b = go.AddComponent<EveryFrameTrigger>();
				return (b, lr => b.Initialise(new EveryFrameTriggerData(i.Id, i.Listeners.ToActions(lr, tc))));
			},
			[typeof(CollisionEnterTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (CollisionEnterTriggerInfo)info;
				var b = go.AddComponent<CollisionEnter>();
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new CollisionEnterTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, tc))));
			},
			[typeof(CollisionExitTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (CollisionExitTriggerInfo)info;
				var b = go.AddComponent<CollisionExit>();
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new CollisionExitTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, tc))));
			},
			[typeof(CollisionStayTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (CollisionStayTriggerInfo)info;
				var b = go.AddComponent<CollisionStay>();
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new CollisionStayTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, tc))));
			},
			[typeof(TriggerEnterTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (TriggerEnterTriggerInfo)info;
				var b = go.AddComponent<TriggerEnter>();
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new TriggerEnterTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, tc))));
			},
			[typeof(TriggerExitTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (TriggerExitTriggerInfo)info;
				var b = go.AddComponent<TriggerExit>();
				b.TriggerContext = tc;

				return (b, lr => b.Initialise(new TriggerExitTriggerData(i.Id,
					i.TagsToDetect,
					i.Listeners.ToActions(lr, tc))));
			},
			[typeof(ConditionTriggerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (ConditionTriggerInfo)info;
				var b = go.AddComponent<Condition>();

				return (b, lr => b.Initialise(new ConditionData(i.Id,
					i.Condition.Resolve(vr, cr, ar, tc),
					i.Listeners.ToActions(lr, tc))));
			},
			[typeof(CameraInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (CameraInfo)info;
				var b = go.AddComponent<CameraBehaviour>();

				return (b, lr => b.Initialise(new CameraData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.View.Resolve(vr, cr, ar, tc),
					i.Size.Resolve(vr, cr, ar, tc))));
			},
			[typeof(SpawnerInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (SpawnerInfo)info;
				var b = go.AddComponent<SpawnerBehaviour>();
				b.Spawner = es;

				return (b, lr => b.Initialise(new SpawnerData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.TemplateId.Resolve(vr, cr, ar, tc),
					i.Position.Resolve(vr, cr, ar, tc),
					i.Rotation.Resolve(vr, cr, ar, tc),
					i.Parameters.ToDictionary(kv => kv.Key, kv => kv.Value.Resolve(vr, cr, ar, tc)))));
			},
			[typeof(DestroyInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (DestroyInfo)info;
				var b = go.AddComponent<DestroyBehaviour>();
				return (b, lr => b.Initialise(new DestroyData(i.Id, i.Listeners.ToActions(lr, tc))));
			},
			[typeof(VariableSetterInfo<Vector3>)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (VariableSetterInfo<Vector3>)info;
				var b = go.AddComponent<Vector3Setter>();

				return (b, lr => b.Initialise(new VariableSetterData<Vector3>(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.ValueToSet.Resolve(vr, cr, ar, tc),
					i.ValueToGet.Resolve(vr, cr, ar, tc))));
			},
			[typeof(VariableSetterInfo<int>)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (VariableSetterInfo<int>)info;
				var b = go.AddComponent<IntSetter>();

				return (b, lr => b.Initialise(new VariableSetterData<int>(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.ValueToSet.Resolve(vr, cr, ar, tc),
					i.ValueToGet.Resolve(vr, cr, ar, tc))));
			},
			[typeof(VariableSetterInfo<float>)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (VariableSetterInfo<float>)info;
				var b = go.AddComponent<FloatSetter>();

				return (b, lr => b.Initialise(new VariableSetterData<float>(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.ValueToSet.Resolve(vr, cr, ar, tc),
					i.ValueToGet.Resolve(vr, cr, ar, tc))));
			},
			[typeof(VariableSetterInfo<bool>)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (VariableSetterInfo<bool>)info;
				var b = go.AddComponent<BoolSetter>();

				return (b, lr => b.Initialise(new VariableSetterData<bool>(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.ValueToSet.Resolve(vr, cr, ar, tc),
					i.ValueToGet.Resolve(vr, cr, ar, tc))));
			},
			[typeof(VariableSetterInfo<string>)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (VariableSetterInfo<string>)info;
				var b = go.AddComponent<StringSetter>();

				return (b, lr => b.Initialise(new VariableSetterData<string>(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.ValueToSet.Resolve(vr, cr, ar, tc),
					i.ValueToGet.Resolve(vr, cr, ar, tc))));
			},
			[typeof(SpriteInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (SpriteInfo)info;
				var b = go.AddComponent<SpriteBehaviour>();

				return (b, lr => b.Initialise(new SpriteData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.Sprite.Resolve(vr, cr, ar, tc),
					i.Size.Resolve(vr, cr, ar, tc))));
			},
			[typeof(AudioSourceInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (AudioSourceInfo)info;
				var b = go.AddComponent<AudioSourceBehaviour>();

				return (b, lr => b.Initialise(new AudioSourceData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.Clip.Resolve(vr, cr, ar, tc),
					i.PlayOnStart.Resolve(vr, cr, ar, tc),
					i.Loop.Resolve(vr, cr, ar, tc))));
			},
			[typeof(SphereGizmoInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (SphereGizmoInfo)info;
				var b = go.AddComponent<SphereGizmoBehaviour>();

				return (b, lr => b.Initialise(new SphereGizmoData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.Radius.Resolve(vr, cr, ar, tc),
					i.IsWire.Resolve(vr, cr, ar, tc),
					i.Colour.Resolve(vr, cr, ar, tc))));
			},
			[typeof(CubeGizmoInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (CubeGizmoInfo)info;
				var b = go.AddComponent<CubeGizmoBehaviour>();

				return (b, lr => b.Initialise(new CubeGizmoData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.Size.Resolve(vr, cr, ar, tc),
					i.IsWire.Resolve(vr, cr, ar, tc),
					i.Colour.Resolve(vr, cr, ar, tc))));
			},
			[typeof(TextLabelInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (TextLabelInfo)info;
				var b = go.AddComponent<TextLabel>();
				return (b, lr => b.Initialise(new TextLabelData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.Text.Resolve(vr, cr, ar, tc),
					i.Label.Resolve(vr, cr, ar, tc),
					i.FontSize.Resolve(vr, cr, ar, tc),
					i.Rect)));
			},
			[typeof(ProgressBarInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (ProgressBarInfo)info;
				var b = go.AddComponent<ProgressBar>();
				return (b, lr => b.Initialise(new ProgressBarData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.Value.Resolve(vr, cr, ar, tc),
					i.Rect)));
			},
			[typeof(UIImageInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (UIImageInfo)info;
				var b = go.AddComponent<UIImage>();
				return (b, lr => b.Initialise(new UIImageData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.Colour.Resolve(vr, cr, ar, tc),
					i.Rect)));
			},
			[typeof(UIButtonInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (UIButtonInfo)info;
				var b = go.AddComponent<UIButton>();
				b.TriggerContext = tc;
				return (b, lr => b.Initialise(new UIButtonData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.Label.Resolve(vr, cr, ar, tc),
					i.Rect)));
			},
			[typeof(UIToggleInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (UIToggleInfo)info;
				var b = go.AddComponent<UIToggle>();
				b.TriggerContext = tc;
				return (b, lr => b.Initialise(new UIToggleData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.InitialValue.Resolve(vr, cr, ar, tc),
					i.Label.Resolve(vr, cr, ar, tc),
					i.Rect)));
			},
			[typeof(UISliderInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (UISliderInfo)info;
				var b = go.AddComponent<UISlider>();
				b.TriggerContext = tc;
				return (b, lr => b.Initialise(new UISliderData(i.Id,
					i.Listeners.ToActions(lr, tc),
					i.InitialValue.Resolve(vr, cr, ar, tc),
					i.MinValue.Resolve(vr, cr, ar, tc),
					i.MaxValue.Resolve(vr, cr, ar, tc),
					i.Rect)));
			},
			[typeof(UIInputFieldInfo)] = (go, info, vr, cr, es, ar, tc) =>
			{
				var i = (UIInputFieldInfo)info;
				var b = go.AddComponent<UIInputField>();
				b.TriggerContext = tc;
				return (b, lr => b.Initialise(new UIInputFieldData(i.Id,
					i.Listeners.ToActions(lr, tc),
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
			TriggerContext triggerContext)
		{
			return Builders.TryGetValue(behaviourInfo.GetType(), out var builder)
				? builder(gameObject,
					behaviourInfo,
					variableRegistry,
					compiledExpressionRegistry,
					entitySpawner,
					assets,
					triggerContext)
				: throw new ArgumentException($"Unsupported behaviour info type '{behaviourInfo.GetType()}'");
		}

		private static IReadOnlyList<Action> ToActions(this IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyBehaviourRegistry listenerRegistry,
			TriggerContext triggerContext) =>
			listeners.Select(d =>
			{
				if (d.IsDynamic)
					return BuildDynamicAction(d, listenerRegistry, triggerContext);

				var behaviour = listenerRegistry[d.BehaviourDescriptor];

				if (d.OutputMapping.Count == 0)
					return (Action)behaviour.Execute;

				return () =>
				{
					triggerContext.ApplyMapping(d.OutputMapping);
					behaviour.Execute();
				};
			}).ToArray();

		private static Action BuildDynamicAction(ListenerInfo listener,
			IReadOnlyBehaviourRegistry registry,
			TriggerContext triggerContext)
		{
			return () =>
			{
				if (listener.OutputMapping.Count > 0)
					triggerContext.ApplyMapping(listener.OutputMapping);

				var targets = listener.BehaviourTag != null
					? registry.GetByBehaviourTag(listener.BehaviourTag, listener.EntityTag)
					: registry.GetByEntityTagAndBehaviourId(listener.EntityTag!, listener.BehaviourDescriptor.BehaviourId);

				foreach (var behaviour in targets)
					if (behaviour) behaviour.Execute();
			};
		}
	}
}
