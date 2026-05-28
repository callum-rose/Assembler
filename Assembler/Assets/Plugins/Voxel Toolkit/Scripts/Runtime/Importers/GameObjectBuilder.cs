using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace VoxelToolkit
{
    public class AnimationChunk
    {
        public readonly string Name;
        public readonly int StartFrame;
        public readonly int Length;
        public readonly bool Loop;

        public AnimationChunk(string name, int startFrame, int length, bool loop)
        {
            Name = name;
            StartFrame = startFrame;
            Length = length;
            Loop = loop;
        }
    }
    
    public class AnimationProperties
    {
        public readonly string RootPath;
        public readonly float FrameDuration;
        public readonly bool Loop;

        public readonly IReadOnlyList<AnimationChunk> Chunks;

        public AnimationProperties(string rootPath, bool loop, float frameDuration, IReadOnlyList<AnimationChunk> chunks)
        {
            RootPath = rootPath;
            Loop = loop;
            FrameDuration = frameDuration;
            Chunks = chunks;
        }
    }
    
    public class GameObjectGenerationParameters
    {
        public readonly IReadOnlyList<AnimationProperties> AnimationProperties;

        public GameObjectGenerationParameters(IReadOnlyList<AnimationProperties> animationProperties)
        {
            AnimationProperties = animationProperties;
        }
    }
    
    /// <summary>
    /// Builds a game objects hierarchy for a specific asset
    /// </summary>
	public class GameObjectBuilder
    {
        private static readonly int Palette = Shader.PropertyToID("_Palette");

        /// <summary>
        /// The amount of AO to be applied to the object
        /// </summary>
        public float AOStrength { get; set; }
        /// <summary>
        /// The hue shift to be applied to the materials
        /// </summary>
        public float HueShift { get; set; } = 0.0f;
        /// <summary>
        /// Saturation to be applied to the materials
        /// </summary>
        public float Saturation { get; set; } = 1.3f;
        /// <summary>
        /// Brightness to be applied to the materials
        /// </summary>
        public float Brightness { get; set; } = 0.0f;
        /// <summary>
        /// The opaque material to be used for the object built
        /// </summary>
        public UnityEngine.Material OpaqueMaterial { get; set; }
        /// <summary>
        /// The transparent material to be used for the object built
        /// </summary>
        public UnityEngine.Material TransparentMaterial { get; set; }
        /// <summary>
        /// How much should transparent edges of the mesh should be shifted (Helps with the incorrect shadows)
        /// </summary>
        public float TransparentEdgeShift { get; set; }
        /// <summary>
        /// How much should opaque edges of the mesh should be shifted (Helps with the incorrect shadows)
        /// </summary>
        public float OpaqueEdgeShift { get; set; }
        /// <summary>
        /// The scale of the mesh built
        /// </summary>
        public float Scale { get; set; } = 0.1f;
        /// <summary>
        /// The chunk size to be used to generate the mesh
        /// </summary>
        public int ChunkSize { get; set; } = 16;
        /// <summary>
        /// Generation approach to be taken to generate meshes
        /// </summary>
        public MeshGenerationApproach MeshGenerationApproach { get; set; } = MeshGenerationApproach.Textureless;
        /// <summary>
        /// Defines where material properties should be embedded to
        /// </summary>
        public MaterialPropertiesEmbeddingMode MaterialPropertiesEmbeddingMode  { get; set; } = MaterialPropertiesEmbeddingMode.Vertex;
        /// <summary>
        /// The origin mode of the objects to be generated
        /// </summary>
        public OriginMode OriginMode { get; set; } = OriginMode.Center;
        /// <summary>
        /// Index format to be used. If null UInt32 will be used if supported  
        /// </summary>
        public IndexFormat? IndexFormat { get; set; }
#if UNITY_EDITOR
        /// <summary>
        /// If set the mesh is going to have UV2 lightmap coords (Editor Only)
        /// </summary>
        public bool GenerateLightmapUV { get; set; }
#endif
        /// <summary>
        /// If set the resulting objects going to have mesh colliders with respected meshes
        /// </summary>
        public bool GenerateColliders { get; set; } = true;

        /// <summary>
        /// Texture optimization mode to use
        /// </summary>
        public TextureOptimizationMode TextureOptimizationMode { get; set; } = TextureOptimizationMode.None;
        
        private struct ShapeGenerationParameters
        {
            public readonly Shape Shape;
            public readonly int ModelID;
            public readonly VoxelObject VoxelObject;
            public readonly GameObject RootGameObject;
            public readonly string Path;

            public ShapeGenerationParameters(VoxelObject voxelObject, GameObject rootGameObject, Shape shape, Model model, string path)
            {
                VoxelObject = voxelObject;
                RootGameObject = rootGameObject;
                Shape = shape;
                ModelID = model.ID;
                Path = path;
            }
        }
        
        /// <summary>
        /// Creates a game object for the given asset
        /// </summary>
        /// <param name="asset">The asset to generate game object hierarchy from</param>
        /// <param name="parameters">Additional game object generation parameters to take into account</param>
        /// <returns>The game object built from the given asset</returns>
        public GameObject CreateGameObject(VoxelAsset asset, GameObjectGenerationParameters parameters = null)
        {
            var shapeGenerationParametersList = UnityEngine.Pool.ListPool<ShapeGenerationParameters>.Get();
            var keys = UnityEngine.Pool.DictionaryPool<GameObject, string>.Get();
            var group = AddHierarchyObject(asset, null, asset.HierarchyRoot, shapeGenerationParametersList, parameters, keys);
            group.name = "Root";
            
            GenerateMeshes(asset, shapeGenerationParametersList, parameters, keys);

            UnityEngine.Pool.ListPool<ShapeGenerationParameters>.Release(shapeGenerationParametersList);
            if (group == null)
            {
                UnityEngine.Pool.DictionaryPool<GameObject, string>.Release(keys);
                return group;
            }

#if UNITY_EDITOR
            EnsureNamesAreUnique(group);
#endif
            
            CreateAnimations(asset, group, parameters, keys);
            
            UnityEngine.Pool.DictionaryPool<GameObject, string>.Release(keys);
            
            return group;
        }

#if UNITY_EDITOR
        private static void EnsureNamesAreUnique(GameObject go)
        {
            UnityEditor.GameObjectUtility.EnsureUniqueNameForSibling(go);
            for (var index = 0; index < go.transform.childCount; index++)
                EnsureNamesAreUnique(go.transform.GetChild(index).gameObject);
        }
#endif

        private ShapeGenerationParameters? FindParametersWithID(int id, List<ShapeGenerationParameters> shapeGenerationParametersList)
        {
            foreach (var entry in shapeGenerationParametersList)
                if (entry.ModelID == id)
                    return entry;

            return null;
        }

        private void UpdatePath(HierarchyNode node, ref string path)
        {
            if (path != null && node is INamedObject namedObject && !string.IsNullOrEmpty(namedObject.Name))
                path += string.IsNullOrEmpty(path) ? namedObject.Name : $"\r{namedObject.Name}";
            else
                path = null;
        }
        
        private GameObject AddHierarchyObject(VoxelAsset asset, GameObject parent, HierarchyNode node, List<ShapeGenerationParameters> shapeGenerationParametersList, GameObjectGenerationParameters gameObjectGenerationParameters, Dictionary<GameObject, string> keys)
        {
            var path = string.Empty;
            var result = (GameObject)null;
            if (node is Group group)
                result = AddGroupGameObject(asset, parent, group, shapeGenerationParametersList, path, gameObjectGenerationParameters, keys);

            if (node is Transformation transformation)
            {
                transformation.Name = "Root";
                var transformationResult = AddTransformationElement(asset, parent, transformation, shapeGenerationParametersList, path, gameObjectGenerationParameters, keys);
                if (transformationResult.IsGroup)
                    result = transformationResult.GameObject;
            }
            else if (node is Shape shape)
                result = AddShapeGameObject(asset, parent, shape, shapeGenerationParametersList, path);
            else
                throw new Exception("Unexpected hierarchy object type");
            
            if (result != null)
                AddAnimationIfNeeded(result, path, gameObjectGenerationParameters, keys);

            return result;
        }
        
        private void GenerateMeshes(VoxelAsset asset, List<ShapeGenerationParameters> shapeGenerationParametersList, GameObjectGenerationParameters parameters, Dictionary<GameObject, string> keys)
        {
            var groupToUpdate = UnityEngine.Pool.ListPool<MeshUpdateParameters>.Get();

            foreach (var entry in shapeGenerationParametersList)
            {
                var format = IndexFormat ?? (SystemInfo.supports32bitsIndexBuffer
                    ? UnityEngine.Rendering.IndexFormat.UInt32
                    : UnityEngine.Rendering.IndexFormat.UInt16);

                var meshShift = OriginMode == OriginMode.Corner
                    ? Vector3.zero
                    : -((Vector3)(float3)entry.VoxelObject.Size / 2.0f);

                groupToUpdate.Add(new MeshUpdateParameters(format, entry.VoxelObject, meshShift));
            }

            var result = VoxelObject.UpdateVoxelObjectsGroup(groupToUpdate, TextureOptimizationMode);

            var opaqueName = "/VoxelToolkitDefaultOpaque";
            var transparentName = "/VoxelToolkitDefaultTransparent";

            var opaqueMaterial = OpaqueMaterial == null
                ? Resources.Load<UnityEngine.Material>($"{PathUtility.GetMaterialPath()}{opaqueName}")
                : OpaqueMaterial;

            var transparentMaterial = TransparentMaterial == null
                ? Resources.Load<UnityEngine.Material>($"{PathUtility.GetMaterialPath()}{transparentName}")
                : TransparentMaterial;

            if (MeshGenerationApproach == MeshGenerationApproach.Textured)
            {
                opaqueMaterial = new UnityEngine.Material(opaqueMaterial)
                    {
                        mainTexture = result.Atlas
                    };
                
                transparentMaterial = new UnityEngine.Material(transparentMaterial)
                    {
                        mainTexture = result.Atlas
                    };
                
                opaqueMaterial.SetTexture(Palette, result.PaletteAtlas);
                transparentMaterial.SetTexture(Palette, result.PaletteAtlas);
                
                opaqueMaterial.SetFloat(Keywords.PropertiesTextureID, result.PaletteAtlas == null ? 0.0f : 1.0f);
                opaqueMaterial.EnableKeyword(Keywords.PropertiesTexture);
                
                transparentMaterial.SetFloat(Keywords.PropertiesTextureID, result.PaletteAtlas == null ? 0.0f : 1.0f);
                transparentMaterial.EnableKeyword(Keywords.PropertiesTexture);

                opaqueMaterial.SetFloat(Keywords.TexturedID, 1.0f);
                opaqueMaterial.EnableKeyword(Keywords.Textured);
                
                transparentMaterial.SetFloat(Keywords.TexturedID, 1.0f);
                transparentMaterial.EnableKeyword(Keywords.Textured);
            }

            for (var index = 0; index < groupToUpdate.Count; index++)
            {
                var group = groupToUpdate[index];
                var entry = shapeGenerationParametersList[index];
                
                if (entry.ModelID != entry.Shape.Animation.DefaultFrame.Value.ID)
                    continue;
                
                var go = entry.RootGameObject;
                
#if UNITY_EDITOR
                if (GenerateLightmapUV)
                    group.Object.GenerateLightmapUV();
#endif
                
                for (var modelIndex = 0; modelIndex < group.Object.MeshesCount; modelIndex++)
                {
                    var shift = (float3)(group.Object.Size / 2) * Scale;
                    
                    var descriptor = group.Object.GetMesh(modelIndex);

                    var childName = group.Object.MeshesCount == 1 ? go.name : $"{go.name} entry {modelIndex}";
                    var target = (GameObject)null;
                    if (group.Object.MeshesCount == 1)
                        target = go;
                    else
                    {
                        target = new GameObject(childName);
                        target.transform.SetParent(go.transform);
                        target.transform.localPosition = Vector3.zero;
                        target.transform.localRotation = Quaternion.identity;
                    }

                    if (entry.Shape.Animation.FrameCount > 1 && group.Object.MeshesCount == 1)
                    {
                        AddAnimationIfNeeded(target, entry.Path, parameters, keys);
                        var animation = GetOrAddComponent<MeshAnimation>(target);
                        animation.AnimationRange = asset.AnimationRange;
                        animation.Looped = entry.Shape.Animation.Looped;
                        for (var frameIndex = 0; frameIndex < entry.Shape.Animation.FrameCount; frameIndex++)
                        {
                            var frameToSearch = entry.Shape.Animation[frameIndex];
                            var frame = shapeGenerationParametersList.Find(x => x.ModelID == frameToSearch.Value.ID);
                            if (frame.VoxelObject.MeshesCount == 0)
                                continue;
                            
                            var currentFrame = entry.Shape.Animation[frameIndex];
                            var frameToAdd = new MeshAnimation.Frame(frame.VoxelObject.GetMesh(0).Mesh);
                            animation.SetFrame(frameToAdd, currentFrame.Frame);
                        }
                    }

                    var meshFilter = GetOrAddComponent<MeshFilter>(target);

                    target.transform.localPosition += (Vector3)(-shift - group.Shift * Scale);

                    if (GenerateColliders)
                    {
                        var meshCollider = GetOrAddComponent<MeshCollider>(target);
                        meshCollider.sharedMesh = descriptor.Mesh;
                    }

                    var renderer = GetOrAddComponent<MeshRenderer>(target);
                    
                    MaterialSetupUtility.Setup(renderer, descriptor, opaqueMaterial, transparentMaterial);
                        
                    meshFilter.sharedMesh = descriptor.Mesh;
                    descriptor.Mesh.name = $"{go.name} {modelIndex}{entry.Shape.ID}{index}";
                }
            }
            
            foreach (var group in groupToUpdate)
                group.Object.DisposeWithoutMeshes();

            UnityEngine.Pool.ListPool<MeshUpdateParameters>.Release(groupToUpdate);
        }

        private T GetOrAddComponent<T>(GameObject target) where T : Component
        {
            var entry = target.GetComponent<T>();
            if (entry != null)
                return entry;

            return target.AddComponent<T>();
        }

        private string GetRelativePath(GameObject root, GameObject child)
        {
            if (child == root)
                return string.Empty;
                    
            var builder = new StringBuilder();
            do
            {
                builder.Insert(0, $"{child.name}/");
                child = child.transform.parent.gameObject;
            } while (child != root);

            return builder.ToString();
        }

        private void CreateAnimations(VoxelAsset asset, GameObject root, GameObjectGenerationParameters parameters, Dictionary<GameObject, string> keys)
        {
            var animations = UnityEngine.Pool.ListPool<Animation>.Get();
            root.GetComponentsInChildren(animations);

            foreach (var animation in animations)
            {
                var entryKey = keys[animation.gameObject];
                var entry = parameters.AnimationProperties.First(x => x.RootPath == entryKey);
                var components = UnityEngine.Pool.ListPool<VoxelAnimationBase>.Get();

                animation.GetComponentsInChildren(components);
                components.RemoveAll(x => x.GetComponentInParent<Animation>() != animation);

                var maxFrame = 0;
                foreach (var component in components)
                    maxFrame = math.max(maxFrame, component.MaxFrame);

                var first = true;
                foreach (var chunk in entry.Chunks)
                {
                    var chunkClip = new AnimationClip();
                    chunkClip.wrapMode = chunk.Loop ? WrapMode.Loop : WrapMode.ClampForever;
                    chunkClip.name = chunk.Name;
                    chunkClip.legacy = true;
                    
                    animation.AddClip(chunkClip, chunk.Name);
                    if (first)
                    {
                        animation.clip = chunkClip;
                        first = false;
                    }

                    var length = asset.AnimationRange.Clamp(chunk.Length == int.MaxValue ? (maxFrame + 1) : chunk.Length);
                    var start = asset.AnimationRange.Clamp(chunk.Length == int.MaxValue ? 0 : chunk.StartFrame);
                    var startTime = 0.0f;
                    var endTime = (start + length) * entry.FrameDuration;
                    
                    foreach (var component in components)
                    {
                        var path = GetRelativePath(animation.gameObject, component.gameObject);
                        chunkClip.SetCurve(path, component.GetType(), "time",
                            AnimationCurve.Linear(startTime, start, endTime + entry.FrameDuration, start + length + 1));
                    }
                }

                UnityEngine.Pool.ListPool<VoxelAnimationBase>.Release(components);
            }
            
            UnityEngine.Pool.ListPool<Animation>.Release(animations);
        }

        private GameObject AddShapeGameObject(VoxelAsset asset, GameObject parent, Shape shape, List<ShapeGenerationParameters> shapeGenerationParametersList, string path)
        {
            var go = new GameObject(shape.name);
            go.transform.SetParent(parent != null ? parent.transform : null);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            
            for (var index = 0; index < shape.Animation.FrameCount; index++)
            {
                var model = shape.Animation[index].Value;
                var currentShape = FindParametersWithID(model.ID, shapeGenerationParametersList);
                if (currentShape.HasValue)
                {
                    shapeGenerationParametersList.Add(new ShapeGenerationParameters(currentShape.Value.VoxelObject, go, shape, model, path));
                    continue;
                }

                var maxSize = Mathf.Max(model.Size.x, model.Size.y, model.Size.z);
                var voxelObject = VoxelObject.CreateFromModel(model, asset.Palette, Mathf.Min(maxSize, ChunkSize));
                voxelObject.Scale = Scale;
                voxelObject.OpaqueEdgeShift = OpaqueEdgeShift;
                voxelObject.TransparentEdgeShift = TransparentEdgeShift;
                voxelObject.MeshGenerationApproach = MeshGenerationApproach;
                voxelObject.AOStrength = AOStrength;
                voxelObject.MaterialPropertiesEmbeddingMode = MaterialPropertiesEmbeddingMode;

                voxelObject.HueShift = HueShift;
                voxelObject.Saturation = Saturation;
                voxelObject.Brightness = Brightness;
                
                shapeGenerationParametersList.Add(new ShapeGenerationParameters(voxelObject, go, shape, model, path));
            }

            return go;
        }

        private GameObject AddGroupGameObject(VoxelAsset asset, GameObject parent, Group group, List<ShapeGenerationParameters> shapeGenerationParametersList, string path, GameObjectGenerationParameters gameObjectGenerationParameters, Dictionary<GameObject, string> keys)
        {
            var go = new GameObject(group.Name);
            go.transform.SetParent(parent == null ? null : parent.transform);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            for (var index = 0; index < group.ChildrenCount; index++)
                AddTransformationElement(asset, go, group[index] as Transformation, shapeGenerationParametersList, path, gameObjectGenerationParameters, keys);

            return go;
        }

        private void AddAnimationIfNeeded(GameObject go, string path, GameObjectGenerationParameters parameters, Dictionary<GameObject, string> keys)
        {
            if (parameters == null)
                return;

            if (go.GetComponent<Animation>() != null)
                return;
            
            var entryProperties = parameters.AnimationProperties.FirstOrDefault(x => x.RootPath == path); 
            if (entryProperties == null)
                return;
            
            keys.Add(go, path); 
            
            var animation = go.AddComponent<Animation>();
            animation.name = go.name;
            animation.wrapMode = entryProperties.Loop ? WrapMode.Loop : WrapMode.ClampForever;
        }

        public static (int4x4 Transformation, int3 Scale) SeparateScale(int4x4 matrix)
        {
            var xAxis = matrix.c0.xyz;
            var yAxis = matrix.c1.xyz;
            var zAxis = matrix.c2.xyz;

            if (math.dot(math.cross(xAxis, yAxis), zAxis) >= 0)
                return (matrix, new int3(1, 1, 1));
            
            var scale = new int3(
                math.dot(matrix.c0.xyz, new int3(1, 0, 0)) < 0 ? -1 : 1,
                math.dot(matrix.c1.xyz, new int3(0, 1, 0)) < 0 ? -1 : 1,
                math.dot(matrix.c2.xyz, new int3(0, 0, 1)) < 0 ? -1 : 1
            );

            var restored = new int4x4(
                new int4(matrix.c0.xyz * scale.x, 0),
                new int4(matrix.c1.xyz * scale.y, 0),
                new int4(matrix.c2.xyz * scale.z, 0),
                new int4(matrix.c3.xyz, 1)
            );

            return (restored, scale);
        }

        private void AddTransformationAnimationIfNeeded(VoxelAsset asset, Transformation transformation, Transform target)
        {
            if (transformation.Animation.FrameCount < 2)
                return;

            var entryTransformation = GetOrAddComponent<TransformAnimation>(target.gameObject);
            entryTransformation.AnimationRange = asset.AnimationRange;
            entryTransformation.UnitSize = Scale;
            entryTransformation.Looped = transformation.Animation.Looped;
            for (var index = 0; index < transformation.Animation.FrameCount; index++)
            {
                var frame = transformation.Animation[index];
                var disassembledTransformation = DisassembleTransformation(frame.Value);
                entryTransformation.SetFrame(new TransformAnimation.Frame(disassembledTransformation.Position, disassembledTransformation.Rotation, disassembledTransformation.Scale), frame.Frame);
            }
        }

        private (float3 Position, quaternion Rotation, float3 Scale) DisassembleTransformation(int4x4 transformation)
        {
            var separation = SeparateScale(transformation);
                
            var position = (float3)math.mul(separation.Transformation, new int4(0, 0, 0, 1)).xyz * Scale;
            var rotationMatrix = new float3x3(
                separation.Transformation.c0.xyz,
                separation.Transformation.c1.xyz,
                separation.Transformation.c2.xyz);
                
            var rotation = math.rotation(rotationMatrix);
            var scale = (float3)separation.Scale;

            return (position, rotation, scale);
        }
        
        private (GameObject GameObject, bool IsGroup) AddTransformationElement(VoxelAsset asset, GameObject parent, Transformation transformation, List<ShapeGenerationParameters> shapeGenerationParametersList, string path, GameObjectGenerationParameters generationParameters, Dictionary<GameObject, string> keys)
        {
            UpdatePath(transformation, ref path);
            var frameIndex = 0;
            var frame = transformation.Animation.Frames[frameIndex];

            GameObject result = null;
            string fallbackName = null;
            var isGroup = false;
            if (transformation.Child is Shape shape)
            {
                result = AddShapeGameObject(asset, parent, shape, shapeGenerationParametersList, path);
                fallbackName = "Shape";
            }
            else if (transformation.Child is Group group)
            {
                isGroup = true;
                result = AddGroupGameObject(asset, parent, group, shapeGenerationParametersList, path, generationParameters, keys);
                fallbackName = "Group";
            }

            var disassembledTransformation = DisassembleTransformation(frame.Value);

            result.transform.localPosition = disassembledTransformation.Position;
            result.transform.localRotation = disassembledTransformation.Rotation;
            result.transform.localScale = disassembledTransformation.Scale;
            
            result.name = (transformation.Name ?? fallbackName) ?? string.Empty;
            AddAnimationIfNeeded(result, path, generationParameters, keys);
            
            AddTransformationAnimationIfNeeded(asset, transformation, result.transform);
            
            return (result, isGroup);
        }
	}
}