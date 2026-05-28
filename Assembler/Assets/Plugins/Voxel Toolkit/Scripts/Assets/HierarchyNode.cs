using System;
using UnityEngine;

namespace VoxelToolkit
{
    /// <summary>
    /// The base hierarchy element
    /// </summary>
    [Serializable]
    public class HierarchyNode : ScriptableObject
    {
        [SerializeField] internal int id;

        /// <summary>
        /// Element's unique ID
        /// </summary>
        public int ID => id;

        /// <summary>
        /// The related objects of the node
        /// </summary>
        public virtual ScriptableObject[] RelatedObjects => Array.Empty<ScriptableObject>();
    }
}