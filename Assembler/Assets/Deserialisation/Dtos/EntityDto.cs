using System.Collections.Generic;

namespace Assembler.Deserialisation.Dtos
{
	public sealed record EntityDto
	{
		public string? Id { get; init; }
		public TemplateRefDto? Template { get; init; }
		public List<string>? Tags { get; init; }
		public object? Position { get; init; }
		public object? Rotation { get; init; }
		public Dictionary<string, BehaviourDto>? Behaviours { get; init; }
		public Dictionary<string, object>? Variables { get; init; }
		public List<EntityDto>? Children { get; init; }
	}
}
