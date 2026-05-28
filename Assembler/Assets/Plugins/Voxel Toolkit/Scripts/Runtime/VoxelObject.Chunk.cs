using System;
using Unity.Collections;

namespace VoxelToolkit
{
    public partial class VoxelObject
    {
        private struct Chunk : IDisposable
        {
            public NativeArray<Voxel> Voxels;

            public VertexSet OpaqueVertices;
            public VertexSet TransparentVertices;

            public bool IsDirty;
            
            public int VoxelsCount;

            private bool isValid;

            public bool IsValid => isValid;
            
            public Chunk(NativeArray<Voxel> voxels)
            {
                Voxels = voxels;
                VoxelsCount = 0;

                isValid = true;
                IsDirty = true;
                OpaqueVertices = new VertexSet(new NativeArray<Vertex>(0, Allocator.Persistent, NativeArrayOptions.UninitializedMemory), 0);
                TransparentVertices = new VertexSet(new NativeArray<Vertex>(0, Allocator.Persistent, NativeArrayOptions.UninitializedMemory), 0);
            }
            
            public void Dispose()
            {
                if (!isValid)
                    return;

                if (Voxels.IsCreated)
                    Voxels.Dispose();
                
                if (OpaqueVertices.Vertices.IsCreated)
                    OpaqueVertices.Vertices.Dispose();
                
                if (TransparentVertices.Vertices.IsCreated)
                    TransparentVertices.Vertices.Dispose();
                
                isValid = false;
            }
        }
    }
}