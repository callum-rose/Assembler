using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Assembler.Deserialisation.Dtos;
using Assembler.Extensions;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Parsing
{
	// Orchestrates the DTO -> Info transform: walks the GameDto, builds the constant/variable, expression,
	// template and entity Info, and constructs entities/children. The reusable conversion machinery lives
	// in focused helpers: AssemblerValueConverter (object -> AssemblerValue IR), ValueSourceFactory
	// (AssemblerValue -> ValueSource), ExpressionSynthesis (!expr call sites) and ListenerParsing.
	public static class Transformer
	{
		public static GameInfo Transform(GameDto gameDto)
		{
			var info = new AboutInfo(gameDto.Game?.Title ?? string.Empty, gameDto.Game?.Description ?? string.Empty);

			var world = new WorldInfo(gameDto.World?.Dimensionality ?? 0,
				gameDto.World?.BackgroundColor?.ToColor(Array.Empty<ValueInfo>()) ?? Color.black);

			var physics =
				new PhysicsInfo(gameDto.Physics?.Gravity?.ToVector3(Array.Empty<ValueInfo>()) ?? new Vector3(0, 0, 0));

			var assets = gameDto.Assets.EmptyIfNull().Select(a => a.Type switch
			{
				"sprite" => (AssetInfo)new SpriteAssetInfo(a.Id ?? string.Empty,
					a.Source ?? "resources",
					a.Path ?? string.Empty),
				"audioclip" => new AudioClipAssetInfo(a.Id ?? string.Empty, a.Source ?? "resources", a.Path ?? string.Empty),
				"mesh" => new MeshAssetInfo(a.Id ?? string.Empty, a.Source ?? "resources", a.Path ?? string.Empty),
				_ => throw new NotImplementedException($"Unknown asset type: {a.Type}")
			}).ToList();

			var localisation = CreateLocalisationInfo(gameDto.Localisation);

			var values = new List<ValueInfo>((gameDto.Constants?.Count ?? 0) + (gameDto.Variables?.Count ?? 0));

			var allValues = (gameDto.Constants ?? new Dictionary<string, object>())
				.Concat(gameDto.Variables ?? new Dictionary<string, object>());

			foreach (var kvp in allValues)
			{
				values.Add(new ValueInfo(kvp.Key, AssemblerValueConverter.Convert(values, kvp.Value, kvp.Key)));
			}

			var expressions = gameDto.Expressions.EmptyIfNull().Select(CreateExpressionInfo).ToArray();

			var ctx = new TransformContext(
				values,
				new Dictionary<string, AssemblerValue>(),
				expressions.ToDictionary(e => e.Id),
				BuiltInTypeRegistry.Default,
				new Dictionary<Type, MethodInfo>(),
				new InlineExpressionAccumulator());

			var templates = gameDto.Templates?
				.Select(kvp => new ConcreteEntityInfo(
					kvp.Key,
					kvp.Value.Tags ?? new List<string>(),
					ValueSourceFactory.CreateValueSource<Vector3>(ctx, AssemblerValueConverter.ToAssemblerValue(kvp.Value.Position)),
					ValueSourceFactory.CreateValueSource<Vector3>(ctx, AssemblerValueConverter.ToAssemblerValue(kvp.Value.Rotation)),
					(kvp.Value.Behaviours ?? new Dictionary<string, BehaviourDto>())
					.Select(b => CreateBehaviour(ctx, b.Key, b.Value))
					.ToArray(),
					CreateEntityVariables(kvp.Value.Variables),
					BuildChildren(ctx, kvp.Value.Children)))
				.ToArray() ?? Array.Empty<ConcreteEntityInfo>();

			var entities = (gameDto.Entities ?? new Dictionary<string, EntityDto>())
				.Select(kvp => CreateEntityInfo(kvp.Key, kvp.Value)).ToArray();

			// Inline `!expr { Do: '<C# body>' }` call sites synthesised anonymous expressions onto the
			// context as they were transformed above. Append them so the builder compiles and registers
			// them alongside the declared ones.
			var allExpressions = ctx.InlineExpressions.Count > 0
				? expressions.Concat(ctx.InlineExpressions.Expressions).ToArray()
				: expressions;

			return new GameInfo(info,
				world,
				physics,
				assets,
				localisation,
				values,
				allExpressions,
				templates,
				entities)
			{ ParseContext = ctx };

			ExpressionInfo CreateExpressionInfo(KeyValuePair<string, ExpressionDto> kvp) =>
				new(kvp.Key,
					kvp.Value.ArgumentTypes.EmptyIfNull()
						.Zip(kvp.Value.ArgumentNames.EmptyIfNull(), (type, name) => (type, name)).ToArray(),
					kvp.Value.ReturnType ?? string.Empty,
					kvp.Value.RegisterTypes ?? Array.Empty<string>(),
					kvp.Value.RegisterTypeStatics ?? Array.Empty<string>(),
					kvp.Value.Expression ?? string.Empty,
					kvp.Value.CallableAs);

			ConcreteEntityInfo CreateEntityInfo(string entityId, EntityDto entityDto)
			{
				EntityInfo template;
				Dictionary<string, AssemblerValue> parameters;

				if (entityDto.Template is null)
				{
					template = NullEntityInfo.Instance;
					parameters = new Dictionary<string, AssemblerValue>();
				}
				else
				{
					template = templates.First(t => t.Id == entityDto.Template.Id);
					parameters = AssemblerValueConverter.ConvertProps(entityDto.Template.Parameters);
				}

				var entityCtx = ctx.WithParameters(parameters);

				var ownBehaviours = (entityDto.Behaviours ?? new Dictionary<string, BehaviourDto>())
					.Select(b => CreateBehaviour(entityCtx, b.Key, b.Value));

				var children = BuildChildren(ctx, entityDto.Children);

				return TemplateInstantiator.Instantiate(template,
					entityId,
					ctx,
					ValueSourceFactory.CreateValueSource<Vector3>(entityCtx, AssemblerValueConverter.ToAssemblerValue(entityDto.Position)),
					ValueSourceFactory.CreateValueSource<Vector3>(entityCtx, AssemblerValueConverter.ToAssemblerValue(entityDto.Rotation)),
					parameters,
					entityDto.Tags,
					ownBehaviours,
					CreateEntityVariables(entityDto.Variables),
					additionalChildren: children);
			}
		}

		// Children are a keyed mapping (id -> child), matching how top-level Entities are keyed, so the
		// key is the child's relative id. Mapping order is preserved, which keeps sibling order stable.
		private static IReadOnlyList<ChildEntityInfo> BuildChildren(TransformContext ctx,
			Dictionary<string, EntityDto>? children)
		{
			if (children == null || children.Count == 0)
			{
				return Array.Empty<ChildEntityInfo>();
			}

			return children.Select(kvp => BuildChild(ctx, kvp.Key, kvp.Value)).ToArray();
		}

		private static ChildEntityInfo BuildChild(TransformContext ctx, string idSuffix, EntityDto dto)
		{
			var templateRefId = dto.Template?.Id;

			var ownParams = AssemblerValueConverter.ConvertProps(dto.Template?.Parameters);
			var childCtx = ctx.WithParameters(ownParams);

			var ownBehaviours = (dto.Behaviours ?? new Dictionary<string, BehaviourDto>())
				.Select(b => CreateBehaviour(childCtx, b.Key, b.Value))
				.ToArray();

			var position = ValueSourceFactory.CreateValueSource<Vector3>(childCtx, AssemblerValueConverter.ToAssemblerValue(dto.Position));
			var rotation = ValueSourceFactory.CreateValueSource<Vector3>(childCtx, AssemblerValueConverter.ToAssemblerValue(dto.Rotation));

			var nestedChildren = BuildChildren(childCtx, dto.Children);

			return new ChildEntityInfo(
				idSuffix,
				templateRefId,
				ownParams,
				dto.Tags?.ToArray() ?? Array.Empty<string>(),
				position,
				rotation,
				ownBehaviours,
				CreateEntityVariables(dto.Variables),
				nestedChildren);
		}

		private static IReadOnlyList<ValueInfo> CreateEntityVariables(IReadOnlyDictionary<string, object>? variables)
		{
			if (variables == null || variables.Count == 0)
			{
				return Array.Empty<ValueInfo>();
			}

			var result = new ValueInfo[variables.Count];
			var i = 0;

			foreach (var kvp in variables)
			{
				result[i++] = new ValueInfo(kvp.Key, AssemblerValueConverter.ToAssemblerValue(kvp.Value));
			}

			return result;
		}

		internal static AssemblerValue SubstituteAssemblerValue(AssemblerValue value,
			IReadOnlyDictionary<string, AssemblerValue> parameters)
		{
			return value switch
			{
				ParamRef paramRef => parameters.TryGetValue(paramRef.Id, out var resolved)
					? resolved
					: throw new ParsingException(
						$"Parameter '{paramRef.Id}' not supplied during template instantiation"),
				VecValue vec => new VecValue(
					SubstituteAssemblerValue(vec.X, parameters),
					SubstituteAssemblerValue(vec.Y, parameters),
					SubstituteAssemblerValue(vec.Z, parameters)),
				ColourValue col => new ColourValue(
					SubstituteAssemblerValue(col.R, parameters),
					SubstituteAssemblerValue(col.G, parameters),
					SubstituteAssemblerValue(col.B, parameters),
					SubstituteAssemblerValue(col.A, parameters),
					col.Raw),
				ExprRef exprRef => exprRef with
				{
					With = exprRef.With.Select(a => SubstituteAssemblerValue(a, parameters)).ToArray()
				},
				_ => value
			};
		}

		private static BehaviourInfo CreateBehaviour(TransformContext ctx,
			string id,
			BehaviourDto behaviourDto)
		{
			var type = behaviourDto.Type ?? string.Empty;

			if (!BehaviourRegistry.All.TryGetValue(type, out var factory))
			{
				throw new ParsingException($"Cannot convert behaviour type '{type}'");
			}

			var props = AssemblerValueConverter.ConvertProps(behaviourDto.Properties);

			var info = factory(id,
				ListenerParsing.GetListeners(ctx, behaviourDto),
				props,
				ctx);

			return behaviourDto.Tags is { Count: > 0 }
				? info with { Tags = behaviourDto.Tags.ToArray() }
				: info;
		}

		private static LocalisationInfo CreateLocalisationInfo(LocalisationDto? dto)
		{
			if (dto?.Locales == null || dto.Locales.Count == 0)
			{
				return LocalisationInfo.Empty;
			}

			var locales = new Dictionary<string, IReadOnlyDictionary<string, string>>(dto.Locales.Count);

			foreach (var kvp in dto.Locales)
			{
				locales[kvp.Key] = new Dictionary<string, string>(kvp.Value);
			}

			return new LocalisationInfo(dto.DefaultLocale ?? string.Empty, locales);
		}
	}
}
