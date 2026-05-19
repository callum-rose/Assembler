using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Assembler.Deserialisation.Dtos;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Parsing
{
	public static class RefDtoExtensions
	{
		public static T ResolveValue<T>(this RefDto refDto, IReadOnlyList<ValueInfo> resolvedValues)
		{
			var valueDto = resolvedValues.FirstOrDefault(v => v.Id == refDto.Id);

			if (valueDto is null)
			{
				throw new ParsingException($"Reference with ID {refDto.Id} not found");
			}

			if (TryUnwrap<T>(valueDto.Value, out var value))
			{
				return value;
			}

			throw new ParsingException(
				$"Reference with ID {refDto.Id} does not contain a value of type {typeof(T).Name}. Actual type: {valueDto.Value.GetType().Name}");
		}

		public static bool TryResolveValue<T>(this RefDto refDto,
			IReadOnlyList<ValueInfo> resolvedValues,
			[NotNullWhen(true)] out T? value)
		{
			var valueDto = resolvedValues.FirstOrDefault(v => v.Id == refDto.Id);

			if (valueDto is null)
			{
				value = default;
				return false;
			}

			return TryUnwrap(valueDto.Value, out value);
		}

		internal static bool TryUnwrap<T>(AssemblerValue assemblerValue, [NotNullWhen(true)] out T? value)
		{
			switch (assemblerValue)
			{
				case IntValue iv when typeof(T) == typeof(int):
					value = (T)(object)iv.Value;
					return true;
				case IntValue iv when typeof(T) == typeof(float):
					value = (T)(object)(float)iv.Value;
					return true;
				case IntValue iv when typeof(T) == typeof(double):
					value = (T)(object)(double)iv.Value;
					return true;
				case FloatValue fv when typeof(T) == typeof(float):
					value = (T)(object)fv.Value;
					return true;
				case FloatValue fv when typeof(T) == typeof(double):
					value = (T)(object)(double)fv.Value;
					return true;
				case BoolValue bv when typeof(T) == typeof(bool):
					value = (T)(object)bv.Value;
					return true;
				case StringValue sv when typeof(T) == typeof(string):
					value = (T)(object)sv.Value;
					return true;
				case Vector2Value v when typeof(T) == typeof(Vector2):
					value = (T)(object)v.Value;
					return true;
				case Vector3Value v when typeof(T) == typeof(Vector3):
					value = (T)(object)v.Value;
					return true;
				case ColorValue v when typeof(T) == typeof(Color):
					value = (T)(object)v.Value;
					return true;
				default:
					value = default;
					return false;
			}
		}
	}
}
