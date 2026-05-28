using System.IO;

namespace VoxelToolkit
{
	public abstract class VoxelImporter
	{
		public abstract VoxelAsset ImportAsset(BinaryReader reader);
	}
}