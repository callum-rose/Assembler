using UnityEngine;

namespace VoxelToolkit
{
    /// <summary>
    /// Represents the layer
    /// </summary>
    [System.Serializable]
    public class Layer : ScriptableObject
    {
        [SerializeField] private int id;

        [SerializeField] private string layerName;

        /// <summary>
        /// The id of the layer
        /// </summary>
        public int ID
        {
            get => id;
            set => id = value;
        }

        /// <summary>
        /// The name of the layer
        /// </summary>
        public string Name
        {
            get => layerName;
            set => layerName = value;
        }
    }
}