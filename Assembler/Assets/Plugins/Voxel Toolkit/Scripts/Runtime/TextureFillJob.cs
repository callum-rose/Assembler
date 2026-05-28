using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace VoxelToolkit
{
    public struct TextureDescriptor
    {
        public readonly int2 Size;
        public readonly int StartPosition;

        public TextureDescriptor(int2 size, int startPosition)
        {
            Size = size;
            StartPosition = startPosition;
        }
    }
    
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
    public struct MaterialsFillJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<TransformedMaterial> Palette;
        [NativeDisableParallelForRestriction] public NativeArray<int> Target;
        
        public void Execute(int index)
        {
            Target[index] = Palette[index].Parameters;
            Target[index + 256] = Palette[index].Color.RGBA;
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
    public struct TextureFillJob : IJob
    {
        [ReadOnly] public NativeList<Quad> Quads;
        [ReadOnly] public NativeArray<Voxel> Chunk;
        [ReadOnly] public NativeArray<TransformedMaterial> Palette;
        [ReadOnly] public int ChunkSize;
        [ReadOnly] public int ChunkSizeSquared;
        
        public NativeList<Color32> ColorTarget;
        public NativeList<TextureDescriptor> TextureDescriptors;

        public void Execute()
        {
            var multiplier = new int4(ChunkSize, ChunkSizeSquared, 1, 0);
            var position = 0;

            var count = Quads.Length;
            for (var index = 0; index < count; index++)
            {
                var quad = Quads[index];

                var start = new int4(quad.StartX, quad.StartY, quad.StartZ, 0);
                var end = new int4(quad.EndX, quad.EndY, quad.EndZ, 0);

                var tempStart = start;
                var tempEnd = end;

                start = math.min(tempStart, tempEnd);
                end = math.max(tempStart, tempEnd);
                
                var originalPosition = position;
                var size = new int2();
                
                switch (quad.Orientation)
                {
                    case FaceOrientation.Left:
                        size = new int2(quad.EndZ - quad.StartZ, quad.EndY - quad.StartY);
                        for (var y = end.y - 1; y >= start.y; y--)
                        {
                            for (var z = start.z; z < end.z; z++)
                            {
                                var voxelPosition = new int4(quad.StartX, y, z, 0);
                                var chunkIndex = math.dot(multiplier, voxelPosition);

                                var voxel = Chunk[chunkIndex];
                                var material = Palette[voxel.Material];
                                
                                var color = material.Color;
                                color.A = voxel.Material;
                                ColorTarget.Add(color);
                                position++;
                            }  
                        }   
                        break;
                    case FaceOrientation.Right:
                        size = new int2(quad.EndZ - quad.StartZ, quad.EndY - quad.StartY);
                        for (var y = end.y - 1; y >= start.y; y--)
                        {
                            for (var z = start.z; z < end.z; z++)
                            {
                                var voxelPosition = new int4(quad.StartX, y, z, 0);
                                var chunkIndex = math.dot(multiplier, voxelPosition);

                                var voxel = Chunk[chunkIndex];
                                var material = Palette[voxel.Material];
                                
                                var color = material.Color;
                                color.A = voxel.Material;
                                ColorTarget.Add(color);
                                position++;
                            }  
                        }   
                        break;
                    case FaceOrientation.Top:
                        size = new int2(quad.EndX - quad.StartX, quad.EndZ - quad.StartZ);
                        for (var z = end.z - 1; z >= start.z; z--)
                        {
                            for (var x = start.x; x < end.x; x++)
                            {
                                var voxelPosition = new int4(x, quad.StartY, z, 0);
                                var chunkIndex = math.dot(multiplier, voxelPosition);

                                var voxel = Chunk[chunkIndex];
                                var material = Palette[voxel.Material];
                                
                                var color = material.Color;
                                color.A = voxel.Material;
                                ColorTarget.Add(color);
                                position++;
                            }  
                        }
                        break;
                    case FaceOrientation.Bottom:
                        size = new int2(quad.EndX - quad.StartX, quad.EndZ - quad.StartZ);
                        
                        for (var z = end.z - 1; z >= start.z; z--)
                        {
                            for (var x = start.x; x < end.x; x++)
                            {
                                var voxelPosition = new int4(x, quad.StartY, z, 0);
                                var chunkIndex = math.dot(multiplier, voxelPosition);

                                var voxel = Chunk[chunkIndex];
                                var material = Palette[voxel.Material];
                                
                                var color = material.Color;
                                color.A = voxel.Material;
                                ColorTarget.Add(color);
                                position++;
                            }
                        }
                        break;
                    case FaceOrientation.Closer:
                        size = new int2(quad.EndX - quad.StartX, quad.EndY - quad.StartY);
                        for (var y = end.y - 1; y >= start.y; y--)
                        {
                            for (var x = start.x; x < end.x; x++)
                            {
                                var voxelPosition = new int4(x, y, quad.StartZ, 0);
                                var chunkIndex = math.dot(multiplier, voxelPosition);

                                var voxel = Chunk[chunkIndex];
                                var material = Palette[voxel.Material];
                                
                                var color = material.Color;
                                color.A = voxel.Material;
                                ColorTarget.Add(color);
                                position++;
                            }   
                        }
                        break;
                    case FaceOrientation.Further:
                        size = new int2(quad.EndX - quad.StartX, quad.EndY - quad.StartY);
                        for (var y = end.y - 1; y >= 0 ; y--)
                        {
                            for (var x = start.x; x < end.x; x++)
                            {
                                var voxelPosition = new int4(x, y, quad.StartZ, 0);
                                var chunkIndex = math.dot(multiplier, voxelPosition);

                                var voxel = Chunk[chunkIndex];
                                var material = Palette[voxel.Material];
                                
                                var color = material.Color;
                                color.A = voxel.Material;
                                ColorTarget.Add(color);
                                position++;
                            }   
                        }
                        break;
                }

                var singleColor = true;

                var referenceColor = ColorTarget[originalPosition];

                for (var pixelIndex = originalPosition + 1; pixelIndex < position; pixelIndex++)
                {
                    var current = ColorTarget[pixelIndex];
                    if (current.RGBA == referenceColor.RGBA)
                        continue;

                    singleColor = false;
                    break;
                }

                if (singleColor)
                {
                    ColorTarget.RemoveRange(originalPosition + 1, position - originalPosition - 1);
                    size = new int2(1, 1);
                    position = originalPosition + 1;
                }

                TextureDescriptors.Add(new TextureDescriptor(size, originalPosition));
            }
        }
    }
}