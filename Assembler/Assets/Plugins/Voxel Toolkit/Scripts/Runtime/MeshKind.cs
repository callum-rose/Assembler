using System;

namespace VoxelToolkit
{
    /// <summary>
    /// The kind of the generated mesh
    /// </summary>
    [Flags]
    public enum MeshKind
    {
        /// <summary>
        /// The mesh is invalid
        /// </summary>
        Invalid = 0,
        /// <summary>
        /// The mesh is for opaque voxels
        /// </summary>
        Opaque = 1,
        /// <summary>
        /// The mesh is for transparent voxels
        /// </summary>
        Transparent = 2
    }
}