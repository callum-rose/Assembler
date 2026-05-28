using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelToolkit
{
    public class TexturePackerResult : IDisposable
    {
        public readonly Texture2D Atlas;
        public readonly NativeArray<float3x3> UV;

        public TexturePackerResult(Texture2D atlas, NativeArray<float3x3> uv)
        {
            Atlas = atlas;
            UV = uv;
        }

        public void Dispose()
        {
            UV.Dispose();
        }
    }
}