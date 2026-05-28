using UnityEngine;

namespace VoxelToolkit
{
    /// <summary>
    /// The voxel representation for the asset data
    /// </summary>
    [System.Serializable]
    public struct VoxelData
    {
        [SerializeField] private Vector3Int position;
        [SerializeField] private int material;

        /// <summary>
        /// The position of the voxel
        /// </summary>
        public Vector3Int Position => position;
        
        /// <summary>
        /// The material of the voxel
        /// </summary>
        public int Material => material;

        public VoxelData(Vector3Int position, int material)
        {
            this.position = position;
            this.material = material;
        }
    }
}