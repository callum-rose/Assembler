using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace VoxelToolkit
{
    [Flags]
    public enum FaceOrientation : byte
    {
        None = 0,
        Left = 1,
        Right = 2,
        Top = 4,
        Bottom = 8,
        Closer = 16,
        Further = 32,
    }
    
    public struct Face
    {
        public FaceOrientation Faces;
        public readonly byte Material;
        public readonly int Occlusion;

        public Face(FaceOrientation faces, byte material, int occlusion)
        {
            Faces = faces;
            Material = material;
            Occlusion = occlusion;
        }
    }

    public struct Triplet<T> where T : struct
    {
        [ReadOnly] [NativeDisableParallelForRestriction] public T Negative;
        [ReadOnly] [NativeDisableParallelForRestriction] public T Zero;
        [ReadOnly] [NativeDisableParallelForRestriction] public T Positive;

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                switch (index)
                {
                   case -1:
                        return Negative;
                   case 0:
                       return Zero;
                   case 1:
                       return Positive;
                }

                return Zero;
            }

            set
            {
                switch (index)
                {
                    case -1:
                        Negative = value;
                        break;
                    case 0:
                        Zero = value;
                        break;
                    case 1:
                        Positive = value;
                        break;
                } 
            }
        }
    }

    public struct VoxelChunkSet
    {
        private Triplet<Triplet<Triplet<NativeArray<Voxel>>>> entries;

        public NativeArray<Voxel> this[int3 index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => entries[index.x][index.y][index.z];

            set
            {
                var xEntry = entries[index.x];
                var yEntry = xEntry[index.y];
                yEntry[index.z] = value;
                xEntry[index.y] = yEntry;
                entries[index.x] = xEntry;
            }
        }
    }
    
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
    public struct FacesGenerationJob : IJobParallelFor
    {
        public VoxelChunkSet ChunkSet;
        
        [NativeDisableParallelForRestriction] [ReadOnly] public NativeArray<TransformedMaterial> Palette;

        [ReadOnly] public int ChunkSize;
        [ReadOnly] public int LastChunkIndex;
        [ReadOnly] public int ChunkSizeSquared;
        [ReadOnly] public bool CalculateOcclusion;

        [NativeDisableParallelForRestriction] [WriteOnly] public NativeArray<Face> Faces;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (int Index, NativeArray<Voxel> TargetChunk) GetSamplePosition(int3 position, int3 shift)
        {
            var multiplier = new int4(ChunkSize, ChunkSizeSquared, 1, 0);
            var shiftedPosition = position + shift;
            var nextChunk = shiftedPosition >= ChunkSize;
            var previousChunk = shiftedPosition < 0; 
            var shiftedIndex = math.select(math.select(shiftedPosition, 0, nextChunk), LastChunkIndex, previousChunk);
            var chunkIndex = math.select(new int3(0, 0, 0), new int3(1, 1, 1), nextChunk) - math.select(new int3(0, 0, 0), new int3(1, 1, 1), previousChunk);

            return (math.dot(new int4(shiftedIndex, 0), multiplier), ChunkSet[chunkIndex]);
        }

        public void Execute(int index)
        {
            var multiplier = new int4(ChunkSize, ChunkSizeSquared, 1, 0);

            var y = index / ChunkSizeSquared;
            var leftover = index - (y * ChunkSizeSquared);
            var x = leftover / ChunkSize;
            var z = leftover - (x * ChunkSize);
            
            var center = math.dot(multiplier, new int4(x, y, z, 0));
            var centerVoxel = ChunkSet[new int3(0, 0, 0)][center];
            if (centerVoxel.Material == 0)
            {
                Faces[center] = new Face();
                return;
            }

            var higher = GetSamplePosition(new int3(x, y, z), new int3(0, 1, 0)); 
            var lower = GetSamplePosition(new int3(x, y, z), new int3(0, -1, 0)); 
            var closer = GetSamplePosition(new int3(x, y, z), new int3(0, 0, -1)); 
            var further = GetSamplePosition(new int3(x, y, z), new int3(0, 0, 1)); 
            var left = GetSamplePosition(new int3(x, y, z), new int3(-1, 0, 0)); 
            var right = GetSamplePosition(new int3(x, y, z), new int3(1, 0, 0)); 

            var centerMaterial = Palette[centerVoxel.Material];
            var centerMaterialType = centerMaterial.MaterialType;

            var voxelHigher = higher.TargetChunk[higher.Index];
            var higherMaterial = Palette[voxelHigher.Material].MaterialType;
            var voxelLower = lower.TargetChunk[lower.Index];
            var lowerMaterial = Palette[voxelLower.Material].MaterialType;
            var voxelCloser = closer.TargetChunk[closer.Index];
            var closerMaterial = Palette[voxelCloser.Material].MaterialType;
            var voxelFurther = further.TargetChunk[further.Index];
            var furtherMaterial = Palette[voxelFurther.Material].MaterialType;
            var voxelOnTheLeft = left.TargetChunk[left.Index];
            var leftMaterial = Palette[voxelOnTheLeft.Material].MaterialType;
            var voxelOnTheRight = right.TargetChunk[right.Index];
            var rightMaterial = Palette[voxelOnTheRight.Material].MaterialType;

            var faces = FaceOrientation.None;
            faces |= (FaceOrientation)math.select((int)FaceOrientation.Top, 0, higherMaterial == centerMaterialType);
            faces |= (FaceOrientation)math.select((int)FaceOrientation.Bottom, 0, lowerMaterial == centerMaterialType);
            faces |= (FaceOrientation)math.select((int)FaceOrientation.Closer, 0, closerMaterial == centerMaterialType);
            faces |= (FaceOrientation)math.select((int)FaceOrientation.Further, 0, furtherMaterial == centerMaterialType);
            faces |= (FaceOrientation)math.select((int)FaceOrientation.Left, 0, leftMaterial == centerMaterialType);
            faces |= (FaceOrientation)math.select((int)FaceOrientation.Right, 0, rightMaterial == centerMaterialType);

            var occlusion = (int)0;
            if (CalculateOcclusion)
            {
                for (var occlusionX = -1; occlusionX <= 1; occlusionX++)
                {
                    for (var occlusionY = -1; occlusionY <= 1; occlusionY++)
                    {
                        for (var occlusionZ = -1; occlusionZ <= 1; occlusionZ++)
                        {
                            var entry = GetSamplePosition(new int3(x, y, z), new int3(occlusionX, occlusionY, occlusionZ));
                            var voxel = entry.TargetChunk[entry.Index];
                            var value = voxel.Material == 0 ? 0 : 1;
                            
                            var shift = (occlusionX + 1) + (occlusionY + 1) * 3 + (occlusionZ + 1) * 9;
                            occlusion |= value << shift;
                        }
                    }
                }
            }

            Faces[center] = new Face(faces, centerVoxel.Material, occlusion);
        }
    }
}