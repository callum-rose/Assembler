using System.Collections.Generic;
using System.Linq;
using Assembler.Extensions;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Parsing
{
	public static class TemplateInstantiator
	{
		// public static Dictionary<string, object> CreateParameters(string entityId,
		// 	Dictionary<string, object>? templateParameters = null)
		// {
		// 	var parameters = templateParameters ?? new Dictionary<string, object>();
		// 	parameters["self_id"] = entityId;
		// 	return parameters;
		// }

		public static ConcreteEntityInfo Instantiate(EntityInfo template,
			string entityId,
			IReadOnlyList<ValueInfo> allValues,
			ValueSource<Vector3>? position = null,
			ValueSource<Vector3>? rotation = null,
			IReadOnlyDictionary<string, object>? parameters = null,
			IEnumerable<string>? additionalTags = null,
			IEnumerable<BehaviourInfo>? additionalBehaviours = null)
		{
			var augmentedParameters = new Dictionary<string, object>(parameters.EmptyIfNull())
			{
				["self_id"] = entityId
			};

			var inheritedBehaviours = template.Behaviours.Select(b => SubstituteBehaviour(b, augmentedParameters, allValues));

			var behaviours = inheritedBehaviours.Concat(additionalBehaviours.EmptyIfNull()).ToArray();

			var tags = template.Tags.Concat(additionalTags.EmptyIfNull()).ToArray();

			var resolvedPosition = position ?? template.InitialPosition.SubstituteParameters(augmentedParameters, allValues);
			var resolvedRotation = rotation ?? template.InitialRotation.SubstituteParameters(augmentedParameters, allValues);

			return new ConcreteEntityInfo(
				entityId,
				tags,
				resolvedPosition,
				resolvedRotation,
				behaviours);
		}

		public static ValueSource<T> SubstituteParameters<T>(this ValueSource<T> source,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues)
		{
			return source switch
			{
				ParameterSource<T> p => !parameters.TryGetValue(p.ParameterId, out var raw)
					? throw new ParsingException($"Parameter '{p.ParameterId}' not supplied during template instantiation")
					: Transformer.CreateValueSource<T>(allValues, raw, parameters: parameters),
				ExpressionSource<T> e => new ExpressionSource<T>(e.ExpressionId,
					e.Arguments.Select(a => a.SubstituteParameters(parameters, allValues)).ToArray()),
				_ => source
			};
		}

		private static BehaviourInfo SubstituteBehaviour(
			BehaviourInfo info,
			IReadOnlyDictionary<string, object> parameters,
			IReadOnlyList<ValueInfo> allValues)
		{
			var listeners = SubstituteListeners(info.Listeners, parameters);
			var substituted = info.SubstituteParameters(listeners, parameters, allValues);
			return substituted with { Tags = info.Tags };
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

				var paramId = l.BehaviourDescriptor.EntityId[Transformer.ParameterEntityIdSentinel.Length..];

				if (parameters.TryGetValue(paramId, out var raw) && raw is string entityId)
				{
					result[i] = new ListenerInfo(l.BehaviourDescriptor with { EntityId = entityId })
					{
						OutputMapping = l.OutputMapping,
						EntityTag = l.EntityTag,
						BehaviourTag = l.BehaviourTag
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
