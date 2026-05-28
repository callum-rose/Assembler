using System;

namespace VoxelToolkit
{
    /// <summary>
    /// The voxel data type
    /// </summary>
    public struct Voxel : IEquatable<Voxel>
    {
        /// <summary>
        /// The material of the voxel
        /// </summary>
        public readonly byte Material;

        public Voxel(byte material)
        {
            Material = (byte)((material + 1) % 256);
        }

        /// <summary>
        /// Compares two voxels
        /// </summary>
        /// <param name="other">The second voxel to compare with</param>
        /// <returns>Returns true if voxels are identical</returns>
        public bool Equals(Voxel other)
        {
            return Material == other.Material;
        }

        public override bool Equals(object obj)
        {
            return obj is Voxel other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Material.GetHashCode();
        }
    }
}