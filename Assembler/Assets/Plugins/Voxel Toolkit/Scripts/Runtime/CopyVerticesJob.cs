using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace VoxelToolkit
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
    public struct CopyVerticesJob : IJob
    {
        [ReadOnly] public int StartPosition;
        [ReadOnly] public int Count;
        [ReadOnly] public NativeArray<Vertex> Source;
        [NativeDisableContainerSafetyRestriction] public NativeArray<Vertex> Result;

        public void Execute()
        {
            NativeArray<Vertex>.Copy(Source, 0, Result, StartPosition, Count);
        }
    }
}