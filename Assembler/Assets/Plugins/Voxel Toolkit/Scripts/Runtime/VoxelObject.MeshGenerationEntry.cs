using Unity.Collections;

namespace VoxelToolkit
{
    public partial class VoxelObject
    {
        private struct MeshGenerationEntry
        {
            public readonly NativeArray<Vertex> Vertices;
            public readonly int StartIndex;
            public readonly int Count;
            public readonly int OpaqueVerticesCount;
            public readonly int TransparentVerticesCount => Count - OpaqueVerticesCount;
            
            public bool HasTransparentVertices => TransparentVerticesCount > 0;
            public bool HasOpaqueVertices => OpaqueVerticesCount > 0;

            public MeshGenerationEntry(NativeArray<Vertex> vertices, int startIndex, int count, int opaqueVerticesCount)
            {
                Vertices = vertices;
                StartIndex = startIndex;
                Count = count;
                OpaqueVerticesCount = opaqueVerticesCount;
            }
        }
    }
}