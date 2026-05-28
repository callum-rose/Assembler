using Unity.Mathematics;
using UnityEngine;

namespace VoxelToolkit
{
    public partial class VoxelObject
    {
        private struct QuadGenerationJobEntry
        {
            public readonly int3 Location;
            public readonly QuadGenerationJob Job;
            public VertexGenerationJob OpaqueVertexGenerationJob;
            public VertexGenerationJob TransparentVertexGenerationJob;

            public QuadGenerationJobEntry(QuadGenerationJob job, VertexGenerationJob opaqueVertexGenerationJob, VertexGenerationJob transparentVertexGenerationJob, int3 location)
            {
                Job = job;
                OpaqueVertexGenerationJob = opaqueVertexGenerationJob;
                TransparentVertexGenerationJob = transparentVertexGenerationJob;
                Location = location;
            }
        }
    }
}