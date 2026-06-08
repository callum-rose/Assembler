using System;
using System.Collections.Generic;
using Assembler.Parsing.Info;
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
					_ when typeof(T) == typeof(object) =>
						(IValueProvider<T>)(object)new BoxingValueProvider(provider),
					_ => throw new ResolveException(
						$"Type mismatch for variable '{id}'. Expected {typeof(T)}, got {provider.GetType()}")
				};
			}

			throw new ResolveException($"Variable not registered for id: {id}");
		}

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
				_ => throw new ResolveException($"Cannot unwrap {value.GetType().Name} to {typeof(T).Name}")
			};
		}
	}
}
