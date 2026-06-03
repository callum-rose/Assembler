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
	public static class Transformer
	{
		// `CreateValueSourceForArg` is invoked via reflection (one MakeGenericMethod per arg type
		// the parser actually encounters). This is a method handle constant, not mutable state —
		// the per-call cache of closed-generic MethodInfos lives on TransformContext.
		private readonly static MethodInfo CreateValueSourceForArgOpenGeneric =
			typeof(Transformer).GetMethod(nameof(CreateValueSourceForArg),
				BindingFlags.NonPublic | BindingFlags.Static)!;

		private static ValueSource<T> CreateValueSourceForArg<T>(TransformContext ctx, AssemblerValue raw) =>
			raw is NoValue ? None<T>.Instance : CreateValueSource<T>(ctx, raw);

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
				values.Add(new ValueInfo(kvp.Key, Convert(values, kvp.Value, kvp.Key)));
			}

			var expressions = gameDto.Expressions.EmptyIfNull().Select(CreateExpressionInfo).ToArray();

			var ctx = new TransformContext(
				values,
				new Dictionary<string, AssemblerValue>(),
				expressions.ToDictionary(e => e.Id),
				BuiltInTypeRegistry.Default);

			var templates = gameDto.Templates?
				.Select(kvp => new ConcreteEntityInfo(
					kvp.Key,
					kvp.Value.Tags ?? new List<string>(),
					CreateValueSource<Vector3>(ctx, ToAssemblerValue(kvp.Value.Position)),
					CreateValueSource<Vector3>(ctx, ToAssemblerValue(kvp.Value.Rotation)),
					(kvp.Value.Behaviours ?? new Dictionary<string, BehaviourDto>())
					.Select(b => CreateBehaviour(ctx, b.Key, b.Value))
					.ToArray(),
					CreateEntityVariables(kvp.Value.Variables),
					BuildTemplateChildren(ctx, kvp.Value.Children, kvp.Key)))
				.ToArray() ?? Array.Empty<ConcreteEntityInfo>();

			var entities = (gameDto.Entities ?? new Dictionary<string, EntityDto>())
				.Select(kvp => CreateEntityInfo(kvp.Key, kvp.Value)).ToArray();

			var gameOverCondition = CreateValueSource<bool>(ctx, ToAssemblerValue(gameDto.GameOverCondition));

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
				entities,
				gameOverCondition) { ParseContext = ctx };

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
					parameters = ConvertProps(entityDto.Template.Parameters);
				}

				var entityCtx = ctx.WithParameters(parameters);

				var ownBehaviours = (entityDto.Behaviours ?? new Dictionary<string, BehaviourDto>())
					.Select(b => CreateBehaviour(entityCtx, b.Key, b.Value));

				var children = BuildChildren(ctx, entityDto.Children, entityId);

				return TemplateInstantiator.Instantiate(template,
					entityId,
					ctx,
					CreateValueSource<Vector3>(entityCtx, ToAssemblerValue(entityDto.Position)),
					CreateValueSource<Vector3>(entityCtx, ToAssemblerValue(entityDto.Rotation)),
					parameters,
					entityDto.Tags,
					ownBehaviours,
					CreateEntityVariables(entityDto.Variables),
					additionalChildren: children);
			}
		}

		private static IReadOnlyList<ChildEntityInfo> BuildTemplateChildren(TransformContext ctx,
			List<EntityDto>? children,
			string templateId) =>
			BuildChildren(ctx, children, $"template '{templateId}'");

		private static IReadOnlyList<ChildEntityInfo> BuildChildren(TransformContext ctx,
			List<EntityDto>? children,
			string parentDescription)
		{
			if (children == null || children.Count == 0)
			{
				return Array.Empty<ChildEntityInfo>();
			}

			var result = new ChildEntityInfo[children.Count];

			for (var i = 0; i < children.Count; i++)
			{
				result[i] = BuildChild(ctx, children[i], i, parentDescription);
			}

			return result;
		}

		private static ChildEntityInfo BuildChild(TransformContext ctx,
			EntityDto dto,
			int index,
			string parentDescription)
		{
			var templateRefId = dto.Template?.Id;
			var explicitId = dto.Id;

			var idSuffix = explicitId ??
			               (templateRefId != null ? $"{templateRefId}_{index}" : $"child_{index}");

			var ownParams = ConvertProps(dto.Template?.Parameters);
			var childCtx = ctx.WithParameters(ownParams);

			var ownBehaviours = (dto.Behaviours ?? new Dictionary<string, BehaviourDto>())
				.Select(b => CreateBehaviour(childCtx, b.Key, b.Value))
				.ToArray();

			var position = CreateValueSource<Vector3>(childCtx, ToAssemblerValue(dto.Position));
			var rotation = CreateValueSource<Vector3>(childCtx, ToAssemblerValue(dto.Rotation));

			var nestedChildren = BuildChildren(childCtx, dto.Children, explicitId ?? $"{parentDescription}[{index}]");

			return new ChildEntityInfo(
				idSuffix,
				explicitId,
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
				result[i++] = new ValueInfo(kvp.Key, ToAssemblerValue(kvp.Value));
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
				ExprRef exprRef => new ExprRef(exprRef.Do,
					exprRef.With.Select(a => SubstituteAssemblerValue(a, parameters)).ToArray()),
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

			var props = ConvertProps(behaviourDto.Properties);

			var info = factory(id,
				GetListeners(ctx, behaviourDto),
				props,
				ctx);

			return behaviourDto.Tags is { Count: > 0 }
				? info with { Tags = behaviourDto.Tags.ToArray() }
				: info;
		}

		private static IReadOnlyList<ListenerInfo> GetListeners(TransformContext ctx,
			BehaviourDto behaviourDto) =>
			behaviourDto.Listeners
				.EmptyIfNull()
				.Select(l =>
				{
					var outputs = l.Outputs ?? new Dictionary<string, string>();

					if (l is GameOverListenerDto)
					{
						return new GameOverListenerInfo { OutputMapping = outputs };
					}

					if (l is { EntityTag: not null, BehaviourTag: not null })
					{
						throw new ParsingException(
							"A listener cannot declare both EntityTag and BehaviourTag. " +
							"Pick one: EntityTag (+ BehaviourId) targets behaviours on entities with that tag; " +
							"BehaviourTag targets all behaviours carrying that tag.");
					}

					if (l.EntityTag != null)
					{
						var entityTag = CreateValueSource<string>(ctx, ToAssemblerValue(l.EntityTag));

						return new EntityTaggedListenerInfo(entityTag, l.BehaviourId ?? string.Empty) { OutputMapping = outputs };
					}

					if (l.BehaviourTag != null)
					{
						var behaviourTag = CreateValueSource<string>(ctx, ToAssemblerValue(l.BehaviourTag));

						return (ListenerInfo)new BehaviourTaggedListenerInfo(behaviourTag) { OutputMapping = outputs };
					}

					var entityId = l.EntityId switch
					{
						ParamRefDto paramRefDto => ctx.Parameters.TryGetValue(paramRefDto.Id ?? string.Empty, out var pv)
						                           && pv is StringValue sv
							? sv.Value
							: ParameterEntityIdSentinel + (paramRefDto.Id ?? string.Empty),
						VarRefDto varRefDto => varRefDto.ResolveValue<string>(ctx.Values),
						string behaviourId => behaviourId,
						_ => throw new ParsingException($"Cannot get Id for listener {l.EntityId}")
					};

					var behaviourDescriptor = new BehaviourDescriptor(entityId, l.BehaviourId ?? string.Empty);

					return new DirectListenerInfo(behaviourDescriptor) { OutputMapping = outputs };
				})
				.ToArray();

		/// <summary>
		/// Builds listeners authored *inside* a behaviour's properties (e.g. a state machine's per-state
		/// <c>OnEnter</c>/<c>OnExit</c> hooks). Unlike the top-level <c>Listeners:</c> field, these arrive
		/// as already-converted <see cref="AssemblerValue"/>s (<see cref="DictValue"/> entries, or a
		/// <see cref="GameOverMarker"/> for a nested <c>!gameover</c>), so this mirrors <see cref="GetListeners"/>
		/// reading from that shape to produce identical <see cref="ListenerInfo"/> semantics.
		/// </summary>
		internal static IReadOnlyList<ListenerInfo> ParseNestedListeners(TransformContext ctx, AssemblerValue? raw) =>
			raw is ListValue list
				? list.Value.Select(item => ParseNestedListener(ctx, item)).ToArray()
				: Array.Empty<ListenerInfo>();

		private static ListenerInfo ParseNestedListener(TransformContext ctx, AssemblerValue item)
		{
			if (item is GameOverMarker)
			{
				return new GameOverListenerInfo();
			}

			if (item is not DictValue dict)
			{
				throw new ParsingException(
					"OnEnter/OnExit entries must be listener maps (EntityId + BehaviourId, EntityTag, or BehaviourTag) or !gameover.");
			}

			var fields = dict.Value;
			var outputs = ParseNestedOutputMapping(fields.GetValueOrDefault("Outputs"));

			var hasEntityTag = fields.TryGetValue("EntityTag", out var entityTagValue);
			var hasBehaviourTag = fields.TryGetValue("BehaviourTag", out var behaviourTagValue);

			if (hasEntityTag && hasBehaviourTag)
			{
				throw new ParsingException(
					"A listener cannot declare both EntityTag and BehaviourTag. " +
					"Pick one: EntityTag (+ BehaviourId) targets behaviours on entities with that tag; " +
					"BehaviourTag targets all behaviours carrying that tag.");
			}

			var behaviourId = (fields.GetValueOrDefault("BehaviourId") as StringValue)?.Value ?? string.Empty;

			if (hasEntityTag)
			{
				return new EntityTaggedListenerInfo(CreateValueSource<string>(ctx, entityTagValue!), behaviourId)
				{
					OutputMapping = outputs
				};
			}

			if (hasBehaviourTag)
			{
				return new BehaviourTaggedListenerInfo(CreateValueSource<string>(ctx, behaviourTagValue!))
				{
					OutputMapping = outputs
				};
			}

			var entityId = ResolveNestedEntityId(ctx, fields.GetValueOrDefault("EntityId"));

			return new DirectListenerInfo(new BehaviourDescriptor(entityId, behaviourId)) { OutputMapping = outputs };
		}

		private static IReadOnlyDictionary<string, string> ParseNestedOutputMapping(AssemblerValue? value) =>
			value is DictValue dict
				? dict.Value.ToDictionary(kvp => kvp.Key, kvp => (kvp.Value as StringValue)?.Value ?? string.Empty)
				: new Dictionary<string, string>();

		private static string ResolveNestedEntityId(TransformContext ctx, AssemblerValue? value) =>
			value switch
			{
				StringValue s => s.Value,
				ParamRef p => ctx.Parameters.TryGetValue(p.Id, out var pv) && pv is StringValue sv
					? sv.Value
					: ParameterEntityIdSentinel + p.Id,
				VarRef v => ctx.Values.ResolveValue(v.Id) is StringValue sv
					? sv.Value
					: throw new ParsingException($"Listener EntityId variable '{v.Id}' must resolve to a string."),
				null => throw new ParsingException(
					"Listener entry requires an EntityId (with BehaviourId), or an EntityTag / BehaviourTag."),
				_ => throw new ParsingException($"Cannot interpret listener EntityId '{value}'.")
			};

		internal const string ParameterEntityIdSentinel = "@param:";

		internal static IReadOnlyList<string> ConvertStringList(AssemblerValue? value) =>
			value is ListValue list
				? list.Value
					.Select(item => item is StringValue sv ? sv.Value : item?.ToString() ?? string.Empty)
					.ToArray()
				: Array.Empty<string>();

		internal static IReadOnlyList<IValueSourceArg> ConvertArgumentList(TransformContext ctx,
			AssemblerValue? value) =>
			value is ListValue list
				? list.Value.Select(item => (IValueSourceArg)
					CreateValueSource<object>(ctx, item)).ToArray()
				: Array.Empty<IValueSourceArg>();

		private static IReadOnlyList<IValueSourceArg> BuildTextArguments(TransformContext ctx, TextRef textRef)
		{
			if (textRef.Arguments.Count == 0)
			{
				return Array.Empty<IValueSourceArg>();
			}

			var args = new IValueSourceArg[textRef.Arguments.Count];

			for (int i = 0; i < textRef.Arguments.Count; i++)
			{
				// !text placeholders have no declared types — each argument is boxed to object and
				// stringified by string.Format at runtime, so every argument resolves as object.
				args[i] = CreateValueSource<object>(ctx, textRef.Arguments[i]);
			}

			return args;
		}

		/// <summary>
		/// Routes a <c>!expr { Do, With }</c> call site to an <see cref="ExpressionSource{T}"/>.
		/// If <c>Do</c> names a declared expression (by id or <c>CallableAs</c> alias) it's a named
		/// call against that expression's id; otherwise <c>Do</c> is compiled as an anonymous inline
		/// C# body whose params <c>arg0</c>, <c>arg1</c>, … bind positionally to <c>With</c>.
		/// </summary>
		private static ValueSource<T> CreateExpressionSource<T>(TransformContext ctx, ExprRef exprRef)
		{
			if (TryResolveNamedExpression(ctx, exprRef.Do, out var named))
			{
				return new ExpressionSource<T>(named.Id, BuildNamedExpressionArguments(ctx, exprRef, named));
			}

			return CreateInlineExpressionSource<T>(ctx, exprRef);
		}

		// Resolves a `Do` value to a declared expression by its id first, then by CallableAs alias.
		private static bool TryResolveNamedExpression(TransformContext ctx, string @do, out ExpressionInfo info)
		{
			if (ctx.ExpressionsById.TryGetValue(@do, out info!))
			{
				return true;
			}

			foreach (var candidate in ctx.ExpressionsById.Values)
			{
				if (candidate.CallableAlias == @do)
				{
					info = candidate;
					return true;
				}
			}

			info = null!;
			return false;
		}

		private static IReadOnlyList<IValueSourceArg> BuildNamedExpressionArguments(TransformContext ctx,
			ExprRef exprRef,
			ExpressionInfo info)
		{
			if (info.Arguments.Count != exprRef.With.Count)
			{
				throw new ParsingException(
					$"Expression '{exprRef.Do}' expects {info.Arguments.Count} arguments " +
					$"but {exprRef.With.Count} were supplied.");
			}

			if (exprRef.With.Count == 0)
			{
				return Array.Empty<IValueSourceArg>();
			}

			var args = new IValueSourceArg[exprRef.With.Count];

			for (int i = 0; i < exprRef.With.Count; i++)
			{
				var typeName = info.Arguments[i].type;

				if (!ctx.TypeRegistry.TryGetValue(typeName, out var argType))
				{
					throw new ParsingException(
						$"Expression '{exprRef.Do}' argument {i} has unknown type '{typeName}'.");
				}

				args[i] = BuildArg(ctx, argType, exprRef.With[i]);
			}

			return args;
		}

		// Synthesises an anonymous ExpressionInfo for an inline `Do: '<C# body>'` and accumulates it
		// on the context. Operand (arg0, arg1, …) types are inferred from `With`; the return type is
		// the use-site T where mappable, otherwise inferred from the first operand. The body itself is
		// handed verbatim to the compiler, which binds argN to the synthesised params and resolves any
		// other identifier (method, `new`, registered expression) exactly as in any `Expressions:` body.
		private static ValueSource<T> CreateInlineExpressionSource<T>(TransformContext ctx, ExprRef exprRef)
		{
			var argInfos = new (string type, string name)[exprRef.With.Count];
			var argTypes = new Type[exprRef.With.Count];

			for (int i = 0; i < exprRef.With.Count; i++)
			{
				var typeName = InferTypeName(ctx, exprRef.With[i]);

				if (!ctx.TypeRegistry.TryGetValue(typeName, out var argType))
				{
					throw new ParsingException(
						$"Inline expression '{exprRef.Do}' could not resolve a type for argument {i} " +
						$"(inferred '{typeName}').");
				}

				argInfos[i] = (typeName, $"arg{i}");
				argTypes[i] = argType;
			}

			var returnTypeName = InferReturnTypeName<T>(ctx, argInfos);

			var id = ctx.InlineExpressions.Add(generatedId => new ExpressionInfo(
				generatedId,
				argInfos,
				returnTypeName,
				Array.Empty<string>(),
				Array.Empty<string>(),
				WrapInlineBody(exprRef.Do)));

			var args = new IValueSourceArg[exprRef.With.Count];

			for (int i = 0; i < exprRef.With.Count; i++)
			{
				args[i] = BuildArg(ctx, argTypes[i], exprRef.With[i]);
			}

			return new ExpressionSource<T>(id, args);
		}

		// Wraps one `With` operand as a strongly-typed ValueSource<argType>, via the cached
		// closed-generic CreateValueSourceForArg factory.
		private static IValueSourceArg BuildArg(TransformContext ctx, Type argType, AssemblerValue raw)
		{
			if (!ctx.ExprArgFactoryCache.TryGetValue(argType, out var typed))
			{
				typed = CreateValueSourceForArgOpenGeneric.MakeGenericMethod(argType);
				ctx.ExprArgFactoryCache[argType] = typed;
			}

			return (IValueSourceArg)typed.Invoke(null, new object?[] { ctx, raw })!;
		}

		// Best-effort static type-name for an inline operand: literals by kind, `!var` by its
		// resolved value, nested named `!expr` by its declared return type. Anything else (including
		// nested inline `!expr`) falls back to the use-site type, supplied by the caller via the
		// generic CreateValueSource<T> through InferReturnTypeName — here we default to "float".
		private static string InferTypeName(TransformContext ctx, AssemblerValue value) =>
			value switch
			{
				IntValue => "int",
				FloatValue => "float",
				BoolValue => "bool",
				StringValue => "string",
				Vector2Value or Vector3Value or VecValue => "vector",
				ColorValue or ColourValue => "colour",
				TypedListValue typed when TryGetTypeName(ctx,
					typeof(List<>).MakeGenericType(typed.ElementType), out var listName) => listName,
				VarRef varRef => TryResolveValue(ctx, varRef.Id, out var resolved)
					? InferTypeName(ctx, resolved)
					: "float",
				ExprRef nested when TryResolveNamedExpression(ctx, nested.Do, out var info) => info.ReturnType,
				_ => "float"
			};

		// Return type for a synthesised inline expression: the use-site T mapped to a registry name
		// where possible (the common case — Position wants vector, a float property wants float). When
		// T isn't a registered type (e.g. object), fall back to the first operand's inferred type.
		private static string InferReturnTypeName<T>(TransformContext ctx, (string type, string name)[] argInfos)
		{
			if (typeof(T) != typeof(object) && TryGetTypeName(ctx, typeof(T), out var name))
			{
				return name;
			}

			if (argInfos.Length > 0)
			{
				return argInfos[0].type;
			}

			throw new ParsingException(
				$"Cannot infer the return type of an inline !expr used as '{typeof(T).Name}'. " +
				"Use a named expression with a declared ReturnType instead.");
		}

		// Inline bodies are written as terse expressions (`-arg0`, `arg0 * 2`). The compiler parses a
		// method body of statements, so a bare expression needs an explicit `return … ;`. A body that
		// already contains a statement separator (`;`) is treated as hand-written statements and passed
		// through unchanged (it may use `return`, locals, etc., exactly like a declared expression body).
		private static string WrapInlineBody(string body) =>
			body.Contains(';') ? body : $"return {body};";

		private static bool TryGetTypeName(TransformContext ctx, Type type, out string name)
		{
			foreach (var kvp in ctx.TypeRegistry)
			{
				if (kvp.Value == type)
				{
					name = kvp.Key;
					return true;
				}
			}

			name = string.Empty;
			return false;
		}

		private static bool TryResolveValue(TransformContext ctx, string id, out AssemblerValue value)
		{
			foreach (var info in ctx.Values)
			{
				if (info.Id == id)
				{
					value = info.Value;
					return true;
				}
			}

			value = NoValue.Instance;
			return false;
		}

		/// <summary>
		/// Wraps a parsed <see cref="AssemblerValue"/> into a <see cref="ValueSource{T}"/>.
		/// Constants are dereferenced to their values.
		/// Variable references become <see cref="ValueReferenceSource{T}"/>.
		/// Expression references become <see cref="ExpressionSource{T}"/> with their arguments
		/// recursively wrapped as <see cref="ValueSource{T}"/>.
		/// </summary>
		private static ClockProperty ParseClockProperty(string property) =>
			property.Trim().ToLowerInvariant() switch
			{
				"deltatime" => ClockProperty.DeltaTime,
				"time" => ClockProperty.Time,
				"framecount" => ClockProperty.FrameCount,
				"unscaleddeltatime" => ClockProperty.UnscaledDeltaTime,
				_ => throw new ParsingException(
					$"Unknown !clock property '{property}'. Expected one of: deltaTime, time, frameCount, unscaledDeltaTime")
			};

		public static ValueSource<T> CreateValueSource<T>(TransformContext ctx,
			AssemblerValue raw,
			T? fallback = default) =>
			raw switch
			{
				ParamRef paramRef => ctx.Parameters.TryGetValue(paramRef.Id, out var paramValue)
					? CreateValueSource(ctx, paramValue, fallback)
					: new ParameterSource<T>(paramRef.Id),
				AssetRef assetRef => new AssetSource<T>(assetRef.Id),
				EntityPositionRef entityPositionRef when typeof(T) == typeof(Vector3) || typeof(T) == typeof(object) =>
					new EntityPositionSource<T>(entityPositionRef.Id),
				EntityPositionRef entityPositionRef => throw new ParsingException(
					$"!entity_position '{entityPositionRef.Id}' resolves to Vector3 but was used where a {typeof(T).Name} was expected"),
				ClockRef clockRef when typeof(T) == typeof(float) || typeof(T) == typeof(int)
					|| typeof(T) == typeof(double) || typeof(T) == typeof(object) =>
					new ClockValueSource<T>(ParseClockProperty(clockRef.Property)),
				ClockRef clockRef => throw new ParsingException(
					$"!clock '{clockRef.Property}' resolves to a numeric value but was used where a {typeof(T).Name} was expected"),
				OutputRef outputRef => new TriggerOutputSource<T>(outputRef.Id),
				TextRef textRef when typeof(T) == typeof(string) =>
					new LocalisedTextSource<T>(textRef.Key, BuildTextArguments(ctx, textRef)),
				TextRef textRef => throw new ParsingException(
					$"!text '{textRef.Key}' resolves to a string but was used where a {typeof(T).Name} was expected"),
				VarRef varRef => new ValueReferenceSource<T>(varRef.Id),
				ExprRef exprRef => CreateExpressionSource<T>(ctx, exprRef),
				VecValue vec when typeof(T) == typeof(Vector3) => new ConstantSource<T>(
					(T)(object)vec.ToVector3(ctx.Values)),
				VecValue vec when typeof(T) == typeof(Vector2) => new ConstantSource<T>(
					(T)(object)vec.ToVector2(ctx.Values)),
				VecValue vec => new ConstantSource<T>((T)(object)vec.ToVector3(ctx.Values)),
				ColourValue col when typeof(T) == typeof(Color) => new ConstantSource<T>(
					(T)(object)col.ToColor(ctx.Values)),
				Vector3Value v3 when typeof(T) == typeof(Vector3) => new ConstantSource<T>((T)(object)v3.Value),
				Vector3Value v3 when typeof(T) == typeof(Vector2) => new ConstantSource<T>(
					(T)(object)new Vector2(v3.Value.x, v3.Value.y)),
				Vector2Value v2 when typeof(T) == typeof(Vector2) => new ConstantSource<T>((T)(object)v2.Value),
				ColorValue cv when typeof(T) == typeof(Color) => new ConstantSource<T>((T)(object)cv.Value),
				TypedListValue typed when IsAssignableList(typeof(T), typed.ElementType) =>
					new ConstantSource<T>((T)BuildTypedList(typed)),
				ListValue list when TryGetListElementType(typeof(T), out var elementType) =>
					new ConstantSource<T>((T)BuildListFromUntyped(list, elementType!)),
				NoValue or null when fallback is not null => new ConstantSource<T>(fallback),
				NoValue or null => None<T>.Instance,
				_ => new ConstantSource<T>(CoerceConstant<T>(raw))
			};

		private static bool IsAssignableList(Type t, Type elementType)
		{
			if (!t.IsGenericType)
			{
				return false;
			}

			var genericDef = t.GetGenericTypeDefinition();

			if (genericDef != typeof(IReadOnlyList<>) &&
			    genericDef != typeof(IEnumerable<>) &&
			    genericDef != typeof(List<>))
			{
				return false;
			}

			return t.GetGenericArguments()[0] == elementType;
		}

		private static bool TryGetListElementType(Type t, out Type? elementType)
		{
			elementType = null;

			if (!t.IsGenericType)
			{
				return false;
			}

			var genericDef = t.GetGenericTypeDefinition();

			if (genericDef != typeof(IReadOnlyList<>) &&
			    genericDef != typeof(IEnumerable<>) &&
			    genericDef != typeof(List<>))
			{
				return false;
			}

			elementType = t.GetGenericArguments()[0];
			return true;
		}

		private static object BuildTypedList(TypedListValue typed)
		{
			var listType = typeof(List<>).MakeGenericType(typed.ElementType);
			var list = (System.Collections.IList)Activator.CreateInstance(listType, typed.Items.Count);

			foreach (var item in typed.Items)
			{
				list.Add(UnwrapPrimitive(item, typed.ElementType));
			}

			return list;
		}

		private static object BuildListFromUntyped(ListValue list, Type elementType)
		{
			var listType = typeof(List<>).MakeGenericType(elementType);
			var result = (System.Collections.IList)Activator.CreateInstance(listType, list.Value.Count);

			foreach (var item in list.Value)
			{
				result.Add(UnwrapPrimitive(item, elementType));
			}

			return result;
		}

		private static object UnwrapPrimitive(AssemblerValue value, Type expectedType)
		{
			return value switch
			{
				IntValue i when expectedType == typeof(int) => i.Value,
				IntValue i when expectedType == typeof(float) => (float)i.Value,
				FloatValue f when expectedType == typeof(float) => f.Value,
				BoolValue b when expectedType == typeof(bool) => b.Value,
				StringValue s when expectedType == typeof(string) => s.Value,
				Vector2Value v when expectedType == typeof(Vector2) => v.Value,
				Vector3Value v when expectedType == typeof(Vector3) => v.Value,
				ColorValue c when expectedType == typeof(Color) => c.Value,
				_ => throw new ParsingException(
					$"List element {value.GetType().Name} cannot be coerced to {expectedType.Name}")
			};
		}

		private static T CoerceConstant<T>(AssemblerValue value)
		{
			if (RefDtoExtensions.TryUnwrap<T>(value, out var unwrapped))
			{
				return unwrapped;
			}

			if (typeof(T) == typeof(object))
			{
				return (T)Unwrap(value);
			}

			throw new ParsingException(
				$"Cannot convert value '{value}' of type '{value.GetType()}' to a {typeof(T)}");
		}

		private static object Unwrap(AssemblerValue value) =>
			value switch
			{
				IntValue i => i.Value,
				FloatValue f => f.Value,
				BoolValue b => b.Value,
				StringValue s => s.Value,
				Vector2Value v => v.Value,
				Vector3Value v => v.Value,
				ColorValue c => c.Value,
				_ => throw new ParsingException($"Cannot unwrap {value.GetType().Name} to object")
			};

		private static AssemblerValue Convert(IReadOnlyList<ValueInfo> resolvedValues, object? obj, string? name = null) =>
			obj switch
			{
				VecDto vecDto => new Vector3Value(vecDto.ToVector3(resolvedValues)),
				ColourDto colourDto => new ColorValue(colourDto.ToColor(resolvedValues)),
				RefDto refDto => ResolveRef(refDto, resolvedValues),
				int i => new IntValue(i),
				float f => new FloatValue(f),
				double d => new FloatValue((float)d),
				bool b => new BoolValue(b),
				string s => new StringValue(s),
				List<VecDto> vecList => new TypedListValue(typeof(Vector3),
					vecList.ConvertAll<AssemblerValue>(v => new Vector3Value(v.ToVector3(resolvedValues)))),
				List<ColourDto> colourList => new TypedListValue(typeof(Color),
					colourList.ConvertAll<AssemblerValue>(c => new ColorValue(c.ToColor(resolvedValues)))),
				List<int> intList => new TypedListValue(typeof(int),
					intList.ConvertAll<AssemblerValue>(i => new IntValue(i))),
				List<float> floatList => new TypedListValue(typeof(float),
					floatList.ConvertAll<AssemblerValue>(f => new FloatValue(f))),
				List<bool> boolList => new TypedListValue(typeof(bool),
					boolList.ConvertAll<AssemblerValue>(b => new BoolValue(b))),
				List<string> stringList => new TypedListValue(typeof(string),
					stringList.ConvertAll<AssemblerValue>(s => new StringValue(s))),
				not null => throw new ParsingException(DescribeConvertFailure(obj, name)),
				_ => throw new ParsingException(
					$"Cannot convert null to a value{(name is null ? string.Empty : $" (for '{name}')")}")
			};

		// Builds the failure message for a value Convert can't handle, naming the offending
		// field and — for an untyped collection (e.g. a mixed YAML sequence that deserialises to
		// List<object>) — listing the element types so the mismatch is visible.
		private static string DescribeConvertFailure(object obj, string? name)
		{
			var forField = name is null ? string.Empty : $" for '{name}'";

			if (obj is System.Collections.IEnumerable enumerable and not string)
			{
				var elementTypes = enumerable.Cast<object?>()
					.Select(item => item?.GetType().Name ?? "null")
					.Distinct()
					.ToArray();

				return $"Cannot convert value of type {obj.GetType()} to a value{forField} " +
				       $"(element types: {string.Join(", ", elementTypes)})";
			}

			return $"Cannot convert value of type {obj.GetType()} to a value{forField}";
		}

		private static AssemblerValue ResolveRef(RefDto refDto, IReadOnlyList<ValueInfo> resolvedValues) =>
			resolvedValues.ResolveValue(refDto.Id);

		private static Dictionary<string, AssemblerValue> ConvertProps(IReadOnlyDictionary<string, object>? raw)
		{
			if (raw is null)
			{
				return new Dictionary<string, AssemblerValue>();
			}

			var result = new Dictionary<string, AssemblerValue>(raw.Count);

			foreach (var kvp in raw)
			{
				var converted = ToAssemblerValue(kvp.Value);

				if (converted is not NoValue)
				{
					result[kvp.Key] = converted;
				}
			}

			return result;
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

		private static AssemblerValue ToAssemblerValue(object? raw) =>
			raw switch
			{
				null => NoValue.Instance,
				AssemblerValue av => av,
				int i => new IntValue(i),
				float f => new FloatValue(f),
				double d => new FloatValue((float)d),
				bool b => new BoolValue(b),
				string s => new StringValue(s),
				VarRefDto v => new VarRef(v.Id ?? string.Empty),
				AssetRefDto v => new AssetRef(v.Id ?? string.Empty),
				EntityPositionRefDto v => new EntityPositionRef(v.Id ?? string.Empty),
				ClockRefDto v => new ClockRef(v.Property ?? string.Empty),
				OutputRefDto v => new OutputRef(v.Id ?? string.Empty),
				ParamRefDto v => new ParamRef(v.Id ?? string.Empty),
				// A nested `!gameover` (e.g. inside a state machine OnEnter/OnExit list) deserialises to a
				// GameOverListenerDto via the global tag mapping; carry it through as a marker so the
				// nested-listener parser can rebuild a GameOverListenerInfo.
				GameOverListenerDto => new GameOverMarker(),
				ExprRefDto v => new ExprRef(v.Do ?? string.Empty,
					v.With.EmptyIfNull().Select(ToAssemblerValue).ToArray()),
				TextRefDto v => new TextRef(v.Key ?? string.Empty,
					v.Arguments.EmptyIfNull().Select(ToAssemblerValue).ToArray()),
				VecDto v => new VecValue(ToAssemblerValue(v.X), ToAssemblerValue(v.Y), ToAssemblerValue(v.Z)),
				ColourDto v => new ColourValue(ToAssemblerValue(v.R),
					ToAssemblerValue(v.G),
					ToAssemblerValue(v.B),
					ToAssemblerValue(v.A),
					v.Raw is null ? NoValue.Instance : new StringValue(v.Raw)),
				List<VecDto> vecList => new TypedListValue(typeof(Vector3), vecList.ConvertAll(ToAssemblerValue)),
				List<ColourDto> colourList => new TypedListValue(typeof(Color), colourList.ConvertAll(ToAssemblerValue)),
				List<int> intList => new TypedListValue(typeof(int), intList.ConvertAll<AssemblerValue>(i => new IntValue(i))),
				List<float> floatList => new TypedListValue(typeof(float),
					floatList.ConvertAll<AssemblerValue>(f => new FloatValue(f))),
				List<bool> boolList => new TypedListValue(typeof(bool),
					boolList.ConvertAll<AssemblerValue>(b => new BoolValue(b))),
				List<string> stringList => new TypedListValue(typeof(string),
					stringList.ConvertAll<AssemblerValue>(s => new StringValue(s))),
				IDictionary<string, object> dict => new DictValue(ToAssemblerDict(dict)),
				IEnumerable<object> list => new ListValue(ToAssemblerList(list)),
				_ => throw new ParsingException($"Cannot convert raw value '{raw}' (type {raw.GetType()}) to an AssemblerValue")
			};

		private static IReadOnlyDictionary<string, AssemblerValue> ToAssemblerDict(IDictionary<string, object> dict)
		{
			var result = new Dictionary<string, AssemblerValue>(dict.Count);

			foreach (var kvp in dict)
			{
				var converted = ToAssemblerValue(kvp.Value);

				if (converted is not NoValue)
				{
					result[kvp.Key] = converted;
				}
			}

			return result;
		}

		private static IReadOnlyList<AssemblerValue> ToAssemblerList(IEnumerable<object> list) =>
			list.Select(ToAssemblerValue).Where(converted => converted is not NoValue).ToList();
	}
}