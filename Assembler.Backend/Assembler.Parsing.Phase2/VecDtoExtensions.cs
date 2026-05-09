using Assembler.Parsing.Phase1.Dtos;
using Assembler.Parsing2.Info;

namespace Assembler.Parsing2;

public static class VecDtoExtensions
{
	extension(VecDto dto)
	{
		public Vector2 ToVector2(IReadOnlyList<VariableInfo> resolvedValues)
		{
			return new Vector2(
				ResolveComponent(dto.X, resolvedValues),
				ResolveComponent(dto.Y, resolvedValues)
			);
		}

		public Vector3 ToVector3(IReadOnlyList<VariableInfo> resolvedValues)
		{
			return new Vector3(
				ResolveComponent(dto.X, resolvedValues),
				ResolveComponent(dto.Y, resolvedValues),
				dto.Z is not null ? ResolveComponent(dto.Z, resolvedValues) : 0f
			);
		}
	}

	private static float ResolveComponent(object? component, IReadOnlyList<VariableInfo> resolvedValues)
	{
		if (component is int i)
		{
			return i;
		}

		if (component is float f)
		{
			return f;
		}

		if (component is double d)
		{
			return (float)d;
		}

		if (component is string s && float.TryParse(s, out var parsedFloat))
		{
			return parsedFloat;
		}

		if (component is RefDto refDto)
		{
			if (refDto.TryResolveValue<float>(resolvedValues, out var f2))
			{
				return f2;
			}

			if (refDto.TryResolveValue<int>(resolvedValues, out var i2))
			{
				return i2;
			}

			if (refDto.TryResolveValue<double>(resolvedValues, out var d2))
			{
				return (float)d2;
			}

			throw new ParsingException($"Reference with ID {refDto.Id} could not be resolved to a numeric type.");
		}

		throw new ParsingException($"Invalid component value: {component ?? "null"}");
	}
}