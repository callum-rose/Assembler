using Unity.Mathematics;
using UnityEngine.Rendering;

namespace VoxelToolkit
{
    /// <summary>
    /// The struct representing single entry in mesh update group
    /// </summary>
    public struct MeshUpdateParameters
    {
        public readonly IndexFormat IndexFormat;
        public readonly VoxelObject Object;
        public readonly float3 Shift;

        public MeshUpdateParameters(IndexFormat indexFormat, VoxelObject o, float3 shift)
        {
            IndexFormat = indexFormat;
            Object = o;
            Shift = shift;
        }
    }
}