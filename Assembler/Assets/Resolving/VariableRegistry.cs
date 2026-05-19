using System;
using System.Collections.Generic;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Resolving
{
	public class VariableRegistry
	{
		private readonly Dictionary<string, object> _variables = new();

		public void Register(ValueInfo valueInfo)
		{
			_variables[valueInfo.Id] = valueInfo.Value switch
			{
				int i => new ValueProvider<int>(i),
				float f => new ValueProvider<float>(f),
				bool b => new ValueProvider<bool>(b),
				string s => new ValueProvider<string>(s),
				Vector2 vec2 => new ValueProvider<Vector2>(vec2),
				Vector3 vec3 => new ValueProvider<Vector3>(vec3),
				Color c => new ValueProvider<Color>(c),
				_ => throw new Exception(
					$"Unsupported value type of '{valueInfo.Value.GetType()}' for variable '{valueInfo.Id}'")
			};
		}

		public void RegisterCollection(string id, IReadOnlyList<string> items)
		{
			_variables[id] = new ValueProvider<IReadOnlyList<string>>(items);
		}

		public IValueProvider<T> Get<T>(string id)
		{
			if (!_variables.TryGetValue(id, out var container))
			{
				throw new Exception($"Variable not registered for id: {id}");
			}

			return container switch
			{
				IValueProvider<T> typedProvider => typedProvider,
				IValueProvider<int> intProvider when typeof(T) == typeof(float) => (IValueProvider<T>)(object)new MappedValueProvider<int, float>(intProvider, i => i),
				IValueProvider<object> objectProvider => (IValueProvider<T>)objectProvider,
				_ => throw new Exception($"Type mismatch for variable '{id}'. Expected {typeof(T)}, got {container.GetType()}")
			};
		}
	}
}
