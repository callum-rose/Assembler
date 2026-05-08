using Assembler.Parsing.Phase1.Dtos;

namespace Assembler.Parsing2;

public static class VecDtoToVector
{
	extension(VecDto dto)
	{
		public Vector2 ToVector2(IReadOnlyList<ValueDto> resolvedReferences)
		{
			var vec = new Vector2();

			{
				if (dto.X is int i)
				{
					vec.X = i;
				}
				else if (dto.X is float f)
				{
					vec.X = f;
				}
				else if (dto.X is string s && float.TryParse(s, out var parsedFloat))
				{
					vec.X = parsedFloat;
				}
				else if (dto.X is RefDto refDto)
				{
					vec.X = refDto.ResolveValue<float>(resolvedReferences);
				}
				else
				{
					throw new ArgumentException("Invalid X value in VecDto", nameof(dto));
				}
			}

			{
				if (dto.Y is int i)
				{
					vec.Y = i;
				}
				else if (dto.Y is float f)
				{
					vec.Y = f;
				}
				else if (dto.Y is string s && float.TryParse(s, out var parsedFloat))
				{
					vec.Y = parsedFloat;
				}
				else if (dto.Y is RefDto refDto)
				{
					vec.Y = refDto.ResolveValue<float>(resolvedReferences);
				}
				else
				{
					throw new ArgumentException("Invalid Y value in VecDto", nameof(dto));
				}
			}

			return vec;
		}

		public Vector3 ToVector3(IReadOnlyList<ValueDto> resolvedReferences)
		{
			var vec = new Vector3();

			{
				if (dto.X is int i)
				{
					vec.X = i;
				}
				else if (dto.X is float f)
				{
					vec.X = f;
				}
				else if (dto.X is string s && float.TryParse(s, out var parsedFloat))
				{
					vec.X = parsedFloat;
				}
				else if (dto.X is RefDto refDto)
				{
					vec.X = refDto.ResolveValue<float>(resolvedReferences);
				}
				else
				{
					throw new ArgumentException("Invalid X value in VecDto", nameof(dto));
				}
			}

			{
				if (dto.Y is int i)
				{
					vec.Y = i;
				}
				else if (dto.Y is float f)
				{
					vec.Y = f;
				}
				else if (dto.Y is string s && float.TryParse(s, out var parsedFloat))
				{
					vec.Y = parsedFloat;
				}
				else if (dto.Y is RefDto refDto)
				{
					vec.Y = refDto.ResolveValue<float>(resolvedReferences);
				}
				else
				{
					throw new ArgumentException("Invalid Y value in VecDto", nameof(dto));
				}
			}

			{
				if (dto.Z is int i)
				{
					vec.Z = i;
				}
				else if (dto.Z is float f)
				{
					vec.Z = f;
				}
				else if (dto.Z is string s && float.TryParse(s, out var parsedFloat))
				{
					vec.Z = parsedFloat;
				}
				else if (dto.Z is RefDto refDto)
				{
					vec.Z = refDto.ResolveValue<float>(resolvedReferences);
				}
				else if (dto.Z is not null)
				{
					throw new ArgumentException("Invalid Z value in VecDto", nameof(dto));
				}
			}

			return vec;
		}
	}
}