using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Parsing.Phase2.Info;
using UnityEngine;

namespace Assembler.Parsing.Phase2
{
	public static class TemplateInstantiator
	{
		public static EntityInfo Instantiate(
			EntityInfo template,
			string newEntityId,
			ValueSource<Vector3> overridePosition,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<VariableInfo> allValues)
		{
			var behaviours = template.Behaviours
				.Select(b => SubstituteBehaviour(b, parameters, allValues))
				.ToArray();

			return new ConcreteEntityInfo(
				newEntityId,
				NullEntityInfo.Instance,
				template.Tags,
				overridePosition,
				Substitute(template.InitialRotation, parameters, allValues),
				behaviours);
		}

		public static ValueSource<T> Substitute<T>(
			ValueSource<T> source,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<VariableInfo> allValues)
		{
			switch (source)
			{
				case ParameterSource<T> p:
				{
					if (!parameters.TryGetValue(p.ParameterId, out var raw))
					{
						throw new ParsingException($"Parameter '{p.ParameterId}' not supplied during template instantiation");
					}

					return Transformer.Wrap<T>(allValues, raw, parameters: parameters);
				}

				case ExpressionSource<T> e:
				{
					var newArgs = e.Arguments
						.Select(a => Substitute(a, parameters, allValues))
						.ToArray();
					return new ExpressionSource<T>(e.ExpressionId, newArgs);
				}

				default:
					return source;
			}
		}

		public static BehaviourInfo SubstituteBehaviour(
			BehaviourInfo info,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<VariableInfo> allValues)
		{
			var listeners = SubstituteListeners(info.Listeners, parameters);

			return info switch
			{
				BoxColliderInfo b => new BoxColliderInfo(b.Id, listeners,
					Substitute(b.Size, parameters, allValues),
					Substitute(b.IsTrigger, parameters, allValues)),

				SphereColliderInfo b => new SphereColliderInfo(b.Id, listeners,
					Substitute(b.Radius, parameters, allValues),
					Substitute(b.IsTrigger, parameters, allValues)),

				RigidbodyInfo b => new RigidbodyInfo(b.Id, listeners,
					Substitute(b.UseGravity, parameters, allValues)),

				VelocityInfo b => new VelocityInfo(b.Id, listeners,
					Substitute(b.Velocity, parameters, allValues)),

				TranslateInfo b => new TranslateInfo(b.Id, listeners,
					Substitute(b.Displacement, parameters, allValues)),

				KeyHoldTriggerInfo b => new KeyHoldTriggerInfo(b.Id, listeners,
					Substitute(b.Key, parameters, allValues)),

				KeyDownTriggerInfo b => new KeyDownTriggerInfo(b.Id, listeners,
					Substitute(b.Key, parameters, allValues)),

				KeyUpTriggerInfo b => new KeyUpTriggerInfo(b.Id, listeners,
					Substitute(b.Key, parameters, allValues)),

				TapTriggerInfo b => new TapTriggerInfo(b.Id, listeners),
				DoubleTapTriggerInfo b => new DoubleTapTriggerInfo(b.Id, listeners),
				LongPressTriggerInfo b => new LongPressTriggerInfo(b.Id, listeners),
				SwipeTriggerInfo b => new SwipeTriggerInfo(b.Id, listeners),
				DragTriggerInfo b => new DragTriggerInfo(b.Id, listeners),
				PinchTriggerInfo b => new PinchTriggerInfo(b.Id, listeners),
				RotateTriggerInfo b => new RotateTriggerInfo(b.Id, listeners),

				TimerTriggerInfo b => new TimerTriggerInfo(b.Id, listeners,
					Substitute(b.Delay, parameters, allValues)),

				IntervalTriggerInfo b => new IntervalTriggerInfo(b.Id, listeners,
					Substitute(b.Interval, parameters, allValues)),

				EveryFrameTriggerInfo b => new EveryFrameTriggerInfo(b.Id, listeners),

				CollisionEnterTriggerInfo b => new CollisionEnterTriggerInfo(b.Id, listeners, b.TagsToDetect),
				CollisionExitTriggerInfo b => new CollisionExitTriggerInfo(b.Id, listeners, b.TagsToDetect),
				CollisionStayTriggerInfo b => new CollisionStayTriggerInfo(b.Id, listeners, b.TagsToDetect),
				TriggerEnterTriggerInfo b => new TriggerEnterTriggerInfo(b.Id, listeners, b.TagsToDetect),
				TriggerExitTriggerInfo b => new TriggerExitTriggerInfo(b.Id, listeners, b.TagsToDetect),
				TriggerStayTriggerInfo b => new TriggerStayTriggerInfo(b.Id, listeners, b.TagsToDetect),

				WhenAllInfo b => new WhenAllInfo(b.Id, listeners, b.TriggerIds),
				WhenAnyInfo b => new WhenAnyInfo(b.Id, listeners, b.TriggerIds),

				ConditionTriggerInfo b => new ConditionTriggerInfo(b.Id, listeners,
					Substitute(b.Condition, parameters, allValues)),

				SetPositionInfo b => new SetPositionInfo(b.Id, listeners,
					Substitute(b.ValueExpression, parameters, allValues)),

				CameraInfo b => new CameraInfo(b.Id, listeners,
					Substitute(b.View, parameters, allValues),
					Substitute(b.Size, parameters, allValues)),

				SpawnerInfo b => new SpawnerInfo(b.Id, listeners,
					Substitute(b.TemplateId, parameters, allValues),
					Substitute(b.Position, parameters, allValues)),

				VariableSetterInfo<Vector3> b => new VariableSetterInfo<Vector3>(b.Id, listeners,
					Substitute(b.ValueToSet, parameters, allValues),
					Substitute(b.ValueToGet, parameters, allValues)),

				VariableSetterInfo<int> b => new VariableSetterInfo<int>(b.Id, listeners,
					Substitute(b.ValueToSet, parameters, allValues),
					Substitute(b.ValueToGet, parameters, allValues)),

				VariableSetterInfo<float> b => new VariableSetterInfo<float>(b.Id, listeners,
					Substitute(b.ValueToSet, parameters, allValues),
					Substitute(b.ValueToGet, parameters, allValues)),

				VariableSetterInfo<bool> b => new VariableSetterInfo<bool>(b.Id, listeners,
					Substitute(b.ValueToSet, parameters, allValues),
					Substitute(b.ValueToGet, parameters, allValues)),

				VariableSetterInfo<string> b => new VariableSetterInfo<string>(b.Id, listeners,
					Substitute(b.ValueToSet, parameters, allValues),
					Substitute(b.ValueToGet, parameters, allValues)),
				
				DestroyInfo d => d,

				_ => throw new ArgumentException($"Cannot substitute parameters into behaviour info type '{info.GetType()}'")
			};
		}

		private static IReadOnlyList<BehaviourDescriptor> SubstituteListeners(
			IReadOnlyList<BehaviourDescriptor> listeners,
			IReadOnlyDictionary<string, object> parameters)
		{
			if (listeners.Count == 0)
			{
				return listeners;
			}

			var result = new BehaviourDescriptor[listeners.Count];
			for (var i = 0; i < listeners.Count; i++)
			{
				var l = listeners[i];
				if (l.EntityId.StartsWith(Transformer.ParameterEntityIdSentinel))
				{
					var paramId = l.EntityId.Substring(Transformer.ParameterEntityIdSentinel.Length);
					if (!parameters.TryGetValue(paramId, out var raw) || raw is not string entityId)
					{
						throw new ParsingException(
							$"Listener parameter '{paramId}' is missing or not a string");
					}

					result[i] = new BehaviourDescriptor(entityId, l.BehaviourId);
				}
				else
				{
					result[i] = l;
				}
			}

			return result;
		}
	}
}
