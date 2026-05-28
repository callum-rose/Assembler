using System.Collections.Generic;
using UnityEngine;

namespace VoxelToolkit.MagicaVoxel
{
	public class GroupElement : HierarchyElement
	{
		public int[] ChildrenIds;

		public HierarchyElement[] Children;

		public GroupElement(int id, Dictionary<string, string> attributes, int[] children) : base(id, attributes)
		{
			ChildrenIds = children;
		}

		public override HierarchyNode AddToAsset(VoxelAsset asset, List<Model> models)
		{
			var group = ScriptableObject.CreateInstance<Group>();
			group.Name = Attributes.TryGetValue("_name", out string name) ? name : $"Group {Id}";

			foreach (var hierarchyElement in Children)
				group.AddChild(hierarchyElement.AddToAsset(asset, models));

			return group;
		}
	}
}