using System;
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
			TransformContext ctx,
			ValueSource<Vector3> position,
			ValueSource<Vector3> rotation,
			IReadOnlyDictionary<string, AssemblerValue> parameters,
			IEnumerable<string>? additionalTags = null,
			IEnumerable<BehaviourInfo>? additionalBehaviours = null,
			IReadOnlyList<ValueInfo>? additionalVariables = null,
			IReadOnlyList<ChildEntityInfo>? additionalChildren = null,
			IReadOnlyDictionary<string, object>? runtimeParameters = null)
		{
			var augmentedParameters = new Dictionary<string, AssemblerValue>(parameters.EmptyIfNull())
			{
				["self_id"] = new StringValue(entityId)
			};

			if (runtimeParameters != null)
			{
				foreach (var kvp in runtimeParameters)
				{
					augmentedParameters[kvp.Key] = AdaptRuntimeParameter(kvp.Key, kvp.Value);
				}
			}

			var instanceCtx = ctx.WithParameters(augmentedParameters);

			var inheritedBehaviours = template.Behaviours.Select(b => SubstituteBehaviour(b, instanceCtx));

			var behaviours = inheritedBehaviours.Concat(additionalBehaviours.EmptyIfNull()).ToArray();

			// Tags override (not merge): an entity that declares its own Tags replaces the
			// template's, mirroring how an explicit Position overrides the template's. An entity
			// with no Tags of its own inherits the template's.
			var ownTags = additionalTags.EmptyIfNull().ToArray();
			var tags = ownTags.Length > 0 ? ownTags : template.Tags.ToArray();

			var resolvedPosition = position is not None<Vector3>
				? position
				: template.InitialPosition.SubstituteParameters(instanceCtx);

			var resolvedRotation = rotation is not None<Vector3>
				? rotation
				: template.InitialRotation.SubstituteParameters(instanceCtx);

			var inheritedVariables = template.Variables.Select(v => new ValueInfo(v.Id,
				FlattenAssemblerValue(Transformer.SubstituteAssemblerValue(v.Value, instanceCtx.Parameters), instanceCtx.Values)));

			var flattenedAdditional = additionalVariables
				.EmptyIfNull()
				.Select(v => new ValueInfo(v.Id, FlattenAssemblerValue(v.Value, instanceCtx.Values)));

			var variables = inheritedVariables
				.Concat(flattenedAdditional)
				.ToArray();

			var inheritedChildren = template.Children
				.Select(c => SubstituteChild(c, instanceCtx));

			var addedChildren = (additionalChildren ?? (IEnumerable<ChildEntityInfo>)Array.Empty<ChildEntityInfo>())
				.Select(c => SubstituteChild(c, instanceCtx));

			var children = inheritedChildren.Concat(addedChildren).ToArray();

			return new ConcreteEntityInfo(
				entityId,
				tags,
				resolvedPosition,
				resolvedRotation,
				behaviours,
				variables,
				children);
		}

		private static ChildEntityInfo SubstituteChild(ChildEntityInfo child, TransformContext ctx)
		{
			var substitutedParameters = new Dictionary<string, AssemblerValue>(child.Parameters.Count);

			foreach (var kvp in child.Parameters)
			{
				substitutedParameters[kvp.Key] = Transformer.SubstituteAssemblerValue(kvp.Value, ctx.Parameters);
			}

			var substitutedBehaviours = child.Behaviours
				.Select(b => SubstituteBehaviour(b, ctx))
				.ToArray();

			var substitutedPosition = child.InitialPosition.SubstituteParameters(ctx);
			var substitutedRotation = child.InitialRotation.SubstituteParameters(ctx);

			var substitutedVariables = child.Variables
				.Select(v => new ValueInfo(v.Id,
					FlattenAssemblerValue(Transformer.SubstituteAssemblerValue(v.Value, ctx.Parameters), ctx.Values)))
				.ToArray();

			var substitutedChildren = child.Children
				.Select(c => SubstituteChild(c, ctx))
				.ToArray();

			return child with
			{
				Parameters = substitutedParameters,
				Behaviours = substitutedBehaviours,
				InitialPosition = substitutedPosition,
				InitialRotation = substitutedRotation,
				Variables = substitutedVariables,
				Children = substitutedChildren
			};
		}

		// Flattens a variable initialiser into a concrete AssemblerValue that
		// VariableRegistry.BuildProvider can turn into a constant provider.
		// ParamRefs are already substituted before we get here; the other reference
		// kinds (ExprRef/AssetRef/EntityPropertyRef/ClockRef/OutputRef) resolve at runtime and
		// cannot be reduced to a constant, so only VarRef is dereferenced here.
		private static AssemblerValue FlattenAssemblerValue(AssemblerValue value, IReadOnlyList<ValueInfo> allValues) =>
			value switch
			{
				VecValue vec => new Vector3Value(vec.ToVector3(allValues)),
				ColourValue col => new ColorValue(col.ToColor(allValues)),
				VarRef varRef => FlattenAssemblerValue(allValues.ResolveValue(varRef.Id), allValues),
				_ => value
			};

		// Adapts a value resolved at runtime (e.g. by an expression in a spawner's Parameters)
		// into the already-flattened AssemblerValue subtype the rest of the instantiation
		// machinery expects. Runtime types (Vector3/Vector2/Color) are accepted here; the IR and
		// resolved stages reject them. Thin wrapper over the unified converter.
		private static AssemblerValue AdaptRuntimeParameter(string key, object? value) =>
			AssemblerValueConverter.ToAssemblerValue(value, new ValueConversion { AllowRuntimeTypes = true, Name = key });

		private static BehaviourInfo SubstituteBehaviour(BehaviourInfo info, TransformContext ctx)
		{
			var listeners = SubstituteListeners(info.Listeners, ctx.Parameters);
			var substituted = info.SubstituteParameters(listeners, ctx);

			return substituted with
			{
				Tags = info.Tags
			};
		}

		internal static IReadOnlyList<ListenerInfo> SubstituteListeners(
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

				if (!direct.BehaviourDescriptor.EntityId.StartsWith(ListenerParsing.ParameterEntityIdSentinel))
				{
					result[i] = direct;
					continue;
				}

				var paramId = direct.BehaviourDescriptor.EntityId[ListenerParsing.ParameterEntityIdSentinel.Length..];

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
