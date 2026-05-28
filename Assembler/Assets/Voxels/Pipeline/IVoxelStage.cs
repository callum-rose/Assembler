using System.Threading;
using System.Threading.Tasks;

namespace Assembler.Voxels.Pipeline
{
	public interface IVoxelStage
	{
		string Name { get; }
		Task<VoxelPipelineContext> ExecuteAsync(VoxelPipelineContext ctx, CancellationToken ct);
	}
}
