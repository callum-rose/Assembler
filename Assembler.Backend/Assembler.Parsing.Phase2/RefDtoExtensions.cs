using Assembler.Parsing.Phase1.Dtos;

namespace Assembler.Parsing2;

public static class RefDtoExtensions
{
	extension(RefDto refDto)
	{
		public T ResolveValue<T>(IReadOnlyList<ValueDto> resolvedReferences)
		{
			var valueDto = resolvedReferences.FirstOrDefault(v => v.Id == refDto.Id);

			if (valueDto is null)
			{
				throw new ArgumentException($"Reference with ID {refDto.Id} not found", nameof(resolvedReferences));
			}
		
			if (valueDto.Value is not T value)
			{
				throw new ArgumentException($"Reference with ID {refDto.Id} does not contain a value of type {typeof(T).Name}", nameof(resolvedReferences));
			}
		
			return value;
		}
	}
}