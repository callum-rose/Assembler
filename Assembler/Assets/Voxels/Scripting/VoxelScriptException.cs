using System;

namespace Assembler.Voxels.Scripting
{
	/// <summary>
	/// Thrown when a voxel script violates a safety limit (voxel-count cap or
	/// wall-clock budget) or asks the builder to do something impossible (e.g.
	/// more than 255 distinct colours). The executor converts this into a
	/// tool_result error so Claude can self-correct rather than crashing the run.
	/// </summary>
	public sealed class VoxelScriptException : Exception
	{
		public VoxelScriptException(string message) : base(message)
		{
		}
	}
}
