using System.Collections.Generic;

namespace Assembler.Deserialisation.Dtos
{
	public sealed record GameDto
	{
		public InfoDto? Game { get; init; }
		public WorldDto? World { get; init; }
		public PhysicsDto? Physics { get; init; }
		public List<AssetDto>? Assets { get; init; }
		public Dictionary<string, object>? Constants { get; init; }
		public Dictionary<string, object>? Variables { get; init; }
		public Dictionary<string, ExpressionDto>? Expressions { get; init; }
		public Dictionary<string, EntityDto>? Templates { get; init; }
		public Dictionary<string, EntityDto>? Entities { get; init; }
		public Dictionary<string, PlacementDto>? Placements { get; init; }
		public ControlsDto? Controls { get; init; }
		public LocalisationDto? Localisation { get; init; }
	}
}
