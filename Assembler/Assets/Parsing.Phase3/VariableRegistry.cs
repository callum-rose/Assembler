using System;
using System.Collections.Generic;
using Assembler.Parsing.Phase2.Info;
using UnityEngine;

namespace Assembler.Parsing.Phase3
{
	public class VariableRegistry
	{
		private readonly Dictionary<string, object> _variables = new();

		public void Register(VariableInfo variableInfo)
		{
			_variables[variableInfo.Id] = variableInfo.Value switch
			{
				int i => new ValueContainer<int>(i),
				float f => new ValueContainer<float>(f),
				bool b => new ValueContainer<bool>(b),
				Vector2 vec2 => new ValueContainer<Vector2>(vec2),
				Vector3 vec3 => new ValueContainer<Vector3>(vec3),
				_ => throw new Exception(
					$"Unsupported value type of '{variableInfo.Value.GetType()}' for variable '{variableInfo.Id}'")
			};
		}

		public ValueContainer<T> Get<T>(string id)
		{
			if (!_variables.TryGetValue(id, out var container))
			{
				throw new Exception($"Variable not registered for id: {id}");
			}

			if (container is not ValueContainer<T> typedContainer)
			{
				throw new Exception($"Type mismatch for variable '{id}'. Expected {typeof(T)}, got {container.GetType()}");
			}

			return typedContainer;
		}
	}
}