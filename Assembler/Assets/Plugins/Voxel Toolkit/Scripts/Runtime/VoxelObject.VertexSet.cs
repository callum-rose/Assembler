using Unity.Collections;

namespace VoxelToolkit
{
    public partial class VoxelObject
    {
        private struct VertexSet
        {
            public readonly NativeArray<Vertex> Vertices;
            public int VerticesCount;

            public VertexSet(NativeArray<Vertex> vertices, int verticesCount)
            {
                Vertices = vertices;
                VerticesCount = verticesCount;
            }
        }
    }
}