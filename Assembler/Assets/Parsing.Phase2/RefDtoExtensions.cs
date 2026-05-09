using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Assembler.Parsing.Phase1.Dtos;
using Assembler.Parsing2.Info;

namespace Assembler.Parsing2
{
	public static class RefDtoExtensions
	{
		public static T ResolveValue<T>(this RefDto refDto, IReadOnlyList<VariableInfo> resolvedValues)
		{
			var valueDto = resolvedValues.FirstOrDefault(v => v.Id == refDto.Id);

			if (valueDto is null)
			{
				throw new ParsingException($"Reference with ID {refDto.Id} not found");
			}

			if (valueDto.Value is not T value)
			{
				// Special case for numeric conversions
				if (typeof(T) == typeof(float))
				{
					if (valueDto.Value is int i)
					{
						return (T)(object)(float)i;
					}
				}

				throw new ParsingException($"Reference with ID {refDto.Id} does not contain a value of type {typeof(T).Name}. Actual type: {valueDto.Value?.GetType().Name ?? "null"}");
			}

			return value;
		}

		public static bool TryResolveValue<T>(this RefDto refDto, IReadOnlyList<VariableInfo> resolvedValues, [NotNullWhen(true)] out T? value)
		{
			var valueDto = resolvedValues.FirstOrDefault(v => v.Id == refDto.Id);

			if (valueDto is null)
			{
				value = default;
				return false;
			}

			if (valueDto.Value is not T t)
			{
				value = default;
				return false;
			}

			value = t;
			return true;
		}
	}
}