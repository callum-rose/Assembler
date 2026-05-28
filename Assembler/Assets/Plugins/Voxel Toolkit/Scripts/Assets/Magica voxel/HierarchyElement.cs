using System.Collections.Generic;

namespace VoxelToolkit.MagicaVoxel
{
    public abstract class HierarchyElement
    {
        public int Id;
        public Dictionary<string, string> Attributes;

        public HierarchyElement(int id, Dictionary<string, string> attributes)
        {
            Id = id;
            Attributes = attributes;
        }

        public abstract HierarchyNode AddToAsset(VoxelAsset asset, List<Model> models);
    }
}