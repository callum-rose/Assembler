using UnityEngine;

namespace VoxelToolkit
{
    /// <summary>
    /// Describes the generated mesh
    /// </summary>
    public struct MeshDescriptor
    { 
        /// <summary>
        /// The mesh attributes (Transparent, Opaque)
        /// </summary>
        public MeshKind MeshKind { get; internal set; }
        
        /// <summary>
        /// The resulting mesh
        /// </summary>
        public readonly Mesh Mesh;
        
        public MeshDescriptor(Mesh mesh)
        {
            Mesh = mesh;
            MeshKind = MeshKind.Invalid;
        }
    }
}