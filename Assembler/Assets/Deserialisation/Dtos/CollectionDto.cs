using System.Collections.Generic;

namespace Assembler.Deserialisation.Dtos
{
	public sealed record CollectionDto
	{
		public string? Id { get; init; }
		public List<string>? Items { get; init; }
	}
}
