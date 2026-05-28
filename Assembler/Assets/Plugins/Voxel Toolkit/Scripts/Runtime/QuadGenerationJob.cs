using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelToolkit
{
    public struct Quad
    {
        public readonly byte StartX, StartY, StartZ;
        public readonly byte EndX, EndY, EndZ;
        public readonly FaceOrientation Orientation;
        public readonly byte Material;
        public readonly byte Occlusion;

        public Quad(int3 start, int3 end, FaceOrientation orientation, byte material, byte occlusion)
        {
            Orientation = orientation;
            StartX = (byte)start.x;
            StartY = (byte)start.y;
            StartZ = (byte)start.z;
            
            EndX = (byte)end.x;
            EndY = (byte)end.y;
            EndZ = (byte)end.z;
            
            Material = material;
            Occlusion = occlusion;
        }
    }
    
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
    public struct QuadGenerationJob : IJob // TODO: Split to variants for 3 possible options
    {
        public NativeArray<Face> Faces;
        [ReadOnly] public int ChunkSize;
        [ReadOnly] public NativeArray<TransformedMaterial> Palette;

        [WriteOnly] public NativeList<Quad> OpaqueQuads;
        [WriteOnly] public NativeList<Quad> TransparentQuads;
        public bool IgnoreMaterials;
        public bool IgnoreHashes;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetLineLength(int4 position, int4 direction, int material, FaceOrientation orientation, int4 size, int originalOcclusion, int occlusionMask, int chunkSizeSquared)
        {
            var endPosition = position;
            var count = 0;
            var multiplier = new int4(ChunkSize, chunkSizeSquared, 1, 0);
            var originalMaterial = Palette[material];
            var originMaterialHash = originalMaterial.Hash;
            var ignoringBothProperties = IgnoreHashes && IgnoreMaterials;
            for (; math.all(endPosition < size); endPosition += direction)
            {
                var currentIndex = math.dot(multiplier, endPosition);
                var currentFace = Faces[currentIndex];
                            
                if ((currentFace.Faces & orientation) == FaceOrientation.None)
                    break;

                if (!IgnoreMaterials && currentFace.Material != material)
                    break;
                
                var materialData = Palette[currentFace.Material];
                if (!IgnoreHashes && originMaterialHash != materialData.Hash)
                    break;
                
                if (ignoringBothProperties && materialData.MaterialType != originalMaterial.MaterialType)
                    break;
                
                if ((currentFace.Occlusion & occlusionMask) != originalOcclusion)
                    break;

                count++;
            }

            return count;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte PackOcclusion8(int occlusion,
            int b0, int b1, int b2, int b3,
            int b4, int b5, int b6, int b7)
        {
            var o = (uint)occlusion;
            var packed  = ((o >> b0) & 1u) << 0;
            packed      |= ((o >> b1) & 1u) << 1;
            packed      |= ((o >> b2) & 1u) << 2;
            packed      |= ((o >> b3) & 1u) << 3;
            packed      |= ((o >> b4) & 1u) << 4;
            packed      |= ((o >> b5) & 1u) << 5;
            packed      |= ((o >> b6) & 1u) << 6;
            packed      |= ((o >> b7) & 1u) << 7;
            
            return (byte)packed;
        }
        
        private void ProcessOrientationGreedyOptimized(FaceOrientation expectedOrientation)
        {
            var firstDirection = int4.zero;
            var secondDirection = int4.zero;
            var orientationMask = ~expectedOrientation;
            var occlusionMask = 0;

            var ob0 = 0;
            var ob1 = 0;
            var ob2 = 0;
            var ob3 = 0;
            var ob4 = 0;
            var ob5 = 0;
            var ob6 = 0;
            var ob7 = 0;
            
            switch (expectedOrientation)
            {
                case FaceOrientation.Top:
                    firstDirection = new int4(0, 0, 1, 0);
                    secondDirection = new int4(1, 0, 0, 0);
                    occlusionMask = 117604800;
                    ob0 = 6;
                    ob1 = 7;
                    ob2 = 8;
                    ob3 = 17;
                    ob4 = 26;
                    ob5 = 25;
                    ob6 = 24;
                    ob7 = 15;
                    break;

                case FaceOrientation.Bottom:
                    firstDirection = new int4(0, 0, 1, 0);
                    secondDirection = new int4(1, 0, 0, 0);
                    occlusionMask = 1837575;
                    ob0 = 0;
                    ob1 = 9;
                    ob2 = 18;
                    ob3 = 19;
                    ob4 = 20;
                    ob5 = 11;
                    ob6 = 2;
                    ob7 = 1;
                    break;

                case FaceOrientation.Closer:
                    firstDirection = new int4(1, 0, 0, 0);
                    secondDirection = new int4(0, 1, 0, 0);
                    occlusionMask = 495;
                    ob0 = 0;
                    ob1 = 1;
                    ob2 = 2;
                    ob3 = 5;
                    ob4 = 8;
                    ob5 = 7;
                    ob6 = 6;
                    ob7 = 3;
                    break;

                case FaceOrientation.Further:
                    firstDirection = new int4(1, 0, 0, 0);
                    secondDirection = new int4(0, 1, 0, 0);
                    occlusionMask = 129761280;
                    ob0 = 18;
                    ob1 = 21;
                    ob2 = 24;
                    ob3 = 25;
                    ob4 = 26;
                    ob5 = 23;
                    ob6 = 20;
                    ob7 = 19;
                    break;

                case FaceOrientation.Left:
                    firstDirection = new int4(0, 0, 1, 0);
                    secondDirection = new int4(0, 1, 0, 0);
                    occlusionMask = 19169865;
                    ob0 = 0;
                    ob1 = 3;
                    ob2 = 6;
                    ob3 = 15;
                    ob4 = 24;
                    ob5 = 21;
                    ob6 = 18;
                    ob7 = 9;
                    break;

                case FaceOrientation.Right:
                    firstDirection = new int4(0, 0, 1, 0);
                    secondDirection = new int4(0, 1, 0, 0);
                    occlusionMask = 76679460;
                    ob0 = 2;
                    ob1 = 11;
                    ob2 = 20;
                    ob3 = 23;
                    ob4 = 26;
                    ob5 = 17;
                    ob6 = 8;
                    ob7 = 5;
                    break;
            }

            var chunkSizeSquared = ChunkSize * ChunkSize;

            var size = new int4(ChunkSize, ChunkSize, ChunkSize, ChunkSize);
            var multiplier = new int4(ChunkSize, chunkSizeSquared, 1, 0);
            
            for (var x = 0; x < ChunkSize; x++)
            {
                for (var y = 0; y < ChunkSize; y++)
                {
                    for (var z = 0; z < ChunkSize; z++)
                    {
                        var currentPosition = new int4(x, y, z, 0);
                        var center = math.dot(multiplier, currentPosition);
                        
                        var face = Faces[center];
                        if ((face.Faces & expectedOrientation) == FaceOrientation.None)
                            continue;

                        var thisOcclusion = face.Occlusion & occlusionMask;
                        var length = GetLineLength(new int4(x, y, z, 0),
                            firstDirection,
                            face.Material,
                            expectedOrientation,
                            size,
                            thisOcclusion,
                            occlusionMask,
                            chunkSizeSquared);

                        for (var index = 0; index < length; index++)
                        {
                            var position = currentPosition + firstDirection * index;
                            var currentIndex = math.dot(multiplier, position);
                            var faceToUpdate = Faces[currentIndex];
                            faceToUpdate.Faces &= orientationMask;
                            Faces[currentIndex] = faceToUpdate;
                        }

                        var finalPosition = currentPosition + secondDirection;
                        var secondaryShift = 1;
                        for (; math.all(finalPosition < size); finalPosition += secondDirection)
                        {
                            var currentLength = GetLineLength(finalPosition, 
                                firstDirection, 
                                face.Material, 
                                expectedOrientation, 
                                size, 
                                thisOcclusion,
                                occlusionMask,
                                chunkSizeSquared);
                            
                            if (currentLength < length)
                                break;
                            
                            for (var index = 0; index < length; index++)
                            {
                                var position = finalPosition + firstDirection * index;
                                var currentIndex = math.dot(multiplier, position);
                                var faceToUpdate = Faces[currentIndex];
                                faceToUpdate.Faces &= orientationMask;
                                Faces[currentIndex] = faceToUpdate;
                            }

                            secondaryShift++;
                        }
                        
                        var endPosition = currentPosition + firstDirection * length + secondDirection * secondaryShift;

                        var isTransparent = Palette[face.Material].MaterialType == MaterialType.Transparent;

                        var occlusion = PackOcclusion8(thisOcclusion, ob0, ob1, ob2, ob3, ob4, ob5, ob6, ob7);
                        var target = isTransparent ? TransparentQuads : OpaqueQuads;
                        target.Add(new Quad(currentPosition.xyz, endPosition.xyz, expectedOrientation, face.Material, occlusion));

                        face.Faces &= orientationMask;
                        Faces[center] = face;
                    }
                }
            }
        }

        public void Execute()
        {
            ProcessOrientationGreedyOptimized(FaceOrientation.Top);
            ProcessOrientationGreedyOptimized(FaceOrientation.Bottom);

            ProcessOrientationGreedyOptimized(FaceOrientation.Closer);
            ProcessOrientationGreedyOptimized(FaceOrientation.Further);

            ProcessOrientationGreedyOptimized(FaceOrientation.Left);
            ProcessOrientationGreedyOptimized(FaceOrientation.Right);
        }
    }
}