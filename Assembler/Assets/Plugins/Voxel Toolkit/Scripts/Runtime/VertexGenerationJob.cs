using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace VoxelToolkit
{
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast, OptimizeFor = OptimizeFor.Performance, DisableSafetyChecks = true)]
    public struct VertexGenerationJob : IJobParallelFor
    {
        [ReadOnly] public int VerticesCount;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeList<Quad> Quads;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<TransformedMaterial> Palette;
        [ReadOnly] public int UVsStart;
        [ReadOnly, NativeDisableParallelForRestriction] public NativeArray<float3x3> UVs;
        [ReadOnly] public float4 Shift;
        [ReadOnly] public float Scale;
        [ReadOnly] public float EdgeShift;
        [ReadOnly] public float OcclusionPower;

        [WriteOnly, NativeDisableParallelForRestriction] public NativeArray<Vertex> Vertices;
        
        public void Execute(int index)
        {
            var one = Scale;

            var quad = Quads[index];
            var material = Palette[quad.Material];

            var start = new float4(quad.StartX, quad.StartY, quad.StartZ, 0);
            var end = new float4(quad.EndX, quad.EndY, quad.EndZ, 0);
            var difference = end - start;
            var initialLocation = start + Shift;
            var position = initialLocation * Scale;
            var size = end - start;
            var scaledSize = (float4)size * Scale;

            var normal = new float4();
            var tangentA = new float4();
            var tangentB = new float4();
            var vertexShift = new float4();
            var directionA = new float4();
            var directionB = new float4();
            
            var transformation = UVs.Length == 0 ? float3x3.identity : UVs[UVsStart + index];
            var maxUV = 0.999f;
            var minUV = 0.001f;
            
            var entrySize = 32.0f;
            var spaceSize = 32.0f;
            var totalSize = entrySize * 16 + spaceSize * 15; 
            var spaceUVSize = spaceSize / totalSize;
            var occlusionCellSize = entrySize / totalSize;
            var effectiveOcclusionCellSize = quad.Occlusion == 0 ? 0.0f : occlusionCellSize;
            
            var occlusionCellX = quad.Occlusion / 16;
            var occlusionCellY = quad.Occlusion % 16;
            var occlusionStartX = quad.Occlusion == 0 ? occlusionCellSize * 0.5f : occlusionCellX * occlusionCellSize + occlusionCellX * spaceUVSize;
            var occlusionStartY = quad.Occlusion == 0 ? occlusionCellSize * 0.5f : occlusionCellY * occlusionCellSize + occlusionCellY * spaceUVSize;
            var uvA = math.mul(transformation, new float3(minUV, minUV, 1.0f)).xy;
            var uvB = math.mul(transformation, new float3(maxUV, minUV, 1.0f)).xy;
            var uvC = math.mul(transformation, new float3(maxUV, maxUV, 1.0f)).xy;
            var uvD = math.mul(transformation, new float3(minUV, maxUV, 1.0f)).xy;

            switch (quad.Orientation)
            {
                case FaceOrientation.Left:
                    normal = new float4(-1, 0, 0, 0);
                    tangentA = new float4(0.0f, 0.0f, scaledSize.z, 0.0f);
                    tangentB = new float4(0.0f, scaledSize.y, 0.0f, 0.0f);
                    directionA = new float4(0, 0, difference.z, 0);
                    directionB = new float4(0, difference.y, 0, 0);
                    (uvA, uvC) = (uvC, uvA);
                    break;
                case FaceOrientation.Right:
                    normal = new float4(1, 0, 0, 0);
                    tangentA = new float4(0.0f, scaledSize.y, 0.0f, 0.0f);
                    tangentB = new float4(0.0f, 0.0f, scaledSize.z, 0.0f);
                    vertexShift = new float4(one, 0.0f, 0.0f, 0.0f);
                    directionA = new float4(0, difference.y, 0, 0);
                    directionB = new float4(0, 0, difference.z, 0);
                    break;
                case FaceOrientation.Top:
                    normal = new float4(0, 1, 0, 0);
                    tangentA = new float4(0.0f, 0.0f, scaledSize.z, 0.0f);
                    tangentB = new float4(scaledSize.x, 0.0f, 0.0f, 0.0f);
                    vertexShift = new float4(0.0f, one, 0.0f, 0.0f);
                    directionA = new float4(0, 0, difference.z, 0);
                    directionB = new float4(difference.x, 0, 0, 0);
                    break;
                case FaceOrientation.Bottom:
                    normal = new float4(0, -1, 0, 0);
                    tangentA = new float4(scaledSize.x, 0.0f, 0.0f, 0.0f);
                    tangentB = new float4(0.0f, 0.0f, scaledSize.z, 0.0f);
                    directionA = new float4(difference.x, 0, 0, 0);
                    directionB = new float4(0, 0, difference.z, 0);
                    (uvA, uvC) = (uvC, uvA);
                    break;
                case FaceOrientation.Closer:
                    normal = new float4(0, 0, -1, 0);
                    tangentB = new float4(scaledSize.x, 0.0f, 0.0f, 0.0f);
                    tangentA = new float4(0.0f, scaledSize.y, 0.0f, 0.0f);
                    directionA = new float4(0, difference.y, 0, 0);
                    directionB = new float4(difference.x, 0, 0, 0);
                    break;
                case FaceOrientation.Further:
                    normal = new float4(0, 0, 1, 0);
                    tangentA = new float4(scaledSize.x, 0.0f, 0.0f, 0.0f);
                    tangentB = new float4(0.0f, scaledSize.y, 0.0f, 0.0f);
                    vertexShift = new float4(0.0f, 0.0f, one, 0.0f);
                    directionA = new float4(difference.x, 0, 0, 0);
                    directionB = new float4(0, difference.y, 0, 0);
                    (uvA, uvC) = (uvC, uvA);
                    break;
            }
             
            position += vertexShift;

            var tanAEdgeShift = tangentA * EdgeShift;
            var tanBEdgeShift = tangentB * EdgeShift;

            var vertexIndex = index * 4;
            var initialVoxelLocation = new float4(quad.StartX, quad.StartY, quad.StartZ, 0);
            var powerFactor = math.ceil(OcclusionPower * 10.0f);
            occlusionStartX += powerFactor;
            occlusionStartY += powerFactor;
            
            Vertices[vertexIndex++] = new Vertex((position + tangentA + tanAEdgeShift - tanBEdgeShift).xyz,
                normal.xyz, new float4(uvA, occlusionStartX, occlusionStartY), material, quad.Material, (initialVoxelLocation + directionA).xyz);
            
            Vertices[vertexIndex++] = new Vertex((position + tangentA + tangentB + tanAEdgeShift + tanBEdgeShift).xyz,
                normal.xyz, new float4(uvB, occlusionStartX + effectiveOcclusionCellSize, occlusionStartY), material, quad.Material, (initialVoxelLocation + directionA + directionB).xyz);
            
            Vertices[vertexIndex++] = new Vertex((position + tangentB + tanBEdgeShift - tanAEdgeShift).xyz,
                normal.xyz, new float4(uvC, occlusionStartX + effectiveOcclusionCellSize, occlusionStartY + effectiveOcclusionCellSize), material, quad.Material, (initialVoxelLocation + directionB).xyz);
            
            Vertices[vertexIndex] = new Vertex((position - tanAEdgeShift - tanBEdgeShift).xyz,
                normal.xyz, new float4(uvD, occlusionStartX, occlusionStartY + effectiveOcclusionCellSize), material, quad.Material, initialVoxelLocation.xyz);
        }
    }
}