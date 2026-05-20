using System.Collections.Generic;
using System.Linq;
using Assembler.Extensions;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Parsing
{
	public static class TemplateInstantiator
	{
		public static ConcreteEntityInfo Instantiate(EntityInfo template,
			string entityId,
			IReadOnlyList<ValueInfo> allValues,
			ValueSource<Vector3> position,
			ValueSource<Vector3> rotation,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IEnumerable<string>? additionalTags = null,
			IEnumerable<BehaviourInfo>? additionalBehaviours = null,
			IReadOnlyList<ValueInfo>? additionalVariables = null)
		{
			var augmentedParameters = new Dictionary<string, AssemblerValue>(parameters.EmptyIfNull())
			{
				["self_id"] = new StringValue(entityId)
			};

			var inheritedBehaviours = template.Behaviours.Select(b => SubstituteBehaviour(b, augmentedParameters, allValues));

			var behaviours = inheritedBehaviours.Concat(additionalBehaviours.EmptyIfNull()).ToArray();

			var tags = template.Tags.Concat(additionalTags.EmptyIfNull()).ToArray();

			var resolvedPosition = position is not None<Vector3>
				? position
				: template.InitialPosition.SubstituteParameters(augmentedParameters, allValues);

			var resolvedRotation = rotation is not None<Vector3>
				? rotation
				: template.InitialRotation.SubstituteParameters(augmentedParameters, allValues);

			var inheritedVariables = template.Variables.Select(v => new ValueInfo(v.Id,
				FlattenAssemblerValue(Transformer.SubstituteAssemblerValue(v.Value, augmentedParameters), allValues)));

			var flattenedAdditional = additionalVariables
				.EmptyIfNull()
				.Select(v => new ValueInfo(v.Id, FlattenAssemblerValue(v.Value, allValues)));

			var variables = inheritedVariables
				.Concat(flattenedAdditional)
				.ToArray();

			return new ConcreteEntityInfo(
				entityId,
				tags,
				resolvedPosition,
				resolvedRotation,
				behaviours,
				variables);
		}

		private static AssemblerValue FlattenAssemblerValue(AssemblerValue value, IReadOnlyList<ValueInfo> allValues) =>
			value switch
			{
				VecValue vec => new Vector3Value(vec.ToVector3(allValues)),
				ColourValue col => new ColorValue(col.ToColor(allValues)),
				_ => value
			};

		public static ValueSource<T> SubstituteParameters<T>(this ValueSource<T> source,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues) =>
			source switch
			{
				ParameterSource<T> p => !parameters.TryGetValue(p.ParameterId, out var raw)
					? throw new ParsingException($"Parameter '{p.ParameterId}' not supplied during template instantiation")
					: Transformer.CreateValueSource<T>(allValues, raw, parameters: parameters),
				ExpressionSource<T> e => new ExpressionSource<T>(e.ExpressionId,
					e.Arguments.Select(a => a.SubstituteParameters(parameters, allValues)).ToArray()),
				_ => source
			};

		private static BehaviourInfo SubstituteBehaviour(
			BehaviourInfo info,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IReadOnlyList<ValueInfo> allValues)
		{
			var listeners = SubstituteListeners(info.Listeners, parameters);
			var substituted = info.SubstituteParameters(listeners, parameters, allValues);

			return substituted with
			{
				Tags = info.Tags
			};
		}

		private static IReadOnlyList<ListenerInfo> SubstituteListeners(
			IReadOnlyList<ListenerInfo> listeners,
			IReadOnlyDictionary<string, AssemblerValue> parameters)
		{
			if (listeners.Count == 0)
			{
				return listeners;
			}

			var result = new ListenerInfo[listeners.Count];

			for (var i = 0; i < listeners.Count; i++)
			{
				var l = listeners[i];

				if (l is not DirectListenerInfo direct)
				{
					result[i] = l;
					continue;
				}

				if (!direct.BehaviourDescriptor.EntityId.StartsWith(Transformer.ParameterEntityIdSentinel))
				{
					result[i] = direct;
					continue;
				}

				var paramId = direct.BehaviourDescriptor.EntityId[Transformer.ParameterEntityIdSentinel.Length..];

				if (parameters.TryGetValue(paramId, out var raw) && raw is StringValue sv)
				{
					result[i] = new DirectListenerInfo(direct.BehaviourDescriptor with
					{
						EntityId = sv.Value
					})
					{
						OutputMapping = direct.OutputMapping
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