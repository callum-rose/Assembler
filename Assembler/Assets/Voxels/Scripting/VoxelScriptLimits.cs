using System;

namespace Assembler.Voxels.Scripting
{
	/// <summary>
	/// Configurable safety caps for procedural voxel scripts. The voxel-count cap
	/// is enforced inside <see cref="VoxelBuilder"/> as it mutates; the wall-clock
	/// budget is enforced both cooperatively inside the builder and as a hard
	/// timeout by the executor; <see cref="MaxToolIterations"/> bounds the
	/// client-side tool loop in <see cref="Assembler.Anthropic.AnthropicClient"/>.
	/// </summary>
	public sealed record VoxelScriptLimits
	{
		public int MaxVoxels { get; init; } = 1_000_000;
		public TimeSpan WallClock { get; init; } = TimeSpan.FromSeconds(5);
		public int MaxToolIterations { get; init; } = 8;

		public static VoxelScriptLimits Default { get; } = new();
	}
}
