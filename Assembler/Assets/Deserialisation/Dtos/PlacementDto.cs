using System.Collections.Generic;

namespace Assembler.Deserialisation.Dtos
{
	// A top-level Placements entry: stamps a named Template out at many absolute positions. `At` is a
	// `vector list` (a literal sequence of `!vec` or an `!expr` that returns one); `Rotation` is a single
	// euler shared by every stamped instance; `Parameters` are forwarded to the template's parameter slots.
	public sealed record PlacementDto
	{
		public string? Template { get; init; }
		public object? At { get; init; }
		public object? Rotation { get; init; }
		public Dictionary<string, object>? Parameters { get; init; }
		public List<string>? Tags { get; init; }
	}
}
