using System.Collections.Generic;

namespace Assembler.Parsing.Phase1.Dtos
{
	public sealed record TemplateRefDto
	{
		public string? Id { get; init; }
		public Dictionary<string, object>? Parameters { get; init; }
	}
}