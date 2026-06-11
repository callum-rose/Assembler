namespace Assembler.Deserialisation.Dtos
{
	/// <summary>The world-space bounds the nav grid spans, as <c>!vec</c> min/max corners.</summary>
	public sealed record BoundsDto
	{
		public VecDto? Min { get; init; }
		public VecDto? Max { get; init; }
	}

	/// <summary>The descriptor's top-level <c>Navigation:</c> section configuring the walkability grid.</summary>
	public sealed record NavigationDto
	{
		public float? CellSize { get; init; }
		public BoundsDto? Bounds { get; init; }
		public string? ObstacleTag { get; init; }

		/// <summary>Which world plane the grid spans: <c>"xy"</c> (default) or <c>"xz"</c> (ground plane).</summary>
		public string? Plane { get; init; }

		/// <summary>Whether path/flow searches may step diagonally. Defaults to <c>true</c> (eight-connected);
		/// set <c>false</c> for four-connected, grid-aligned movement (e.g. a Pacman-style maze).</summary>
		public bool? Diagonal { get; init; }

		/// <summary>The game-wide default clearance (world units) by which obstacles are inflated, used by any
		/// agent that doesn't set its own <c>AgentRadius</c>. Omitted/0 means no inflation. A <c>navigate</c> or
		/// <c>grid mover</c> behaviour can override it per agent.</summary>
		public float? DefaultAgentRadius { get; init; }
	}
}
