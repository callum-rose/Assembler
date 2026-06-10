using System;
using System.Collections.Generic;
using Assembler.Libraries;
using Assembler.Parsing.Info;
using Assembler.Parsing.Info.Behaviours;
using UnityEngine;

namespace Assembler.Resolving
{
	public class VariableRegistry
	{
		private readonly Dictionary<string, IValueProvider> _global = new();

		/// <summary>All global variables keyed by id. Used by debug tooling to inspect/edit game-wide state.</summary>
		public IEnumerable<KeyValuePair<string, IValueProvider>> Globals => _global;

		public void Register(ValueInfo valueInfo)
		{
			_global[valueInfo.Id] = BuildProvider(valueInfo);
		}

		public IValueProvider<T> Get<T>(string id) => Get<T>(id, new EntityVariableScope());

		public IValueProvider<T> Get<T>(string id, EntityVariableScope scope)
		{
			if (scope.TryGet(id, out var provider) || _global.TryGetValue(id, out provider))
			{
				return provider switch
				{
					IValueProvider<T> typedProvider => typedProvider,
					IValueProvider<int> intProvider when typeof(T) == typeof(float) =>
						(IValueProvider<T>)(object)new MappedValueProvider<int, float>(intProvider, i => i),
					// An enum-bound `!var` reads a plain string constant once and hands back a genuine
					// ValueProvider<TEnum> with a real enum value — not a per-read string mapping. The enum
					// type is known here (the read site is ValueReferenceSource<TEnum>), so parse in place.
					IValueProvider<string> str when typeof(T).IsEnum =>
						(IValueProvider<T>)(object)BuildEnumProvider(typeof(T), str.Get(TriggerContext.Empty)),
					_ when typeof(T) == typeof(object) =>
						(IValueProvider<T>)(object)new BoxingValueProvider(provider),
					_ => throw new ResolveException(
						$"Type mismatch for variable '{id}'. Expected {typeof(T)}, got {provider.GetType()}")
				};
			}

			throw new ResolveException($"Variable not registered for id: {id}");
		}

		// Parses a constant string variable into a typed enum provider. Each arm has the concrete enum, so it
		// calls the generic BehaviourEnums.Parse directly (Get<T>'s T is unconstrained and can't).
		private static IValueProvider BuildEnumProvider(Type enumType, string raw) =>
			enumType == typeof(Easing) ? new ValueProvider<Easing>(BehaviourEnums.Parse<Easing>(raw)) :
			enumType == typeof(LayoutDirection) ? new ValueProvider<LayoutDirection>(BehaviourEnums.Parse<LayoutDirection>(raw)) :
			enumType == typeof(PrimitiveType) ? new ValueProvider<PrimitiveType>(BehaviourEnums.Parse<PrimitiveType>(raw)) :
			enumType == typeof(TextAnchor) ? new ValueProvider<TextAnchor>(BehaviourEnums.Parse<TextAnchor>(raw)) :
			enumType == typeof(CameraProjection) ? new ValueProvider<CameraProjection>(BehaviourEnums.Parse<CameraProjection>(raw)) :
			enumType == typeof(CameraFollowMode) ? new ValueProvider<CameraFollowMode>(BehaviourEnums.Parse<CameraFollowMode>(raw)) :
			enumType == typeof(ButtonPhase) ? new ValueProvider<ButtonPhase>(BehaviourEnums.Parse<ButtonPhase>(raw)) :
			throw new ResolveException($"No enum variable provider registered for type '{enumType}'");

		internal static IValueProvider BuildProvider(ValueInfo valueInfo)
		{
			return valueInfo.Value switch
			{
				IntValue i => new ValueProvider<int>(i.Value),
				FloatValue f => new ValueProvider<float>(f.Value),
				BoolValue b => new ValueProvider<bool>(b.Value),
				StringValue s => new ValueProvider<string>(s.Value),
				Vector3Value vec3 => new ValueProvider<Vector3>(vec3.Value),
				ColorValue c => new ValueProvider<Color>(c.Value),
				// A record constant referenced by !var. The transform already completed it (defaults filled),
				// so build the Record straight from the field dict — no schema needed here.
				RecordValue rec => new ValueProvider<Record>(RecordValues.ToRecord(rec)),
				TypedListValue typed => BuildListProvider(typed),
				_ => throw new ResolveException(
					$"Unsupported value type of '{valueInfo.Value.GetType()}' for variable '{valueInfo.Id}'")
			};
		}

		private static IValueProvider BuildListProvider(TypedListValue typed)
		{
			if (typed.ElementType == typeof(int))
			{
				return BuildListProvider<int>(typed);
			}

			if (typed.ElementType == typeof(float))
			{
				return BuildListProvider<float>(typed);
			}

			if (typed.ElementType == typeof(bool))
			{
				return BuildListProvider<bool>(typed);
			}

			if (typed.ElementType == typeof(string))
			{
				return BuildListProvider<string>(typed);
			}

			if (typed.ElementType == typeof(Vector3))
			{
				return BuildListProvider<Vector3>(typed);
			}

			if (typed.ElementType == typeof(Color))
			{
				return BuildListProvider<Color>(typed);
			}

			if (typed.ElementType == typeof(Record))
			{
				return BuildListProvider<Record>(typed);
			}

			throw new ResolveException($"Unsupported list element type: {typed.ElementType}");
		}

		private static IValueProvider BuildListProvider<T>(TypedListValue typed)
		{
			var list = new List<T>(typed.Items.Count);

			foreach (var item in typed.Items)
			{
				list.Add(UnwrapTo<T>(item));
			}

			return new ValueProvider<List<T>>(list);
		}

		private static T UnwrapTo<T>(AssemblerValue value)
		{
			return value switch
			{
				IntValue i when typeof(T) == typeof(int) => (T)(object)i.Value,
				IntValue i when typeof(T) == typeof(float) => (T)(object)(float)i.Value,
				FloatValue f when typeof(T) == typeof(float) => (T)(object)f.Value,
				BoolValue b when typeof(T) == typeof(bool) => (T)(object)b.Value,
				StringValue s when typeof(T) == typeof(string) => (T)(object)s.Value,
				Vector3Value v when typeof(T) == typeof(Vector3) => (T)(object)v.Value,
				ColorValue c when typeof(T) == typeof(Color) => (T)(object)c.Value,
				// Element of a record list. The transform completed each RecordValue, so build the Record
				// straight from its fields (no schema). Reference type, so list identity/mutation work.
				RecordValue rec when typeof(T) == typeof(Record) => (T)(object)RecordValues.ToRecord(rec),
				_ => throw new ResolveException($"Cannot unwrap {value.GetType().Name} to {typeof(T).Name}")
			};
		}
	}
}
