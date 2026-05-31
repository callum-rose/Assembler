using System.Threading;
using System.Threading.Tasks;
using Assembler.Anthropic;

namespace Assembler.Voxels.Scripting
{
	/// <summary>
	/// Owns the <c>run_voxel_script</c> tool definition and handles invocations:
	/// compiles a script against <see cref="VoxelBuilder"/>, runs it under the
	/// safety limits, and stashes the last successful result for the generation
	/// stage and editor to read.
	/// </summary>
	public interface IVoxelScriptExecutor
	{
		AnthropicTool Tool { get; }

		Task<AnthropicToolResult> HandleToolUseAsync(AnthropicToolUse use, CancellationToken ct);

		/// <summary>Source of the most recent successful script, if any.</summary>
		string? LastScript { get; }

		/// <summary>The most recent successful model serialised to goxel text (Z-up).</summary>
		string? LastGoxelTextZUp { get; }

		/// <summary>The most recent successfully-built model, if any.</summary>
		VoxelModel? LastModel { get; }
	}
}
