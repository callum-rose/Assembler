using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace VoxelToolkit
{
    /// <summary>
    /// The class responsible for a voxel meshes generation
    /// </summary>
    public partial class VoxelObject : IDisposable
    {
        private readonly int chunkSize = 16;
        private Chunk emptyChunk;

        private bool isDisposed = false;

        private List<MeshDescriptor> meshes = new List<MeshDescriptor>();
        
        private Chunk EmptyChunk
        {
            get
            {
                if (!emptyChunk.IsValid)
                    emptyChunk = new Chunk(new NativeArray<Voxel>(chunkSize * chunkSize * chunkSize, Allocator.Persistent));

                return emptyChunk;
            }
        }

        /// <summary>
        /// The count of the generated meshes
        /// </summary>
        public int MeshesCount => meshes.Count;
        
        /// <summary>
        /// Object disposal status
        /// </summary>
        public bool IsDisposed => isDisposed;

        /// <summary>
        /// The strength of applied AO. If 0 no AO will be applied
        /// </summary>
        public float AOStrength
        {
            get => aoStrength;
            set
            {
                if (Mathf.Approximately(aoStrength, value))
                    return;

                aoStrength = value;
                if (aoStrength < 0.0f)
                    aoStrength = 0.0f;
                
                MarkDirty();
            }
        }

        private float aoStrength = 1.0f;
        private float hueShift = 0.0f;
        private float saturation = 1.3f;
        private float brightness = 0.0f;

        /// <summary>
        /// The hue shift to be applied to the materials
        /// </summary>
        public float HueShift
        {
            get => hueShift;
            set
            {
                if (Mathf.Approximately(hueShift, value))
                    return;

                hueShift = value;
                materialsAreDirty = true;
                MarkDirty();
            }
        }
        /// <summary>
        /// Saturation to be applied to the materials
        /// </summary>
        public float Saturation 
        {
            get => saturation;
            set
            {
                if (Mathf.Approximately(saturation, value))
                    return;

                saturation = value;
                materialsAreDirty = true;
                MarkDirty();
            }
        }
        /// <summary>
        /// Brightness to be applied to the materials
        /// </summary>
        public float Brightness
        {
            get => brightness;
            set
            {
                if (Mathf.Approximately(brightness, value))
                    return;

                brightness = value;
                materialsAreDirty = true;
                MarkDirty();
            }
        }

        private float scale = 1.0f;
        private float transparentEdgeShift = 0.0f;
        private float opaqueEdgeShift = 0.0f;
        private int3 size;
        private Chunk[][][] chunks;
        private Material[] originalPalette;
        private bool materialsAreDirty = false;
        private NativeArray<TransformedMaterial> palette;
        private MeshGenerationApproach meshGenerationApproach;
        private MaterialPropertiesEmbeddingMode materialPropertiesEmbeddingMode;
        private TextureOptimizationMode textureOptimizationMode;
        private FaceOrientation objectSideIgnoreMask;

        /// <summary>
        /// Faces of the corresponding sides that are located at the end of the object volume will be ignored
        /// </summary>
        public FaceOrientation ObjectSideIgnoreMask
        {
            get => FaceOrientation.Bottom;//objectSideIgnoreMask;
            set
            {
                if (objectSideIgnoreMask == value)
                    return;

                objectSideIgnoreMask = value;
                
                MarkDirty(); // TODO: Mark only perimeter
            }
        }

        public MaterialPropertiesEmbeddingMode MaterialPropertiesEmbeddingMode
        {
            get => materialPropertiesEmbeddingMode;

            set
            {
                if (materialPropertiesEmbeddingMode == value)
                    return;
                
                materialPropertiesEmbeddingMode = value;
                MarkDirty();
            }
        }

        /// <summary>
        /// Mesh generation approach to use
        /// </summary>
        public MeshGenerationApproach MeshGenerationApproach
        {
            get => meshGenerationApproach;
            set
            {
                if (meshGenerationApproach == value)
                    return;
                
                meshGenerationApproach = value;
                
                MarkDirty();
            }
        }

        /// <summary>
        /// Texture optimization mode to use
        /// </summary>
        public TextureOptimizationMode TextureOptimizationMode
        {
            get => textureOptimizationMode;
            set
            {
                if (textureOptimizationMode == value)
                    return;
                
                textureOptimizationMode = value;
                
                MarkDirty();
            }
        }
        
        /// <summary>
        /// The scale of the voxel object
        /// </summary>
        public float Scale
        {
            get => scale;
            set
            {
                if (Mathf.Approximately(scale, value))
                    return;

                scale = value;
                MarkDirty();
            }
        }

        /// <summary>
        /// The shift of the edge of the voxel object applied for opaque objects
        /// </summary>
        public float OpaqueEdgeShift
        {
            get => opaqueEdgeShift;
            set
            {
                if (Mathf.Approximately(opaqueEdgeShift, value))
                    return;

                opaqueEdgeShift = value;
                MarkDirty();
            }
        }
        
        /// <summary>
        /// The shift of the edge of the voxel object applied for transparent objects
        /// </summary>
        public float TransparentEdgeShift
        {
            get => transparentEdgeShift;
            set
            {
                if (Mathf.Approximately(transparentEdgeShift, value))
                    return;
                
                transparentEdgeShift = value; 
                MarkDirty();
            }
        }

        /// <summary>
        /// Provides the size of the voxel object
        /// </summary>
        public int3 Size => size;

        /// <summary>
        /// The count of the chunks for the current voxel object
        /// </summary>
        public Vector3Int ChunksCount
        {
            get
            {
                var xChunksCount = Mathf.CeilToInt((float)size.x / chunkSize);
                var yChunksCount = Mathf.CeilToInt((float)size.y / chunkSize);
                var zChunksCount = Mathf.CeilToInt((float)size.z / chunkSize);

                return new Vector3Int(xChunksCount, yChunksCount, zChunksCount);
            }
        }
        
        /// <summary>
        /// Creates the voxel object of the given size and chunks size
        /// </summary>
        /// <param name="size">The size of the voxel object</param>
        /// <param name="palette">The palette to be used for the mesh generation</param>
        /// <param name="chunkSize">The chunk size of the voxel object</param>
        public VoxelObject(Vector3Int size, ReadonlyArray<Material> palette, int chunkSize)
        {
            if (palette.Length > 256)
                throw new ArgumentException($"Max palette size is 256 but provided the one with size of {palette.Length}");

            if (chunkSize <= 0 || chunkSize > 255)
                throw new ArgumentException($"Chunk size should in in range [0, 255] but got {chunkSize}");

            if (size.x <= 0 || size.y <= 0 || size.z <= 0)
                throw new ArgumentException($"All components of the size vector should be greater than zero but got {size}");

            this.originalPalette = palette.ToArray();
            this.chunkSize = chunkSize;
            this.palette = new NativeArray<TransformedMaterial>(palette.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            materialsAreDirty = true;
            
            this.size = new int3(size.x, size.y, size.z);
            var chunkCount = ChunksCount;

            chunks = new Chunk[chunkCount.x][][];
            for (var xIndex = 0; xIndex < chunkCount.x; xIndex++)
            {
                chunks[xIndex] = new Chunk[chunkCount.y][];
                for (var yIndex = 0; yIndex < chunkCount.y; yIndex++)
                    chunks[xIndex][yIndex] = new Chunk[chunkCount.z];
            }
        }
        
        /// <summary>
        /// Creates the voxel object of the given size and chunks size
        /// </summary>
        /// <param name="size">The size of the voxel object</param>
        /// <param name="chunkSize">The chunk size of the voxel object</param>
        public VoxelObject(Vector3Int size, int chunkSize)
        {
            if (chunkSize <= 0 || chunkSize > 255)
                throw new ArgumentException($"Chunk size should in in range [0, 255] but got {chunkSize}");

            if (size.x <= 0 || size.y <= 0 || size.z <= 0)
                throw new ArgumentException($"All components of the size vector should be greater than zero but got {size}");
            
            this.chunkSize = chunkSize;
            originalPalette = new Material[256];
            for (var index = 0; index < originalPalette.Length; index++)
                originalPalette[index] = Material.Base;
            
            palette = new NativeArray<TransformedMaterial>(256, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            materialsAreDirty = true;
            
            this.size = new int3(size.x, size.y, size.z);
            var chunkCount = ChunksCount;

            chunks = new Chunk[chunkCount.x][][];
            for (var xIndex = 0; xIndex < chunkCount.x; xIndex++)
            {
                chunks[xIndex] = new Chunk[chunkCount.y][];
                for (var yIndex = 0; yIndex < chunkCount.y; yIndex++)
                    chunks[xIndex][yIndex] = new Chunk[chunkCount.z];
            }
        }

        ~VoxelObject()
        {
            Dispose();
        }

        /// <summary>
        /// Sets the material for the voxel object
        /// </summary>
        /// <param name="index">The index of the material to be set</param>
        /// <param name="material">The material to be set</param>
        public void SetMaterial(byte index, Material material)
        {
            originalPalette[index] = material;
            palette[index] = new TransformedMaterial(index, material, HueShift, Saturation, Brightness);
            for (var x = 0; x < chunks.Length; x++)
            {
                var xChunks = chunks[x];
                for (var y = 0; y < xChunks.Length; y++)
                {
                    var yChunks = xChunks[y];
                    for (var z = 0; z < yChunks.Length; z++)
                    {
                        var chunk = yChunks[z];
                        if (chunk.VoxelsCount == 0)
                            continue;

                        chunk.IsDirty = true;
                        chunks[x][y][z] = chunk;
                    }
                }
            }
        }
        
        private void CheckDisposed()
        {
            if (isDisposed)
                throw new ObjectDisposedException("Voxel object is disposed but you are trying to use it");
        }

        /// <summary>
        /// Get access to the generated mesh
        /// </summary>
        /// <param name="index">The index of the mesh to obtain</param>
        /// <returns>Mesh descriptor of the requested mesh</returns>
        public MeshDescriptor GetMesh(int index)
        {
            CheckDisposed();
            return meshes[index];
        }

        /// <summary>
        /// Copies the voxel object from one to another
        /// </summary>
        /// <param name="other">The object to copy to</param>
        public void CopyTo(VoxelObject other)
        {
            CheckDisposed();
            NativeArray<TransformedMaterial>.Copy(palette, other.palette);
            var sizeToIterate = math.min(size, other.size);
            for (var x = 0; x < sizeToIterate.x; x++)
                for (var y = 0; y < sizeToIterate.y; y++)
                    for (var z = 0; z < sizeToIterate.z; z++)
                        other[new int3(x, y, z)] = this[new int3(x, y, z)];
        }
        
        /// <summary>
        /// Creates voxel object directly from voxel model
        /// </summary>
        /// <param name="model">The voxel model voxel object to be created from</param>
        /// <param name="palette">The palette used for mesh generation</param>
        /// <param name="chunkSize">The chunk size of the object to be generated</param>
        /// <returns></returns>
        public static VoxelObject CreateFromModel(Model model, ReadonlyArray<Material> palette, int chunkSize)
        {
            var voxelObject = new VoxelObject(model.Size, palette, chunkSize);
            for (var index = 0; index < model.VoxelsCount; index++)
                voxelObject[model[index].Position] = new Voxel((byte)model[index].Material);
            
            return voxelObject;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private (int3 Chunk, int Index, int3 InsideChunkPosition) GetChunkLocation(int3 position)
        {
            var chunkIndex = position / chunkSize;
            var chunkPosition = position - chunkIndex * chunkSize;
            return (chunkIndex, chunkSize * chunkSize * chunkPosition.y + chunkSize * chunkPosition.x + chunkPosition.z, chunkPosition);
        }

        /// <summary>
        /// Checks if the voxel position belongs to the voxel object volume
        /// </summary>
        /// <param name="value">The position to check</param>
        /// <returns>True if the position is inside the voxel object volume. False if not.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsInVolume(Vector3Int value) => IsInVolume(new int3(value.x, value.y, value.z));

        /// <summary>
        /// Checks if the voxel position belongs to the voxel object volume
        /// </summary>
        /// <param name="value">The position to check</param>
        /// <returns>True if the position is inside the voxel object volume. False if not.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsInVolume(int3 value)
        {
            return math.all(value >= 0) && math.all(value < size);
        }

        /// <summary>
        /// Provides access to a specific voxel of the voxel object
        /// </summary>
        /// <param name="position">The position of the voxel</param>
        public Voxel this[Vector3Int position]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => this[new int3(position.x, position.y, position.z)];
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set => this[new int3(position.x, position.y, position.z)] = value;
        }
        
        /// <summary>
        /// Provides access to a specific voxel of the voxel object
        /// </summary>
        /// <param name="position">The position of the voxel</param>
        public Voxel this[int3 position]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                CheckDisposed();
                var (chunk, index, insideChunkPosition) = GetChunkLocation(position);
                var chunkData = chunks[chunk.x][chunk.y][chunk.z];
                return chunkData.Voxels.IsCreated ? chunkData.Voxels[index] : new Voxel();
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                CheckDisposed();
                var (chunk, index, insideChunkPosition) = GetChunkLocation(position);
                if (math.any(position < 0) ||
                    math.any(position >= size))
                    throw new ArgumentException($"Trying to set the voxel outside of the object boundaries {position}");
                
                var chunkData = chunks[chunk.x][chunk.y][chunk.z];
                if (!chunkData.IsValid && value.Material == 0)
                    return;
                
                if (!chunkData.IsValid)
                {
                    chunkData = new Chunk(new NativeArray<Voxel>(chunkSize * chunkSize * chunkSize,
                        Allocator.Persistent));
                    
                    chunks[chunk.x][chunk.y][chunk.z] = chunkData;
                }

                var current = chunkData.Voxels[index];
                if (current.Equals(value))
                    return;

                var wasEmpty = current.Material == 0;
                chunkData.IsDirty = true;
                if (value.Material == 0)
                {
                    if (wasEmpty)
                        return;
                    
                    chunkData.VoxelsCount--;
                    if (chunkData.VoxelsCount != 0)
                        chunkData.Voxels[index] = value;
                }
                else
                {
                    chunkData.Voxels[index] = value;
                    if (wasEmpty)
                        chunkData.VoxelsCount++;
                }

                chunks[chunk.x][chunk.y][chunk.z] = chunkData;
                
                if (insideChunkPosition.x == 0)
                    MarkDirty(chunk + new int3(-1, 0, 0));
                
                if (insideChunkPosition.x == chunkSize - 1)
                    MarkDirty(chunk + new int3(1, 0, 0));
                
                if (insideChunkPosition.y == 0)
                    MarkDirty(chunk + new int3(0, -1, 0));
                
                if (insideChunkPosition.y == chunkSize - 1)
                    MarkDirty(chunk + new int3(0, 1, 0));
                
                if (insideChunkPosition.z == 0)
                    MarkDirty(chunk + new int3(0, 0, -1));
                
                if (insideChunkPosition.z == chunkSize - 1)
                    MarkDirty(chunk + new int3(0, 0, 1));
            }
        }

        private void MarkDirty()
        {
            for (var xIndex = 0; xIndex < ChunksCount.x; xIndex++)
                for (var yIndex = 0; yIndex < ChunksCount.y; yIndex++)
                    for (var zIndex = 0; zIndex < ChunksCount.z; zIndex++)
                        MarkDirty(new int3(xIndex, yIndex, zIndex));
        }
        
        private void MarkDirty(int3 position)
        {
            var count = ChunksCount;
            if (position.x < 0 || position.x >= count.x)
                return;
            
            if (position.y < 0 || position.y >= count.y)
                return;
            
            if (position.z < 0 || position.z >= count.z)
                return;

            var chunk = chunks[position.x][position.y][position.z];
            chunk.IsDirty = true;
            chunks[position.x][position.y][position.z] = chunk;
        }

        private (Chunk Chunk, bool Forening) GetChunk(int3 position, bool includeNotCreated)
        {
            var count = ChunksCount;
            if (position.x < 0 || position.x >= count.x)
                return (EmptyChunk, true);
            
            if (position.y < 0 || position.y >= count.y)
                return (EmptyChunk, true);
            
            if (position.z < 0 || position.z >= count.z)
                return (EmptyChunk, true);

            var result = chunks[position.x][position.y][position.z];
            return result.Voxels.IsCreated || includeNotCreated ? (result, false) : (EmptyChunk, false);
        }
        
#if UNITY_EDITOR
        /// <summary>
        /// Generates lightmap UV set for the object
        /// </summary>
        public void GenerateLightmapUV()
        {
            CheckDisposed();
            var settings = new UnityEditor.UnwrapParam();
            UnityEditor.UnwrapParam.SetDefaults(out settings);
            settings.packMargin = 0.04f;
            foreach (var meshDescriptor in meshes)
                UnityEditor.Unwrapping.GenerateSecondaryUVSet(meshDescriptor.Mesh, settings);
        }
#endif
        
        private static void WaitForAllHandles(List<JobHandle> handles)
        {
            var handlesToWait = new NativeArray<JobHandle>(handles.Count, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            for (var index = 0; index < handles.Count; index++)
                handlesToWait[index] = handles[index];
            
            handles.Clear();

            JobHandle.CompleteAll(handlesToWait);

            handlesToWait.Dispose();
        }

        private static void CheckSetSize(ref VertexSet set, int size)
        {
            if (set.Vertices.Length >= size)
                return;

            set.Vertices.Dispose();
            set = new VertexSet(new NativeArray<Vertex>(size, Allocator.Persistent), 0);
        }

        /// <summary>
        /// Updates voxel objects in a group which allows to combine textures and make shared materials
        /// </summary>
        /// <param name="group">The set of voxel objects to update in a group</param>
        /// <param name="optimizationMode">Optimization mode to be taken</param>
        public static MeshGroupUpdateResult UpdateVoxelObjectsGroup(List<MeshUpdateParameters> group, TextureOptimizationMode optimizationMode)
        {
            var jobsByEntries = UnityEngine.Pool.DictionaryPool<VoxelObject, GroupEntryJobsSet>.Get();
            var jobHandles = UnityEngine.Pool.ListPool<JobHandle>.Get();
            var filteredGroup = UnityEngine.Pool.DictionaryPool<VoxelObject, MeshUpdateParameters>.Get();

            foreach (var groupEntry in group)
            {
                if (groupEntry.Object == null)
                    continue;

                filteredGroup.TryAdd(groupEntry.Object, groupEntry);
            }

            foreach (var groupEntry in filteredGroup)
            {
                var jobs = new GroupEntryJobsSet(groupEntry.Value);
                if (groupEntry.Key == null)
                    continue;

                jobsByEntries.Add(groupEntry.Key, jobs);

                if (groupEntry.Key.materialsAreDirty)
                {
                    for (var index = 0; index < groupEntry.Key.palette.Length; index++)
                    {
                        groupEntry.Key.palette[index] =
                            new TransformedMaterial((byte)index, groupEntry.Key.originalPalette[index],
                                groupEntry.Key.HueShift, groupEntry.Key.Saturation, groupEntry.Key.Brightness);
                    }

                    groupEntry.Key.materialsAreDirty = false;
                }

                groupEntry.Key.CheckDisposed();
                
                var chunkVolume = groupEntry.Key.chunkSize * groupEntry.Key.chunkSize *
                                  groupEntry.Key.chunkSize;
                var facesCount = chunkVolume;
                var dirtyChunksCount = 0;
                for (var x = 0; x < groupEntry.Key.chunks.Length; x++)
                {
                    var xChunks = groupEntry.Key.chunks[x];
                    for (var y = 0; y < xChunks.Length; y++)
                    {
                        var yChunks = xChunks[y];
                        for (var z = 0; z < yChunks.Length; z++)
                        {
                            var chunk = yChunks[z];

                            if (!chunk.IsDirty)
                                continue;

                            dirtyChunksCount++;

                            chunk.IsDirty = false;
                            groupEntry.Key.chunks[x][y][z] = chunk;

                            var faceGenerationJob = new FacesGenerationJob();
                            faceGenerationJob.CalculateOcclusion = groupEntry.Key.AOStrength > 0.01f;

                            var chunkSet = new VoxelChunkSet();
                            for (var shiftX = -1; shiftX <= 1; shiftX++)
                            {
                                for (var shiftY = -1; shiftY <= 1; shiftY++)
                                {
                                    for (var shiftZ = -1; shiftZ <= 1; shiftZ++)
                                    {
                                        chunkSet[new int3(shiftX, shiftY, shiftZ)] =
                                            groupEntry.Key.GetChunk(
                                                new int3(x, y, z) + new int3(shiftX, shiftY, shiftZ), false).Chunk.Voxels;
                                    }
                                }
                            }

                            faceGenerationJob.ChunkSet = chunkSet;
                            faceGenerationJob.Palette = groupEntry.Key.palette;
                            faceGenerationJob.Faces = new NativeArray<Face>(facesCount, Allocator.TempJob,
                                NativeArrayOptions.UninitializedMemory);
                            faceGenerationJob.ChunkSize = groupEntry.Key.chunkSize;
                            faceGenerationJob.LastChunkIndex = faceGenerationJob.ChunkSize - 1;
                            faceGenerationJob.ChunkSizeSquared =
                                groupEntry.Key.chunkSize * groupEntry.Key.chunkSize;

                            var quadGenerationJob = new QuadGenerationJob();
                            
                            quadGenerationJob.IgnoreMaterials = groupEntry.Key.meshGenerationApproach == 
                                                                MeshGenerationApproach.Textured;
                            
                            quadGenerationJob.IgnoreHashes = groupEntry.Key.materialPropertiesEmbeddingMode ==
                                                             MaterialPropertiesEmbeddingMode.Texture;
                            
                            quadGenerationJob.Faces = faceGenerationJob.Faces;
                            quadGenerationJob.TransparentQuads = new NativeList<Quad>(512, Allocator.TempJob);
                            quadGenerationJob.Palette = groupEntry.Key.palette;
                            quadGenerationJob.OpaqueQuads = new NativeList<Quad>(512, Allocator.TempJob);
                            quadGenerationJob.ChunkSize = groupEntry.Key.chunkSize;

                            var opaqueVertexGenerationJob = new VertexGenerationJob();
                            opaqueVertexGenerationJob.OcclusionPower = groupEntry.Key.AOStrength;
                            opaqueVertexGenerationJob.EdgeShift = groupEntry.Key.opaqueEdgeShift;

                            opaqueVertexGenerationJob.Palette = groupEntry.Key.palette;
                            opaqueVertexGenerationJob.Quads = quadGenerationJob.OpaqueQuads;

                            opaqueVertexGenerationJob.Shift = new Vector4(x, y, z) * groupEntry.Key.chunkSize;
                            opaqueVertexGenerationJob.Shift += new float4(groupEntry.Value.Shift, 0.0f);

                            opaqueVertexGenerationJob.Scale = groupEntry.Key.scale;

                            var transparentVertexGenerationJob = opaqueVertexGenerationJob;
                            transparentVertexGenerationJob.EdgeShift = groupEntry.Key.transparentEdgeShift;
                            transparentVertexGenerationJob.Quads = quadGenerationJob.TransparentQuads;

                            jobs.FacesGenerationJobs.Add(faceGenerationJob);
                            jobs.QuadsGenerationJobs.Add(new QuadGenerationJobEntry(quadGenerationJob,
                                opaqueVertexGenerationJob,
                                transparentVertexGenerationJob,
                                new int3(x, y, z)));

                            var faceJob = faceGenerationJob.Schedule(chunkVolume, 256);
                            jobHandles.Add(faceJob);

                            var quadsJob = quadGenerationJob.Schedule(faceJob);
                            jobHandles.Add(quadsJob);

                            if (groupEntry.Key.meshGenerationApproach != MeshGenerationApproach.Textured)
                                continue;

                            for (var countIndex = 0; countIndex < 2; countIndex++)
                            {
                                var textureFillJob = new TextureFillJob();

                                textureFillJob.Chunk = chunk.Voxels.IsCreated ? chunk.Voxels : groupEntry.Key.emptyChunk.Voxels;
                                textureFillJob.Palette = groupEntry.Key.palette;
                                textureFillJob.TextureDescriptors =
                                    new NativeList<TextureDescriptor>(4096, Allocator.TempJob);
                                textureFillJob.Quads = countIndex == 0
                                    ? quadGenerationJob.OpaqueQuads
                                    : quadGenerationJob.TransparentQuads;

                                textureFillJob.ColorTarget = new NativeList<Color32>(128, Allocator.TempJob);

                                textureFillJob.ChunkSize = groupEntry.Key.chunkSize;
                                textureFillJob.ChunkSizeSquared =
                                    groupEntry.Key.chunkSize * groupEntry.Key.chunkSize;

                                jobHandles.Add(textureFillJob.Schedule(quadsJob));

                                jobs.TextureFillJobs.Add(textureFillJob);
                            }
                            
                            if (groupEntry.Key.materialPropertiesEmbeddingMode == MaterialPropertiesEmbeddingMode.Vertex)
                                continue;

                            var materialFillJob = new MaterialsFillJob();

                            materialFillJob.Palette = groupEntry.Key.palette;
                            materialFillJob.Target = new NativeArray<int>(512, Allocator.TempJob);
                            
                            jobs.PropertiesFillJobs.Add(materialFillJob);
                            jobHandles.Add(materialFillJob.Schedule(256, 32));
                        }
                    }
                }

                if (dirtyChunksCount != 0) 
                    continue;
                
                jobs.Dispose();
                jobsByEntries.Remove(groupEntry.Key);
            }

            if (jobsByEntries.Count == 0)
            {
                UnityEngine.Pool.DictionaryPool<VoxelObject, GroupEntryJobsSet>.Release(jobsByEntries);
                UnityEngine.Pool.ListPool<JobHandle>.Release(jobHandles);
                
                return new MeshGroupUpdateResult(null, null, MeshGroupUpdateResultStatus.Unchanged);
            }
            
            foreach (var groupEntry in group)
            {
                foreach (var mesh in groupEntry.Object.meshes)
                    mesh.Mesh.Clear(true);
            }

            WaitForAllHandles(jobHandles);

            NativeArray<float3x3> uvs = default;
            var textures = UnityEngine.Pool.ListPool<Texture2D>.Get();
            var palettes = UnityEngine.Pool.ListPool<Texture2D>.Get();
            var referenceIndices = UnityEngine.Pool.ListPool<int>.Get();
            var startingQuadLocations = UnityEngine.Pool.DictionaryPool<VoxelObject, int>.Get();

            var textureEmbeddingsCount = 0;
            foreach (var groupEntry in filteredGroup)
            {
                if (!jobsByEntries.TryGetValue(groupEntry.Key, out GroupEntryJobsSet jobs))
                    continue;
                
                startingQuadLocations.Add(groupEntry.Key, referenceIndices.Count);
                
                if (groupEntry.Key.meshGenerationApproach != MeshGenerationApproach.Textured) 
                    continue;

                foreach (var textureFillJob in jobs.TextureFillJobs)
                {
                    var count = textureFillJob.Quads.Length;

                    var array = textureFillJob.ColorTarget.AsArray().Reinterpret<UnityEngine.Color32>();

                    for (var index = 0; index < count; index++)
                    {
                        var textureInfo = textureFillJob.TextureDescriptors[index];
                        var texture = new Texture2D(textureInfo.Size.x, textureInfo.Size.y, TextureFormat.RGBA32,
                            false,
                            false);
                        var subArray = array.GetSubArray(textureInfo.StartPosition,
                            textureInfo.Size.x * textureInfo.Size.y);

                        texture.SetPixelData(subArray, 0, 0);
                        texture.filterMode = FilterMode.Point;
                        texture.Apply(false);

                        referenceIndices.Add(textures.Count);
                        textures.Add(texture);
                    }
                    
                    textureFillJob.ColorTarget.Dispose();
                    textureFillJob.TextureDescriptors.Dispose();
                }

                if (groupEntry.Key.materialPropertiesEmbeddingMode == MaterialPropertiesEmbeddingMode.Vertex)
                    continue;

                textureEmbeddingsCount++;
                foreach (var propertyFillJob in jobs.PropertiesFillJobs)
                {
                    var paletteTexture = new Texture2D(256, 2, TextureFormat.RGBA32,
                        false,
                        false);
                    
                    paletteTexture.SetPixelData(propertyFillJob.Target, 0);
                    paletteTexture.filterMode = FilterMode.Point;
                    paletteTexture.Apply(false);
                    
                    palettes.Add(paletteTexture);

                    propertyFillJob.Target.Dispose();
                }
            }
            
            Texture2D atlas = null;

            if (textures.Count > 0)
            {
                for (var index = 0; index < textures.Count; index++)
                {
                    var texture = textures[index];
                    if (texture.width > 1 && texture.height > 1)
                        continue;
                    
                    var targetSize = math.max(new int2(2, 2), new int2(texture.width, texture.height));

                    var replacement = new Texture2D(targetSize.x, targetSize.y, TextureFormat.RGBA32, false, false);
                    replacement.filterMode = FilterMode.Point;

                    var data = texture.GetPixelData<UnityEngine.Color32>(0);

                    var updatedData = new NativeArray<UnityEngine.Color32>(targetSize.x * targetSize.y, Allocator.TempJob);

                    var maxTextureWidthIndex = texture.width - 1;
                    var maxTextureHeightIndex = texture.height - 1;

                    for (var y = 0; y < targetSize.y; y++)
                    {
                        for (var x = 0; x < targetSize.x; x++)
                        {
                            var sourceX = math.clamp(x, 0, maxTextureWidthIndex);
                            var sourceY = math.clamp(y, 0, maxTextureHeightIndex);

                            var sourceIndex = sourceY * texture.width + sourceX;
                            var targetIndex = y * targetSize.x + x;

                            updatedData[targetIndex] = data[sourceIndex];
                        }
                    }

                    replacement.SetPixelData(updatedData, 0);
                    replacement.Apply();

                    updatedData.Dispose();
                    
                    Object.DestroyImmediate(texture);
                    textures[index] = replacement;
                }

                var packingResult = TexturePacker.Pack(textures.ToArray(), optimizationMode, false);
                if (textureEmbeddingsCount == filteredGroup.Count)
                {
                    var textureData = packingResult.Atlas.GetPixelData<byte>(0);
                    atlas = new Texture2D(packingResult.Atlas.width, packingResult.Atlas.height, TextureFormat.Alpha8, false);
                    atlas.filterMode = FilterMode.Point;

                    var alpha = new NativeArray<byte>(textureData.Length / 4, Allocator.Temp);
                    for (var index = 0; index < alpha.Length; index++)
                        alpha[index] = textureData[index * 4 + 3];
                    
                    atlas.SetPixelData(alpha, 0);
                    textureData.Dispose();
                    alpha.Dispose();
                    Object.DestroyImmediate(packingResult.Atlas);
                }
                else
                    atlas = packingResult.Atlas;
                
                uvs = packingResult.UV;

                foreach (var texture in textures)
                    Object.DestroyImmediate(texture);
            }
            else
                uvs = new NativeArray<float3x3>(0, Allocator.TempJob);
            
            Texture2D paletteAtlas = null;
            if (palettes.Count > 0)
            {
                var packingResult = TexturePacker.Pack(palettes.ToArray(), TextureOptimizationMode.All, true);
                
                foreach (var texture in textures)
                    Object.DestroyImmediate(texture);

                packingResult.UV.Dispose(); // TODO: Make different palettes work
                paletteAtlas = packingResult.Atlas;
            }

            UnityEngine.Pool.ListPool<Texture2D>.Release(textures);
            UnityEngine.Pool.ListPool<Texture2D>.Release(palettes);
            UnityEngine.Pool.ListPool<int>.Release(referenceIndices);

            foreach (var groupEntry in filteredGroup)
            {
                if (!jobsByEntries.TryGetValue(groupEntry.Key, out GroupEntryJobsSet jobs))
                    continue;
                
                var startingQuadLocation = startingQuadLocations[groupEntry.Key];
                for (var index = 0; index < jobs.QuadsGenerationJobs.Count; index++)
                {
                    var quadGenerationJobEntry = jobs.QuadsGenerationJobs[index];
                    var opaqueJob = quadGenerationJobEntry.OpaqueVertexGenerationJob;
                    opaqueJob.VerticesCount = quadGenerationJobEntry.Job.OpaqueQuads.Length * 4;
                    opaqueJob.UVsStart = startingQuadLocation;

                    startingQuadLocation += quadGenerationJobEntry.Job.OpaqueQuads.Length;

                    var transparentJob = quadGenerationJobEntry.TransparentVertexGenerationJob;
                    transparentJob.VerticesCount = quadGenerationJobEntry.Job.TransparentQuads.Length * 4;
                    transparentJob.UVsStart = startingQuadLocation;

                    startingQuadLocation += quadGenerationJobEntry.Job.TransparentQuads.Length;

                    opaqueJob.UVs = uvs;
                    transparentJob.UVs = uvs;

                    var chunk =
                        groupEntry.Key.chunks[quadGenerationJobEntry.Location.x]
                            [quadGenerationJobEntry.Location.y]
                            [quadGenerationJobEntry.Location.z];

                    CheckSetSize(ref chunk.OpaqueVertices, opaqueJob.VerticesCount);
                    CheckSetSize(ref chunk.TransparentVertices, transparentJob.VerticesCount);

                    groupEntry.Key.chunks[quadGenerationJobEntry.Location.x]
                        [quadGenerationJobEntry.Location.y]
                        [quadGenerationJobEntry.Location.z] = chunk;

                    transparentJob.Vertices = chunk.TransparentVertices.Vertices;
                    opaqueJob.Vertices = chunk.OpaqueVertices.Vertices;

                    quadGenerationJobEntry.TransparentVertexGenerationJob = transparentJob;
                    quadGenerationJobEntry.OpaqueVertexGenerationJob = opaqueJob;
                    
                    jobs.QuadsGenerationJobs[index] = quadGenerationJobEntry;

                    jobHandles.Add(
                        quadGenerationJobEntry.OpaqueVertexGenerationJob.Schedule(
                            quadGenerationJobEntry.Job.OpaqueQuads.Length, 256));
                    jobHandles.Add(
                        quadGenerationJobEntry.TransparentVertexGenerationJob.Schedule(
                            quadGenerationJobEntry.Job.TransparentQuads.Length, 256));
                }
            }

            UnityEngine.Pool.DictionaryPool<VoxelObject, int>.Release(startingQuadLocations);

            WaitForAllHandles(jobHandles);
            uvs.Dispose();

            foreach (var groupEntry in filteredGroup)
            {
                if (!jobsByEntries.TryGetValue(groupEntry.Key, out GroupEntryJobsSet jobs))
                    continue;
                
                foreach (var job in jobs.QuadsGenerationJobs)
                {
                    var chunk = groupEntry.Key.GetChunk(job.Location, true);

                    {
                        var set = chunk.Chunk.OpaqueVertices;
                        set.VerticesCount = job.OpaqueVertexGenerationJob.VerticesCount;
                        chunk.Chunk.OpaqueVertices = set;
                    }

                    {
                        var set = chunk.Chunk.TransparentVertices;
                        set.VerticesCount = job.TransparentVertexGenerationJob.VerticesCount;
                        chunk.Chunk.TransparentVertices = set;
                    }

                    groupEntry.Key.chunks[job.Location.x][job.Location.y][job.Location.z] = chunk.Chunk;
                }

                groupEntry.Key.GenerateMeshes(groupEntry.Value.IndexFormat,
                    jobs.MeshGenerationJobs, jobHandles);
            }

            WaitForAllHandles(jobHandles);

            foreach (var groupEntry in filteredGroup)
            {
                if (!jobsByEntries.TryGetValue(groupEntry.Key, out GroupEntryJobsSet jobs))
                    continue;
                
                foreach (var facesGenerationJob in jobs.FacesGenerationJobs)
                    facesGenerationJob.Faces.Dispose();

                foreach (var quadGenerationJob in jobs.QuadsGenerationJobs)
                {
                    quadGenerationJob.Job.TransparentQuads.Dispose();
                    quadGenerationJob.Job.OpaqueQuads.Dispose();
                }

                while (groupEntry.Key.meshes.Count > jobs.MeshGenerationJobs.Count)
                {
                    Object.Destroy(groupEntry.Key.meshes[groupEntry.Key.meshes.Count - 1].Mesh);
                    groupEntry.Key.meshes.RemoveAt(groupEntry.Key.meshes.Count - 1);
                }

                while (groupEntry.Key.meshes.Count < jobs.MeshGenerationJobs.Count)
                {
                    var mesh = new Mesh();
                    mesh.MarkDynamic();

                    groupEntry.Key.meshes.Add(new MeshDescriptor(mesh));
                }

                for (var index = 0; index < jobs.MeshGenerationJobs.Count; index++)
                {
                    var job = jobs.MeshGenerationJobs[index];

                    var mesh = groupEntry.Key.meshes[index];
                    groupEntry.Key.meshes[index] = mesh;

                    mesh.Mesh.SetVertexBufferParams(job.Count,
                        new VertexAttributeDescriptor(VertexAttribute.Position,
                            VertexAttributeFormat.Float32,
                            3),
                        new VertexAttributeDescriptor(VertexAttribute.Normal,
                            VertexAttributeFormat.Float32,
                            3),
                        new VertexAttributeDescriptor(VertexAttribute.Color,
                            VertexAttributeFormat.UNorm8,
                            4),
                        new VertexAttributeDescriptor(VertexAttribute.TexCoord0,
                            VertexAttributeFormat.Float32,
                            4),
                        new VertexAttributeDescriptor(VertexAttribute.TexCoord2,
                            VertexAttributeFormat.Float32,
                            4),
                        new VertexAttributeDescriptor(VertexAttribute.TexCoord3,
                            VertexAttributeFormat.UNorm8,
                            4)); // TODO: Remove allocation

                    var flags = MeshUpdateFlags.DontNotifyMeshUsers |
                                MeshUpdateFlags.DontValidateIndices |
                                MeshUpdateFlags.DontResetBoneBounds |
                                MeshUpdateFlags.DontRecalculateBounds;

                    mesh.Mesh.SetVertexBufferData(job.Vertices, job.StartIndex, 0, job.Count, flags: flags);

                    var trianglesCount = job.Count / 2;
                    var indicesCount = trianglesCount * 3;

                    mesh.Mesh.SetIndexBufferParams(indicesCount, groupEntry.Value.IndexFormat);

                    if (groupEntry.Value.IndexFormat == IndexFormat.UInt32)
                        mesh.Mesh.SetIndexBufferData(IndexGenerator.Instance.GetWithSizeOfU32(indicesCount), 0, 0,
                            indicesCount, flags: flags);
                    else
                        mesh.Mesh.SetIndexBufferData(IndexGenerator.Instance.GetWithSizeOfU16(indicesCount), 0, 0,
                            indicesCount, flags: flags);

                    var submeshCount = 0;
                    if (job.HasOpaqueVertices)
                    {
                        mesh.MeshKind |= MeshKind.Opaque;
                        submeshCount++;
                    }

                    if (job.HasTransparentVertices)
                    {
                        mesh.MeshKind |= MeshKind.Transparent;
                        submeshCount++;
                    }

                    mesh.Mesh.subMeshCount = submeshCount;

                    var extents = (float3)groupEntry.Key.Size * groupEntry.Key.Scale;
                    var meshBounds = new Bounds(extents * 0.5f + groupEntry.Value.Shift * groupEntry.Key.Scale, extents);

                    if (job.HasOpaqueVertices)
                    {
                        var descriptor = new SubMeshDescriptor(0, (job.OpaqueVerticesCount / 2) * 3);
                        descriptor.bounds = meshBounds;
                        descriptor.vertexCount = job.OpaqueVerticesCount;
                        mesh.Mesh.SetSubMesh(0, descriptor, flags: flags);
                    }

                    if (job.HasTransparentVertices)
                    {
                        var descriptor = new SubMeshDescriptor((job.OpaqueVerticesCount / 2) * 3,
                            (job.TransparentVerticesCount / 2) * 3);
                        descriptor.vertexCount = job.TransparentVerticesCount;
                        descriptor.bounds = meshBounds;
                        descriptor.firstVertex = job.OpaqueVerticesCount;

                        mesh.Mesh.SetSubMesh(job.HasOpaqueVertices ? 1 : 0, descriptor, flags: flags);
                    }

                    mesh.Mesh.bounds = meshBounds;
                    mesh.Mesh.MarkModified();

                    groupEntry.Key.meshes[index] = mesh;
                }
            }

            foreach (var groupEntry in filteredGroup)
            {
                if (!jobsByEntries.TryGetValue(groupEntry.Key, out GroupEntryJobsSet jobs))
                    continue;
                
                var itemsToDellocate = UnityEngine.Pool.HashSetPool<NativeArray<Vertex>>.Get();
                foreach (var meshGenerationJob in jobs.MeshGenerationJobs)
                    itemsToDellocate.Add(meshGenerationJob.Vertices);

                foreach (var itemToDeallocate in itemsToDellocate)
                    itemToDeallocate.Dispose();

                UnityEngine.Pool.HashSetPool<NativeArray<Vertex>>.Release(itemsToDellocate);
            }

            foreach (var entry in jobsByEntries)
                entry.Value.Dispose();

            UnityEngine.Pool.DictionaryPool<VoxelObject, MeshUpdateParameters>.Release(filteredGroup);
            UnityEngine.Pool.ListPool<JobHandle>.Release(jobHandles);
            UnityEngine.Pool.DictionaryPool<VoxelObject, GroupEntryJobsSet>.Release(jobsByEntries);

            return new MeshGroupUpdateResult(atlas, paletteAtlas, MeshGroupUpdateResultStatus.Updated);
        }
        
        /// <summary>
        /// Regenerates meshes
        /// </summary>
        /// <param name="indexFormat">The index format to be used for the meshes</param>
        /// <param name="generationApproach">The generation approach to be taken to generate meshes</param>
        /// <param name="shift">The shift applied to the vertices</param>
        public MeshGroupUpdateResult UpdateMeshes(IndexFormat indexFormat, Vector3 shift = new Vector3())
        {
            var groupsList = UnityEngine.Pool.ListPool<MeshUpdateParameters>.Get();
            groupsList.Add(new MeshUpdateParameters(indexFormat, this, shift));
            
            var result = UpdateVoxelObjectsGroup(groupsList, textureOptimizationMode);

            UnityEngine.Pool.ListPool<MeshUpdateParameters>.Release(groupsList);

            return result;
        }

        private void GenerateMeshes(IndexFormat indexFormat,
            List<MeshGenerationEntry> meshGenerationJobs, List<JobHandle> jobHandles)
        {
            var maxVertices = indexFormat == IndexFormat.UInt16 ? 65528 : UInt32.MaxValue;
            var opaqueVerticesCount = 0;
            var transparentVerticesCount = 0;

            for (var x = 0; x < chunks.Length; x++)
            {
                var xChunks = chunks[x];
                for (var y = 0; y < xChunks.Length; y++)
                {
                    var yChunks = xChunks[y];
                    for (var z = 0; z < yChunks.Length; z++)
                    {
                        var chunk = yChunks[z];
                        if (chunk.VoxelsCount == 0)
                            continue;

                        opaqueVerticesCount += chunk.OpaqueVertices.VerticesCount;
                        transparentVerticesCount += chunk.TransparentVertices.VerticesCount;
                    }
                }
            }

            var totalCount = opaqueVerticesCount + transparentVerticesCount;

            if (totalCount == 0)
                return;

            var targetBuffer = new NativeArray<Vertex>(totalCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            
            var chunkIndex = 0;
            var currentIndex = 0;
            for (var x = 0; x < chunks.Length; x++)
            {
                var xChunks = chunks[x];
                for (var y = 0; y < xChunks.Length; y++)
                {
                    var yChunks = xChunks[y];
                    for (var z = 0; z < yChunks.Length; z++)
                    {
                        var chunk = yChunks[z];
                        if (chunk.VoxelsCount == 0)
                            continue;

                        var set = chunk.OpaqueVertices;
                        if (set.VerticesCount == 0)
                            continue;
                        
                        var copyJob = new CopyVerticesJob();

                        copyJob.Source = set.Vertices;
                        copyJob.Count = set.VerticesCount;
                        copyJob.Result = targetBuffer;
                        copyJob.StartPosition = currentIndex;

                        jobHandles.Add(copyJob.Schedule());
                
                        currentIndex += set.VerticesCount;

                        chunkIndex++;
                    }
                }
            }
            
            for (var x = 0; x < chunks.Length; x++)
            {
                var xChunks = chunks[x];
                for (var y = 0; y < xChunks.Length; y++)
                {
                    var yChunks = xChunks[y];
                    for (var z = 0; z < yChunks.Length; z++)
                    {
                        var chunk = yChunks[z];
                        if (chunk.VoxelsCount == 0)
                            continue;

                        var set = chunk.TransparentVertices;
                        if (set.VerticesCount == 0)
                            continue;
                        
                        var copyJob = new CopyVerticesJob();

                        copyJob.Source = set.Vertices;
                        copyJob.Count = set.VerticesCount;
                        copyJob.Result = targetBuffer;
                        copyJob.StartPosition = currentIndex;

                        jobHandles.Add(copyJob.Schedule());
                
                        currentIndex += set.VerticesCount;

                        chunkIndex++;
                    }
                }
            }

            var leftover = totalCount;
            var startIndex = 0;
            while (leftover > 0)
            {
                var count = (int)Mathf.Min(maxVertices, leftover);
                
                var trianglesCount = count / 2;
                var indicesCount = trianglesCount * 3;
                IndexGenerator.Instance.Resize(indicesCount);
                var currentOpaqueVerticesCount = Mathf.Max(0, Mathf.Min(opaqueVerticesCount - startIndex, count));
                
                meshGenerationJobs.Add(new MeshGenerationEntry(targetBuffer, startIndex, count, currentOpaqueVerticesCount));
                
                leftover -= count;
                startIndex += count;
            }
        }

        /// <summary>
        /// Deletes all generated meshes
        /// </summary>
        public void DeleteMeshes()
        {
            CheckDisposed();
            foreach (var meshDescriptor in meshes)
                Object.DestroyImmediate(meshDescriptor.Mesh);

            meshes.Clear();
        }
        
        /// <summary>
        /// Disposes the voxel object
        /// </summary>
        public void Dispose()
        {
            if (isDisposed)
                return;
            
            palette.Dispose();
            emptyChunk.Dispose();

            foreach (var mesh in meshes)
                GameObject.DestroyImmediate(mesh.Mesh);

            for (var x = 0; x < chunks.Length; x++)
            {
                var xChunks = chunks[x];
                for (var y = 0; y < xChunks.Length; y++)
                {
                    var yChunks = xChunks[y];
                    for (var z = 0; z < yChunks.Length; z++)
                    {
                        var chunk = yChunks[z];
                        chunk.Dispose();
                        yChunks[z] = new Chunk();
                    }
                }
            }

            isDisposed = true;
        }
        
        /// <summary>
        /// Disposes the voxel object but keeps meshes alive
        /// </summary>
        public void DisposeWithoutMeshes()
        {
            if (isDisposed)
                return;
            
            palette.Dispose();
            emptyChunk.Dispose();

            for (var x = 0; x < chunks.Length; x++)
            {
                var xChunks = chunks[x];
                for (var y = 0; y < xChunks.Length; y++)
                {
                    var yChunks = xChunks[y];
                    for (var z = 0; z < yChunks.Length; z++)
                    {
                        var chunk = yChunks[z];
                        chunk.Dispose();
                        yChunks[z] = new Chunk();
                    }
                }
            }

            isDisposed = true;
        }

        /// <summary>
        /// Transforms voxel object to model
        /// </summary>
        /// <returns>Model representing the voxel object</returns>
        public Model ToModel()
        {
            var model = ScriptableObject.CreateInstance<Model>();
            model.Size = size.ToVector3Int();

            var voxels = new List<VoxelData>();
            for (var x = 0; x < size.x; x++)
            {
                for (var y = 0; y < size.y; y++)
                {
                    for (var z = 0; z < size.z; z++)
                    {
                        var voxel = this[new int3(x, y, z)];
                        if (voxel.Material == 0)
                            continue;
                        
                        voxels.Add(new VoxelData(new Vector3Int(x, y, z), voxel.Material));
                    }
                }
            }
            
            model.SetVoxels(voxels);
            
            return model;
        }
    }
}