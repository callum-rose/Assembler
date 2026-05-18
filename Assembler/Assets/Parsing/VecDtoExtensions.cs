using System.Collections.Generic;
using Assembler.Deserialisation.Dtos;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Parsing
{
	public static class FloatHelper
	{
		public static float Resolve(object? component, IReadOnlyList<ValueInfo> resolvedValues)
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
	
	public static class VecDtoExtensions
	{
		public static Vector2 ToVector2(this VecDto dto, IReadOnlyList<ValueInfo> resolvedValues)
		{
			return new Vector2(
				FloatHelper.Resolve(dto.X, resolvedValues),
				FloatHelper.Resolve(dto.Y, resolvedValues)
			);
		}

		public static Vector3 ToVector3(this VecDto dto, IReadOnlyList<ValueInfo> resolvedValues)
		{
			return new Vector3(
				FloatHelper.Resolve(dto.X, resolvedValues),
				FloatHelper.Resolve(dto.Y, resolvedValues),
				dto.Z is not null ? FloatHelper.Resolve(dto.Z, resolvedValues) : 0f
			);
		}
	}
}