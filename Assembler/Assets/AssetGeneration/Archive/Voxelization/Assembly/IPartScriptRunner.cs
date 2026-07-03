using System.Threading;
using System.Threading.Tasks;
using Assembler.Voxels;
using Assembler.Voxels.Scripting;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Executes a part's stored VoxelBuilder script into a grid. Seam so the
	/// assembler is testable without the expression compiler.
	/// </summary>
	public interface IPartScriptRunner
	{
		Task<VoxelModel> RunAsync(string source, CancellationToken ct);
	}

	/// <summary>Production implementation backed by <see cref="IVoxelScriptExecutor"/>.</summary>
	public sealed class ExecutorPartScriptRunner : IPartScriptRunner
	{
		private readonly IVoxelScriptExecutor _executor;

		public ExecutorPartScriptRunner(IVoxelScriptExecutor executor) => _executor = executor;

		public Task<VoxelModel> RunAsync(string source, CancellationToken ct) => _executor.RunScriptAsync(source, ct);
	}
}
