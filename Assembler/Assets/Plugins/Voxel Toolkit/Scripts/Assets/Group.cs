using System.Collections.Generic;
using UnityEngine;

namespace VoxelToolkit
{
    /// <summary>
    /// Represents the group of the shapes/transforms etc.
    /// </summary>
    [System.Serializable]
    public class Group : HierarchyNode, INamedObject
    {
        [SerializeField] private List<HierarchyNode> children = new List<HierarchyNode>();
        [SerializeField] private string groupName;

        /// <summary>
        /// The name of the group
        /// </summary>
        public string Name
        {
            get => groupName;
            set => groupName = value;
        }
        
        /// <summary>
        /// The count of the children
        /// </summary>
        public int ChildrenCount => children.Count;

        /// <summary>
        /// The related objects of the node
        /// </summary>
        public override ScriptableObject[] RelatedObjects => children.ConvertAll(x => x as ScriptableObject).ToArray();

        /// <summary>
        /// Providing the access to the child
        /// </summary>
        /// <param name="index">The index of the child to be accessed</param>
        public HierarchyNode this[int index] => children[index];
        
        /// <summary>
        /// Adds a child to the group
        /// </summary>
        /// <param name="node">The child to be added</param>
        public void AddChild(HierarchyNode node)
        {
            children.Add(node);
        }

        /// <summary>
        /// Removes a child from the group
        /// </summary>
        /// <param name="node">The child to be removed</param>
        public void RemoveChildren(HierarchyNode node)
        {
            children.Remove(node);
        }

        /// <summary>
        /// Removes child at the index
        /// </summary>
        /// <param name="index">The index of the child to be removed</param>
        public void RemoveChild(int index)
        {
            children.RemoveAt(index);
        }
    }
}