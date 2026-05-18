using System.Collections.Generic;
using System.Linq;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Parsing
{
	public static class TemplateInstantiator
	{
		public static EntityInfo Instantiate(
			EntityInfo template,
			string newEntityId,
			ValueSource<Vector3> overridePosition,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues)
		{
			var behaviours = template.Behaviours
				.Select(b => SubstituteBehaviour(b, parameters, allValues))
				.ToArray();

			return new ConcreteEntityInfo(
				newEntityId,
				NullEntityInfo.Instance,
				template.Tags,
				overridePosition,
				template.InitialRotation.Substitute(parameters, allValues),
				behaviours);
		}

		public static ValueSource<T> Substitute<T>(this ValueSource<T> source,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues)
		{
			return source switch
			{
				ParameterSource<T> p => !parameters.TryGetValue(p.ParameterId, out var raw)
					? throw new ParsingException($"Parameter '{p.ParameterId}' not supplied during template instantiation")
					: Transformer.Wrap<T>(allValues, raw, parameters: parameters),
				ExpressionSource<T> e => new ExpressionSource<T>(e.ExpressionId,
					e.Arguments.Select(a => a.Substitute(parameters, allValues)).ToArray()),
				_ => source
			};
		}

		public static BehaviourInfo SubstituteBehaviour(
			BehaviourInfo info,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues)
		{
			var listeners = SubstituteListeners(info.Listeners, parameters);
			return info.SubstituteParameters(listeners, parameters, allValues);
		}

		private static IReadOnlyList<ListenerInfo> SubstituteListeners(
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, object> parameters)
		{
			if (listeners.Count == 0)
			{
				return listeners;
			}

			var result = new ListenerInfo[listeners.Count];
			
			for (var i = 0; i < listeners.Count; i++)
			{
				var l = listeners[i];

				if (!l.BehaviourDescriptor.EntityId.StartsWith(Transformer.ParameterEntityIdSentinel))
				{
					result[i] = l;
					continue;
				}

				var paramId = l.BehaviourDescriptor.EntityId.Substring(Transformer.ParameterEntityIdSentinel.Length);

				if (parameters.TryGetValue(paramId, out var raw) && raw is string entityId)
				{
					result[i] = new ListenerInfo(l.BehaviourDescriptor with { EntityId = entityId })
					{
						OutputMapping = l.OutputMapping
					};
				}
				else
				{
					throw new ParsingException($"Listener parameter '{paramId}' is missing or not a string");
				}
			}

			return result;
		}
	}
}
