using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;

namespace VoxelToolkit.Editor
{
    public abstract class VoxelImporter : ScriptedImporter
    {
        [Tooltip("Modifiers to be applied during the generation.")]
        [SerializeField] private ObjectGeneratorsList modifiers = new ObjectGeneratorsList();

        [Tooltip("The depth the opaque edge will be pushed outside during mesh generation. Useful for shadow issues.")]
        [SerializeField] private float opaqueEdgeShift = 0.0f;
        [Tooltip("The depth the transparent edge will be pushed outside during mesh generation. Useful for shadow issues.")]
        [SerializeField] private float transparentEdgeShift = 0.0f;
        [Tooltip("The scale of the object to be generated.")]
        [SerializeField] private float scale = 0.1f;
        [Tooltip("Index format of the mesh to use. Some platforms like some old mobile only support 16 bit indices.")]
        [SerializeField] private IndexFormat indexFormat = IndexFormat.UInt16;
        [Tooltip("Controls whether lightmap uv should be generated during import.")]
        [SerializeField] private bool generateLightmapUV = false;
        [Tooltip("Controls whether colliders should be generated during import.")]
        [SerializeField] private bool generateColliders = true;
        [Tooltip("Controls the size of the chunk the voxel model is going to be split to during generation. Depending on other setting may be beneficial to have larger or smaller value.")]
        [SerializeField, Range(32, 255)] private int chunkSize = 64;
        [Tooltip("Amount of AO shall be added to the model. The value of 0 disables the feature and may spare some triangles.")]
        [SerializeField, Range(0.0f, 10.0f)] private float AOStrength = 1.0f;
        [Tooltip("Controls where the origin of the model is going to be positioned.")]
        [SerializeField] private OriginMode originMode;
        [Tooltip("Controls which part of the model is going to be imported. For instance one can import only mesh data to construct it in the runtime or just a mesh to use it right away.")]
        [SerializeField] private GenerationMode generationMode = GenerationMode.EssentialOnly;
        [Tooltip("The way textures are going to be used in the generated model. Textureless are faster to generate but they have more polygons. Textures usually have much less polygons but have an additional texture.")]
        [SerializeField] private MeshGenerationApproach meshGenerationApproach = MeshGenerationApproach.Textureless;
        [Tooltip("Controls where material properties such as smoothness, emission etc are going to be stored.")]
        [SerializeField] private MaterialPropertiesEmbeddingMode materialPropertiesEmbeddingMode = MaterialPropertiesEmbeddingMode.Vertex;
        [Tooltip("The way texture shall be optimized. Texture optimization works by trying to find some repeating patterns and combine them together. If all enabled then we are going to search though all permutations, but it's going to take more time.")]
        [SerializeField] private TextureOptimizationMode textureOptimizationMode = TextureOptimizationMode.All;
        [Tooltip("The opaque override material to use during creation. (See custom shader demo scene)")]
        [SerializeField] private UnityEngine.Material opaqueMaterial = null;
        [Tooltip("The transparent override material to use during creation. (See custom shader demo scene)")]
        [SerializeField] private UnityEngine.Material transparentMaterial = null;
        [HideInInspector][SerializeField] private bool overrideAssetMaterials = false;
        [HideInInspector][SerializeField] private Material[] overrideMaterials = new Material[256];

        [Tooltip("The amount of hue shift to be applied to the colors of the imported model.")]
        [SerializeField] [Range(-1.0f, 1.0f)] private float hueShift = 0.0f;
        [Tooltip("The amount of brightness shift to be applied to the colors of the imported model.")]
        [SerializeField] [Range(-1.0f, 4.0f)] private float brightness = 0.0f;
        [Tooltip("The saturation of the colors of the imported model.")]
        [SerializeField] [Range(0.0f, 4.0f)] private float saturation = 1.3f;

        private static Dictionary<string, UnityEngine.Material> cachedMaterials = new Dictionary<string, UnityEngine.Material>();
        private static readonly int Palette = Shader.PropertyToID("_Palette");

        protected static string ConvertProjectPathToSystemPath(string projectPath)
        {
            var dataPath = UnityEngine.Application.dataPath;
            var projectFolder = dataPath.Substring(0, dataPath.Length - "Assets".Length);

            return Path.Combine(projectFolder, projectPath);
        }

        private void Reset()
        {
            for (var index = 0; index < overrideMaterials.Length; index++)
                overrideMaterials[index] = Material.Base;
        }

        private static UnityEngine.Material FindMaterial(string name)
        {
            var replacedName = name.Replace('/', '\\');
            if (cachedMaterials.TryGetValue(replacedName, out UnityEngine.Material material))
                return material;

            var materials = AssetDatabase.FindAssets("t:Material").ToList();
            var found = materials.FindAll(x =>
                                          {
                                                var path  = AssetDatabase.GUIDToAssetPath(x).Replace('/', '\\');
                                                return path.EndsWith(replacedName, StringComparison.Ordinal);
                                          });
            
            if (found.Count == 0)
                Debug.LogError($"No material found for name '{replacedName}'");
            else if (found.Count > 1)
                Debug.LogWarning($"More than one material found for name '{replacedName}'");

            material = found.Count == 0 ? null : AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(found[0]);
            if (found.Count == 1)
                cachedMaterials.Add(replacedName, material);
            
            return material;
        }
        
        protected abstract GameObjectGenerationParameters GetGenerationParameters();

        private string GetUniqueName(string assetName, HashSet<string> namesInUse)
        {
            if (namesInUse.Add(assetName))
                return assetName;

            var counter = 1;
            while (!namesInUse.Add($"{assetName} ({++counter})"));
            
            return $"{assetName} ({counter})";
        }

        public override void OnImportAsset(AssetImportContext ctx)
        {
            var asset = ImportAsset(ctx);

            asset.Brightness = brightness;
            asset.HueShift = hueShift;
            asset.Saturation = saturation;
            
            foreach (var modifier in modifiers.Values)
                modifier.Apply(asset);

            ctx.AddObjectToAsset("Voxel data", asset);

            if (overrideAssetMaterials)
            {
                for (var index = 0; index < 256; index++)
                    asset.SetPaletteMaterial(index, overrideMaterials[index]);
            }

            if (generationMode != GenerationMode.DataOnly)
            {
                var gameObjectBuilder = new GameObjectBuilder();

                var opaqueName = "/VoxelToolkitDefaultOpaque.mat";
                var transparentName = "/VoxelToolkitDefaultTransparent.mat";
                
                gameObjectBuilder.OpaqueMaterial = opaqueMaterial ?? FindMaterial($"{PathUtility.GetMaterialPath()}{opaqueName}");
                gameObjectBuilder.TransparentMaterial = transparentMaterial ?? FindMaterial($"{PathUtility.GetMaterialPath()}{transparentName}");

                gameObjectBuilder.AOStrength = AOStrength;
                gameObjectBuilder.MeshGenerationApproach = meshGenerationApproach;
                gameObjectBuilder.MaterialPropertiesEmbeddingMode = materialPropertiesEmbeddingMode;
                gameObjectBuilder.Scale = scale;
                gameObjectBuilder.ChunkSize = chunkSize;
                gameObjectBuilder.IndexFormat = indexFormat;
                gameObjectBuilder.GenerateColliders = generateColliders;
                gameObjectBuilder.OriginMode = originMode;
                gameObjectBuilder.OpaqueEdgeShift = opaqueEdgeShift;
                gameObjectBuilder.TransparentEdgeShift = transparentEdgeShift;
                gameObjectBuilder.GenerateLightmapUV = generateLightmapUV;
                gameObjectBuilder.TextureOptimizationMode = textureOptimizationMode;

                gameObjectBuilder.HueShift = hueShift;
                gameObjectBuilder.Saturation = saturation;
                gameObjectBuilder.Brightness = brightness;
                
                var gameObject = gameObjectBuilder.CreateGameObject(asset, GetGenerationParameters());
                
                ctx.AddObjectToAsset(gameObject.name, gameObject);
                ctx.SetMainObject(gameObject);

                var assetUniqueNames = new HashSet<string>();

                var filters = gameObject.GetComponentsInChildren<MeshFilter>(true);
                var addedMeshes = new HashSet<Mesh>();
                foreach (var meshFilter in filters)
                {
                    if (meshFilter.sharedMesh == null)
                        continue;
                    
                    var mesh = meshFilter.sharedMesh;
                    mesh.name = GetUniqueName($"Mesh {meshFilter.gameObject.name}", assetUniqueNames);
                    if (addedMeshes.Add(mesh))
                        ctx.AddObjectToAsset(mesh.name, mesh);
                }
                
                var meshAnimations = gameObject.GetComponentsInChildren<MeshAnimation>(true);
                foreach (var meshAnimation in meshAnimations)
                {
                    var index = 0;
                    var meshes = meshAnimation.Meshes;
                    var firstName = meshes.Find(x => !string.IsNullOrEmpty(x.name)).name;
                    foreach (var mesh in meshes)
                    {
                        mesh.name = GetUniqueName($"{firstName} frame {index++}", assetUniqueNames);
                        if (addedMeshes.Add(mesh))
                            ctx.AddObjectToAsset(mesh.name, mesh);
                    }
                }

                var animations = gameObject.GetComponentsInChildren<Animation>(true);
                foreach (var animation in animations)
                    foreach (AnimationState state in animation)
                        ctx.AddObjectToAsset(state.name, state.clip);
                
                if (meshGenerationApproach == MeshGenerationApproach.Textured)
                {
                    var texturesToBeSaved = new HashSet<Texture2D>();
                    var materialsToBeSaved = new HashSet<UnityEngine.Material>();
                    var renderers = gameObject.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (var renderer in renderers)
                    {
                        foreach (var material in renderer.sharedMaterials)
                        {
                            materialsToBeSaved.Add(material);
                            texturesToBeSaved.Add(material.mainTexture as Texture2D);
                            
                            var palette = material.GetTexture(Palette);
                            if (palette != null)
                                texturesToBeSaved.Add(palette as Texture2D);
                        }
                    }

                    var textureIndex = 0;
                    foreach (var texture in texturesToBeSaved)
                    {
                        texture.name = GetUniqueName(texturesToBeSaved.Count > 1 ? $"Texture {textureIndex++}" : "Texture", assetUniqueNames);
                        ctx.AddObjectToAsset(texture.name, texture);
                    }

                    var materialIndex = 0;
                    foreach (var material in materialsToBeSaved)
                    {
                        material.name = GetUniqueName(materialsToBeSaved.Count > 1 ? $"Material {materialIndex++}" : "Material", assetUniqueNames);   
                        ctx.AddObjectToAsset(material.name, material);
                    }
                }
            }
            
            if (generationMode == GenerationMode.EssentialOnly)
                return;

            var models = asset.Models;
            foreach (var model in models)
                ctx.AddObjectToAsset(model.name, model);
            
            for (var index = 0; index < asset.LayersCount; index++)
                ctx.AddObjectToAsset(asset.GetLayer(index).name, asset.GetLayer(index));

            ctx.AddObjectToAsset(asset.HierarchyRoot.name, asset.HierarchyRoot);
            AddRelatedObjectsToContext(ctx, asset.HierarchyRoot);
        }

        private void AddRelatedObjectsToContext(AssetImportContext context, HierarchyNode node)
        {
            foreach (var nodeRelatedObject in node.RelatedObjects)
            {
                context.AddObjectToAsset(nodeRelatedObject.name, nodeRelatedObject);
                if (nodeRelatedObject is HierarchyNode hierarchyNode)
                    AddRelatedObjectsToContext(context, hierarchyNode);
            }
        }

        protected abstract VoxelAsset ImportAsset(AssetImportContext ctx);
    }
}