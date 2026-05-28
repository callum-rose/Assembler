using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelToolkit
{
    public struct TransformationEntry
    {
        public readonly byte TransformationIndex;
        public readonly float3x3 InverseTransformationMatrix;
        public readonly float3x3 TransformationMatrix;
        public readonly float3x3 UVTransformationMatrix;

        public TransformationEntry(byte transformationIndex, float3x3 inverseTransformationMatrix, float3x3 transformationMatrix)
        {
            TransformationIndex = transformationIndex;
            InverseTransformationMatrix = inverseTransformationMatrix;
            TransformationMatrix = transformationMatrix;
            UVTransformationMatrix = transformationMatrix;
            UVTransformationMatrix.c2 = math.sign(UVTransformationMatrix.c2);
        }
            
        public TransformationEntry(float3x3 inverseTransformationMatrix, float3x3 transformationMatrix)
        {
            TransformationIndex = 0;
            InverseTransformationMatrix = inverseTransformationMatrix;
            TransformationMatrix = transformationMatrix;
            UVTransformationMatrix = transformationMatrix;
            UVTransformationMatrix.c2 = math.sign(UVTransformationMatrix.c2);
        }
    }

    public struct TextureHandle : IDisposable
    {
        public struct TransformationIterator
        {
            public void Reset(ref TextureHandle handle)
            {
                handle.currentTransformationIndex = 0;
                handle.currentSubTransformationIndex = 0;
                RefreshTransformation(ref handle);
            }

            public bool MoveNext(ref TextureHandle handle)
            {
                var currentEntry = handle.transformations[handle.currentTransformationIndex];
                var transformationsCount = handle.transformations.Length;
                for (;
                     handle.currentTransformationIndex < transformationsCount &&
                     handle.transformations[handle.currentTransformationIndex].TransformationIndex ==
                     currentEntry.TransformationIndex;
                     handle.currentTransformationIndex++);

                handle.currentSubTransformationIndex = 0;
                if (handle.currentTransformationIndex < transformationsCount)
                {
                    RefreshTransformation(ref handle);
                    return true;
                }

                handle.currentTransformationIndex = 0;
                return false;
            }

            public bool MoveNextSubTransformation(ref TextureHandle handle)
            {
                var currentIndex = handle.currentTransformationIndex + handle.currentSubTransformationIndex;
                var currentEntry = handle.transformations[currentIndex];
                var nextIndex = currentIndex + 1;
                if (nextIndex >= handle.transformations.Length)
                {
                    handle.currentSubTransformationIndex = 0;
                    RefreshTransformation(ref handle);
                    return false;
                }

                var nextEntry = handle.transformations[nextIndex];
                if (nextEntry.TransformationIndex != currentEntry.TransformationIndex)
                {
                    handle.currentSubTransformationIndex = 0;
                    RefreshTransformation(ref handle);
                    return false;
                }

                handle.currentSubTransformationIndex++;
                RefreshTransformation(ref handle);
                return true;
            }

            private void RefreshTransformation(ref TextureHandle handle)
            {
                handle.transformation = handle.transformations[handle.currentTransformationIndex + handle.currentSubTransformationIndex];
            }
        }
        
        public readonly NativeArray<int> Data;
        private readonly NativeArray<TransformationEntry> transformations;
        private readonly int2 originalSize;
        public readonly long Hash;
        private TransformationEntry transformation;
        private int currentTransformationIndex;
        private int currentSubTransformationIndex;
        
        public TransformationEntry CurrentTransformation => transformation;

        public int2 OriginalSize
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => originalSize;
        }
        
        public int2 Size
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => math.abs(TransformDirection(originalSize));
        }

        public int this[int2 location]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Data[math.dot(InverseTransformIndex(location), new int2(1, originalSize.x))];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int2 InverseTransformIndex(int2 index) => (int2)math.round(math.mul(transformation.InverseTransformationMatrix, new int3(index, 1))).xy;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int2 TransformIndex(int2 index) => (int2)math.round(math.mul(transformation.InverseTransformationMatrix, new int3(index, 1))).xy;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int2 InverseTransformDirection(int2 index) => (int2)math.round(math.mul(transformation.InverseTransformationMatrix, new int3(index, 0))).xy;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int2 TransformDirection(int2 index) => (int2)math.round(math.mul(transformation.TransformationMatrix, new int3(index, 0))).xy;

        public TextureHandle(NativeArray<int> data, int2 originalSize, TextureOptimizationMode optimizationMode)
        {
            Data = data;
            this.originalSize = originalSize;
            currentTransformationIndex = 0;
            currentSubTransformationIndex = 0;

            transformation = new TransformationEntry();
            transformations = default;
            Hash = 0;
            if (optimizationMode == TextureOptimizationMode.None)
                return;

            Hash = CalculateHash();
            
            var transformationsCount = 1;
            switch (optimizationMode)
            {
                case TextureOptimizationMode.Rotations:
                    transformationsCount = 4;
                    break;
                case TextureOptimizationMode.Mirroring:
                    transformationsCount = 4;
                    break;
                case TextureOptimizationMode.All:
                    transformationsCount = 7;
                    break;
            }

            transformations = new NativeArray<TransformationEntry>(transformationsCount, Allocator.TempJob);
            var lastIndex = originalSize - 1;
            var transformationIndex = 0;
            transformations[transformationIndex++] = new TransformationEntry(
                new float3x3(
                    1, 0, 0,
                    0, 1, 0,
                    0, 0, 1),
                new float3x3(
                    1, 0, 0,
                    0, 1, 0,
                    0, 0, 1));

            if (optimizationMode == TextureOptimizationMode.All ||
                optimizationMode == TextureOptimizationMode.Rotations)
            {
                transformations[transformationIndex++] = new TransformationEntry(
                    new float3x3(
                        -1, 0, lastIndex.x,
                        0, -1, lastIndex.y,
                        0, 0, 1),
                    new float3x3(
                        -1, 0, lastIndex.x,
                        0, -1, lastIndex.y,
                        0, 0, 1));

                transformations[transformationIndex++] = new TransformationEntry(
                    new float3x3(
                        0, -1, lastIndex.x,
                        1, 0, 0,
                        0, 0, 1),
                    new float3x3(
                        0, 1, 0,
                        -1, 0, lastIndex.x,
                        0, 0, 1));

                transformations[transformationIndex++] = new TransformationEntry(new float3x3(
                        0, 1, 0,
                        -1, 0, lastIndex.y,
                        0, 0, 1),
                    new float3x3(
                        0, -1, lastIndex.y,
                        1, 0, 0,
                        0, 0, 1));
            }

            if (optimizationMode == TextureOptimizationMode.All ||
                optimizationMode == TextureOptimizationMode.Mirroring)
            {
                transformations[transformationIndex++] = new TransformationEntry(
                    new float3x3(
                        -1, 0, lastIndex.x,
                        0, 1, 0,
                        0, 0, 1),
                    new float3x3(
                        -1, 0, lastIndex.x,
                        0, 1, 0,
                        0, 0, 1));

                transformations[transformationIndex++] = new TransformationEntry(
                    new float3x3(
                        1, 0, 0,
                        0, -1, lastIndex.y,
                        0, 0, 1),
                    new float3x3(
                        1, 0, 0,
                        0, -1, lastIndex.y,
                        0, 0, 1));

                transformations[transformationIndex++] = new TransformationEntry(
                    new float3x3(
                        -1, 0, lastIndex.x,
                        0, -1, lastIndex.y,
                        0, 0, 1),
                    new float3x3(
                        -1, 0, lastIndex.x,
                        0, -1, lastIndex.y,
                        0, 0, 1));
            }

            var currentIndex = (byte)0;
            var previousSize = originalSize;
            for (var index = 0; index < transformations.Length; index++)
            {
                var transformationEntry = transformations[index];
                var size = (int2)math.abs(math.round(math.mul(transformationEntry.TransformationMatrix,
                    new int3(originalSize, 0))).xy);
                
                if (!size.Equals(previousSize))
                {
                    previousSize = size;
                    currentIndex++;
                }

                transformations[index] = new TransformationEntry(currentIndex,
                    transformationEntry.InverseTransformationMatrix, transformationEntry.TransformationMatrix);
            }

            transformation = transformations[0];
        }

        private long CalculateHash() => RollingHashCalculator.CalculateHash(Data, int2.zero, originalSize - 1, originalSize);

        public void Dispose()
        {
            if (transformations.IsCreated)
                transformations.Dispose();
        }
    }
}