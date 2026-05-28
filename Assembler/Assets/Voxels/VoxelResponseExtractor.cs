using Assembler.Anthropic;

namespace Assembler.Voxels
{
	public static class VoxelResponseExtractor
	{
		public static string? Extract(string assistantText) =>
			FencedBlockExtractor.Extract(assistantText, "goxel");
	}
}
