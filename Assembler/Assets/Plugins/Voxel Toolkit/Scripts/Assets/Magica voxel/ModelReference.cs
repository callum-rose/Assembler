using System.Collections.Generic;

namespace VoxelToolkit.MagicaVoxel
{
	public class ModelReference
	{
		public int Id;
		public Dictionary<string, string> Attributes = new Dictionary<string, string>();

		public ModelReference(int id, Dictionary<string, string> attributes)
		{
			Id = id;
			Attributes = attributes;
		}
	}
}