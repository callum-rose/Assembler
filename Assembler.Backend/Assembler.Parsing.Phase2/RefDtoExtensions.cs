using System.Diagnostics.CodeAnalysis;
using Assembler.Parsing.Phase1.Dtos;

namespace Assembler.Parsing2;

public static class RefDtoExtensions
{
	extension(RefDto refDto)
	{
		public T ResolveValue<T>(IReadOnlyList<Value> resolvedValues)
		{
			var valueDto = resolvedValues.FirstOrDefault(v => v.Id == refDto.Id);

			if (valueDto is null)
			{
				throw new ArgumentException($"Reference with ID {refDto.Id} not found", nameof(resolvedValues));
			}

			if (valueDto.Object is not T value)
			{
				throw new ArgumentException($"Reference with ID {refDto.Id} does not contain a value of type {typeof(T).Name}",
					nameof(resolvedValues));
			}

			return value;
		}

		public bool TryResolveValue<T>(IReadOnlyList<Value> resolvedValues, [NotNullWhen(true)] out T? value)
		{
			var valueDto = resolvedValues.FirstOrDefault(v => v.Id == refDto.Id);

			if (valueDto is null)
			{
				value = default;
				return false;
			}

			if (valueDto.Object is not T t)
			{
				value = default;
				return false;
			}

			value = t;
			return true;
		}
	}
}