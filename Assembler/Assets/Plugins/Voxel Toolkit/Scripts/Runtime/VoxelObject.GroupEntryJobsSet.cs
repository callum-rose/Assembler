using System;
using System.Collections.Generic;

namespace VoxelToolkit
{
    public partial class VoxelObject
    {
        private struct GroupEntryJobsSet : IDisposable
        {
            public readonly MeshUpdateParameters Entry;
            public readonly List<FacesGenerationJob> FacesGenerationJobs;
            public readonly List<QuadGenerationJobEntry> QuadsGenerationJobs;
            public readonly List<MeshGenerationEntry> MeshGenerationJobs;
            public readonly List<TextureFillJob> TextureFillJobs;
            public readonly List<MaterialsFillJob> PropertiesFillJobs;

            public GroupEntryJobsSet(MeshUpdateParameters entry)
            {
                Entry = entry;
                FacesGenerationJobs = UnityEngine.Pool.ListPool<FacesGenerationJob>.Get();
                QuadsGenerationJobs = UnityEngine.Pool.ListPool<QuadGenerationJobEntry>.Get();
                MeshGenerationJobs = UnityEngine.Pool.ListPool<MeshGenerationEntry>.Get();
                TextureFillJobs = UnityEngine.Pool.ListPool<TextureFillJob>.Get();
                PropertiesFillJobs = UnityEngine.Pool.ListPool<MaterialsFillJob>.Get();
            }

            public void Dispose()
            {
                UnityEngine.Pool.ListPool<FacesGenerationJob>.Release(FacesGenerationJobs);
                UnityEngine.Pool.ListPool<QuadGenerationJobEntry>.Release(QuadsGenerationJobs);
                UnityEngine.Pool.ListPool<MeshGenerationEntry>.Release(MeshGenerationJobs);
                UnityEngine.Pool.ListPool<TextureFillJob>.Release(TextureFillJobs);
                UnityEngine.Pool.ListPool<MaterialsFillJob>.Release(PropertiesFillJobs);
            }
        }
    }
}