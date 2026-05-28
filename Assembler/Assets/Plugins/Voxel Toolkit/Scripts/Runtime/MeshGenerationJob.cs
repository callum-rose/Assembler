using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine.Rendering;

namespace VoxelToolkit
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
    public struct MeshGenerationJob : IJobParallelFor
    {
        [WriteOnly][NativeDisableParallelForRestriction] public NativeArray<UInt32> Indices32;
        [WriteOnly][NativeDisableParallelForRestriction] public NativeArray<UInt16> Indices16;

        public void Execute(int partIndex)
        {
            var vertexIndex = partIndex * 4;
            var indexPosition = partIndex * 6;
            
            var b = (UInt32)(vertexIndex + 1);
            var c = (UInt32)(vertexIndex + 2);

            var d = (UInt32)(vertexIndex + 2);
            var e = (UInt32)(vertexIndex + 3);

            Indices16[indexPosition] = (UInt16)vertexIndex;
            Indices32[indexPosition++] = (UInt32)vertexIndex;
            
            Indices16[indexPosition] = (UInt16)b;
            Indices32[indexPosition++] = b;
            
            Indices16[indexPosition] = (UInt16)c;
            Indices32[indexPosition++] = c;

            Indices16[indexPosition] = (UInt16)vertexIndex;
            Indices32[indexPosition++] = (UInt32)vertexIndex;
          
            Indices16[indexPosition] = (UInt16)d;
            Indices32[indexPosition++] = d;
            
            Indices16[indexPosition] = (UInt16)e;
            Indices32[indexPosition] = e;
        }
    }
}