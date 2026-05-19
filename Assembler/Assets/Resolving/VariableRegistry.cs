using System;
using System.Collections.Generic;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Resolving
{
	public class VariableRegistry
	{
		private readonly Dictionary<string, IValueProvider> _variables = new();

		public void Register(ValueInfo valueInfo)
		{
			_variables[valueInfo.Id] = valueInfo.Value switch
			{
				IntValue i => new ValueProvider<int>(i.Value),
				FloatValue f => new ValueProvider<float>(f.Value),
				BoolValue b => new ValueProvider<bool>(b.Value),
				StringValue s => new ValueProvider<string>(s.Value),
				Vector2Value vec2 => new ValueProvider<Vector2>(vec2.Value),
				Vector3Value vec3 => new ValueProvider<Vector3>(vec3.Value),
				ColorValue c => new ValueProvider<Color>(c.Value),
				_ => throw new Exception(
					$"Unsupported value type of '{valueInfo.Value.GetType()}' for variable '{valueInfo.Id}'")
			};
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
				IValueProvider<int> intProvider when typeof(T) == typeof(float) =>
					(IValueProvider<T>)(object)new MappedValueProvider<int, float>(intProvider, i => i),
				_ => throw new Exception(
					$"Type mismatch for variable '{id}'. Expected {typeof(T)}, got {container.GetType()}")
			};
		}
	}
}
