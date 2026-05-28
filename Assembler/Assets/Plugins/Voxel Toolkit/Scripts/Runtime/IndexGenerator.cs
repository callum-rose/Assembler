using System;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace VoxelToolkit
{
    public class IndexGenerator : ScriptableObject
    {
        private static IndexGenerator instance;

        private NativeArray<UInt16> values16;
        private NativeArray<UInt32> values32;
        private int currentSize = 120000;

        public static IndexGenerator Instance
        {
            get
            {
                if (instance != null)
                    return instance;

                instance = ScriptableObject.CreateInstance<IndexGenerator>();

                return instance;
            }
        }

        public void Resize(int size)
        {
            if (currentSize >= size)
                return;
            
            while (currentSize < size)
                currentSize *= 2;
            
            var newTarget16 = new NativeArray<UInt16>(currentSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var newTarget32 = new NativeArray<UInt32>(currentSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            values16.Dispose();
            values32.Dispose();

            values16 = newTarget16;
            values32 = newTarget32;
            
            var job = new MeshGenerationJob();

            job.Indices16 = values16;
            job.Indices32 = values32;
            
            job.Schedule(currentSize / 6, 64).Complete();
        }

        public NativeArray<UInt16> GetWithSizeOfU16(int size)
        {
            Resize(size);

            return values16;
        }

        public NativeArray<UInt32> GetWithSizeOfU32(int size)
        {
            Resize(size);

            return values32;
        }
        
        private void OnEnable()
        {
            values16 = new NativeArray<UInt16>(currentSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            values32 = new NativeArray<UInt32>(currentSize, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            var job = new MeshGenerationJob();

            job.Indices16 = values16;
            job.Indices32 = values32;
            
            job.Schedule(currentSize / 6, 256).Complete();
        }

        private void OnDisable()
        {
            if (values16.IsCreated)
                values16.Dispose();
            
            if (values32.IsCreated)
                values32.Dispose();
        }
    }
}