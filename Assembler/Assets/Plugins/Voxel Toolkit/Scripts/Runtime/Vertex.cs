using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelToolkit
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex
    {
        public readonly float3 Position;
        public readonly float3 Normal;
        public readonly Color32 Color;
        public readonly float4 UV;
        public readonly float MaterialIndex;
        public readonly float3 Location;
        public readonly int Parameters;

        public Vertex(float3 position, float3 normal, float4 uv, TransformedMaterial material, byte materialProperties, float3 location)
        {
            Position = position;
            Normal = normal;
            Color = material.Color;
            Parameters = material.Parameters;
            UV = uv;
            MaterialIndex = materialProperties;
            Location = location;
        }
    }
}