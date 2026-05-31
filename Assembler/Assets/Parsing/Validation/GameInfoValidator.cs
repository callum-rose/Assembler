using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assembler.Parsing.Info;

namespace Assembler.Parsing.Validation
{
	/// <summary>
	/// Validates a transformed <see cref="GameInfo"/> before it proceeds to resolving/building.
	/// Walks the whole tree in a single pass, accumulating every problem into a
	/// <see cref="ValidationResult"/> rather than throwing on the first one. Tree-wide rules
	/// (uniqueness, reference resolution, required scalars) live here; behaviour-specific rules are
	/// dispatched to <see cref="BehaviourValidatorRegistry"/>.
	/// </summary>
	public static class GameInfoValidator
	{
		private readonly static IReadOnlyCollection<string> KnownAssetSources = new[] { "resources" };

		public static ValidationResult Validate(GameInfo game)
		{
			var ctx = new ValidationContext();

			var variableIds = CollectVariableIds(game);
			var assetIds = new HashSet<string>(game.Assets.Select(a => a.Id));
			var expressionsById = game.Expressions
				.GroupBy(e => e.Id)
				.ToDictionary(g => g.Key, g => g.First());
			var templateIds = new HashSet<string>(game.Templates.Select(t => t.Id));
			var references = new ReferenceScope(variableIds, assetIds, expressionsById);

			ValidateWorld(game.World, ctx);
			ValidateAssets(game.Assets, ctx);
			ValidateValueAndExpressionIds(game, ctx);
			ValidateTemplates(game.Templates, ctx);
			ValidateEntities(game.Entities, templateIds, references, ctx);

			return new ValidationResult(ctx.Errors);
		}

		// A variable reference resolves at runtime from the entity-local scope or the global registry.
		// Collecting every declared id (global + all entity/child locals) keeps the reference check from
		// false-flagging legitimate local variables; a name absent from all of them is a genuine typo.
		private static HashSet<string> CollectVariableIds(GameInfo game)
		{
			var ids = new HashSet<string>(game.Variables.Select(v => v.Id));

			foreach (var template in game.Templates)
			{
				CollectEntityVariableIds(template, ids);
			}

			foreach (var entity in game.Entities)
			{
				CollectEntityVariableIds(entity, ids);
			}

			return ids;
		}

		private static void CollectEntityVariableIds(EntityInfo entity, HashSet<string> ids)
		{
			foreach (var variable in entity.Variables)
			{
				ids.Add(variable.Id);
			}

			foreach (var child in entity.Children)
			{
				CollectChildVariableIds(child, ids);
			}
		}

		private static void CollectChildVariableIds(ChildEntityInfo child, HashSet<string> ids)
		{
			foreach (var variable in child.Variables)
			{
				ids.Add(variable.Id);
			}

			foreach (var nested in child.Children)
			{
				CollectChildVariableIds(nested, ids);
			}
		}

		private static void ValidateWorld(WorldInfo world, ValidationContext ctx)
		{
			using (ctx.Scope("world"))
			{
				if (world.Dimensionality is not (2 or 3))
				{
					ctx.Error($"'Dimensionality' is {world.Dimensionality}, which is invalid.",
						"Set World.Dimensionality to 2 or 3.");
				}
			}
		}

		private static void ValidateAssets(IReadOnlyList<AssetInfo> assets, ValidationContext ctx)
		{
			foreach (var asset in assets)
			{
				using (ctx.Scope($"assets/{asset.Id}"))
				{
					ctx.RequireNonEmpty(asset.Id, "Id",
						"Give the asset a non-empty id (the mapping key under Assets).");
					ctx.RequireNonEmpty(asset.Path, "Path",
						"Set 'Path' to the resource path the asset loads from.");
					ctx.RequireOneOf(asset.Source, "Source", KnownAssetSources,
						"Set 'Source' to a supported loader, e.g. resources.");
				}
			}

			using (ctx.Scope("assets"))
			{
				ctx.RequireUnique(assets.Select(a => a.Id), "asset",
					"Rename one of the assets so each has a unique id.");
			}
		}

		private static void ValidateValueAndExpressionIds(GameInfo game, ValidationContext ctx)
		{
			using (ctx.Scope("variables"))
			{
				ctx.RequireUnique(game.Variables.Select(v => v.Id), "variable/constant",
					"Rename one so each variable/constant has a unique id.");
			}

			using (ctx.Scope("expressions"))
			{
				ctx.RequireUnique(game.Expressions.Select(e => e.Id), "expression",
					"Rename one so each expression has a unique id.");
			}
		}

		private static void ValidateTemplates(IReadOnlyList<EntityInfo> templates, ValidationContext ctx)
		{
			// Templates are uninstantiated: their values may be supplied by parameters at instantiation,
			// so required-value and reference checks run on entities (below), not here.
			foreach (var template in templates)
			{
				using (ctx.Scope($"templates/{template.Id}"))
				{
					ctx.RequireNonEmpty(template.Id, "Id",
						"Give the template a non-empty id (the mapping key under Templates).");
				}
			}

			using (ctx.Scope("templates"))
			{
				ctx.RequireUnique(templates.Select(t => t.Id), "template",
					"Rename one so each template has a unique id.");
			}
		}

		private static void ValidateEntities(IReadOnlyList<ConcreteEntityInfo> entities,
			ISet<string> templateIds,
			ReferenceScope references,
			ValidationContext ctx)
		{
			var allEntityIds = new List<string>();

			foreach (var entity in entities)
			{
				using (ctx.Scope($"entities/{entity.Id}"))
				{
					allEntityIds.Add(entity.Id);

					ctx.RequireNonEmpty(entity.Id, "Id",
						"Give the entity a non-empty id (the mapping key under Entities).");

					CheckSource(entity.InitialPosition, "Position", references, ctx);
					CheckSource(entity.InitialRotation, "Rotation", references, ctx);

					ValidateBehaviours(entity.Behaviours, references, ctx);
					ValidateChildren(entity.Children, templateIds, references, ctx, allEntityIds);
				}
			}

			using (ctx.Scope("entities"))
			{
				ctx.RequireUnique(allEntityIds, "entity",
					"Rename one so each entity (including children) has a unique id.");
			}
		}

		private static void ValidateChildren(IReadOnlyList<ChildEntityInfo> children,
			ISet<string> templateIds,
			ReferenceScope references,
			ValidationContext ctx,
			List<string> allEntityIds)
		{
			foreach (var child in children)
			{
				using (ctx.Scope($"children/{child.AbsoluteId ?? child.IdSuffix}"))
				{
					// Only explicit absolute ids must be globally unique; positional suffixes
					// (e.g. "child_0") are composed with the parent id at build time.
					if (child.AbsoluteId != null)
					{
						allEntityIds.Add(child.AbsoluteId);
					}

					if (child.TemplateRefId != null && !templateIds.Contains(child.TemplateRefId))
					{
						ctx.Error($"references template '{child.TemplateRefId}', which is not defined.",
							"Define the template under Templates, or correct the template id.");
					}

					CheckSource(child.InitialPosition, "Position", references, ctx);
					CheckSource(child.InitialRotation, "Rotation", references, ctx);

					ValidateBehaviours(child.Behaviours, references, ctx);
					ValidateChildren(child.Children, templateIds, references, ctx, allEntityIds);
				}
			}
		}

		private static void ValidateBehaviours(IReadOnlyList<BehaviourInfo> behaviours,
			ReferenceScope references,
			ValidationContext ctx)
		{
			foreach (var behaviour in behaviours)
			{
				using (ctx.Scope($"behaviours/{behaviour.Id}"))
				{
					ctx.RequireNonEmpty(behaviour.Id, "Id",
						"Give the behaviour a non-empty id (the mapping key under Behaviours).");

					foreach (var (name, source) in EnumerateSources(behaviour))
					{
						CheckSource(source, name, references, ctx);
					}

					ValidateListeners(behaviour.Listeners, ctx);

					if (BehaviourValidatorRegistry.All.TryGetValue(behaviour.GetType(), out var validator))
					{
						validator.Validate(behaviour, ctx);
					}
				}
			}
		}

		private static void ValidateListeners(IReadOnlyList<ListenerInfo> listeners, ValidationContext ctx)
		{
			foreach (var listener in listeners)
			{
				switch (listener)
				{
					case EntityTaggedListenerInfo tagged:
						ctx.RequireValue(tagged.EntityTag, "listener EntityTag",
							"Set the listener's EntityTag to the tag to match.");
						break;
					case BehaviourTaggedListenerInfo tagged:
						ctx.RequireValue(tagged.BehaviourTag, "listener BehaviourTag",
							"Set the listener's BehaviourTag to the tag to match.");
						break;
				}
			}
		}

		// ---- Value-source reference resolution (reflection-based) ----

		/// <summary>Enumerates the <see cref="IValueSourceArg"/> properties a behaviour exposes.</summary>
		private static IEnumerable<(string name, IValueSourceArg source)> EnumerateSources(BehaviourInfo behaviour)
		{
			foreach (var prop in behaviour.GetType().GetProperties())
			{
				if (prop.GetIndexParameters().Length > 0)
				{
					continue;
				}

				var value = prop.GetValue(behaviour);

				switch (value)
				{
					case IValueSourceArg source:
						yield return (prop.Name, source);
						break;
					case IEnumerable enumerable when value is not string:
						foreach (var element in enumerable)
						{
							if (Unwrap(element) is IValueSourceArg nested)
							{
								yield return (prop.Name, nested);
							}
						}

						break;
				}
			}
		}

		// Dictionary entries surface as KeyValuePair<,>; pull the value out for inspection.
		private static object? Unwrap(object? element)
		{
			if (element is null)
			{
				return null;
			}

			var type = element.GetType();

			if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
			{
				return type.GetProperty("Value")?.GetValue(element);
			}

			return element;
		}

		private static void CheckSource(IValueSourceArg source,
			string property,
			ReferenceScope references,
			ValidationContext ctx)
		{
			var type = source.GetType();

			if (!type.IsGenericType)
			{
				return;
			}

			var definition = type.GetGenericTypeDefinition();

			if (definition == typeof(ValueReferenceSource<>))
			{
				var id = type.GetProperty("VariableId")?.GetValue(source) as string ?? string.Empty;

				if (!references.VariableIds.Contains(id))
				{
					ctx.Error($"'{property}' references variable '{id}', which is not defined.",
						"Define the variable/constant under Variables or Constants, or correct the reference id.");
				}
			}
			else if (definition == typeof(AssetSource<>))
			{
				var id = type.GetProperty("AssetId")?.GetValue(source) as string ?? string.Empty;

				if (!references.AssetIds.Contains(id))
				{
					ctx.Error($"'{property}' references asset '{id}', which is not defined.",
						"Define the asset under Assets, or correct the asset id.");
				}
			}
			else if (definition == typeof(ExpressionSource<>))
			{
				var id = type.GetProperty("ExpressionId")?.GetValue(source) as string ?? string.Empty;

				if (!references.ExpressionsById.TryGetValue(id, out var expression))
				{
					ctx.Error($"'{property}' references expression '{id}', which is not defined.",
						"Define the expression under Expressions, or correct the expression id.");
				}
				else if (type.GetProperty("Arguments")?.GetValue(source) is IEnumerable<IValueSourceArg> args)
				{
					var argList = args.ToList();

					if (argList.Count != expression.Arguments.Count)
					{
						ctx.Error(
							$"'{property}' calls expression '{id}' with {argList.Count} argument(s) " +
							$"but it expects {expression.Arguments.Count}.",
							"Supply the exact number of arguments the expression declares.");
					}

					foreach (var arg in argList)
					{
						CheckSource(arg, $"{property} (expression arg)", references, ctx);
					}
				}
			}

			// ConstantSource, None, ParameterSource, EntityPositionSource and TriggerOutputSource carry no
			// statically-resolvable reference at this stage and are intentionally left unchecked.
		}

		private sealed record ReferenceScope(
			ISet<string> VariableIds,
			ISet<string> AssetIds,
			IReadOnlyDictionary<string, ExpressionInfo> ExpressionsById);
	}
}
