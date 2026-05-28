using System;
using System.Collections.Generic;
using UnityEngine;

namespace VoxelToolkit
{
    /// <summary>
    /// Represents the imported model
    /// </summary>
    [System.Serializable]
    [PreferBinarySerialization]
    public class Model : ScriptableObject
    {
        [SerializeField] private int id;
        [SerializeField] private Vector3Int size;
        [SerializeField] private List<VoxelData> voxels = new List<VoxelData>();
        [SerializeField] private VoxelAsset parentAsset;

        /// <summary>
        /// Voxels count representing the model
        /// </summary>
        public int VoxelsCount => voxels.Count;

        /// <summary>
        /// Parent asset of the model
        /// </summary>
        public VoxelAsset ParentAsset
        {
            get => parentAsset;
            set
            {
                if (parentAsset != null)
                    throw new Exception("Can't change the parent asset after it being set");
                
                parentAsset = value;
            }
        }

        /// <summary>
        /// The unique id of the model
        /// </summary>
        public int ID
        {
            get => id;
            set => id = value;
        }

        /// <summary>
        /// The model boundaries size
        /// </summary>
        public Vector3Int Size
        {
            get => size;
            set => size = value;
        }

        /// <summary>
        /// Provides access to the voxel by the index
        /// </summary>
        /// <param name="index">The index of the voxel to be accessed</param>
        public VoxelData this[int index] => voxels[index];

        /// <summary>
        /// Sets the voxels to the model
        /// </summary>
        /// <param name="voxels">The list of the voxels to be set</param>
        public void SetVoxels(List<VoxelData> voxels)
        {
            this.voxels.Clear();
            this.voxels.AddRange(voxels);
        }

        internal void RemapMaterials(Dictionary<byte, byte> remapping)
        {
            for (var index = 0; index < voxels.Count; index++)
            {
                var voxel = voxels[index];
                voxel = new VoxelData(voxel.Position, remapping[(byte)(voxel.Material + 1)]);
                voxels[index] = voxel;
            }
        }
    }
}