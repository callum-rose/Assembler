using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;
using UnityEngine;

namespace VoxelToolkit
{
    public struct ReadonlyArray<T>
    {
        private T[] values;

        public T[] ToArray()
        {
            var result = new T[values.Length];
            Array.Copy(values, result, result.Length);

            return result;
        }
        
        public T this[int index]
        {
            get => values[index];
        }

        public int Length => values.Length;

        public ReadonlyArray(T[] values)
        {
            this.values = values;
        }
        
        public static implicit operator ReadonlyArray<T>(T[] other)
        {
            return new ReadonlyArray<T>(other);
        }
    }

    [System.Serializable]
    public struct AnimationRange
    {
        [SerializeField] private int start;
        [SerializeField] private int end;

        public int Start => start;
        public int End => end;

        public bool IsValid => start != end || start != 0;

        public AnimationRange(int start, int end)
        {
            this.start = start;
            this.end = end;
        }

        public int Clamp(int index)
        {
            return !IsValid ? index : math.clamp(index, start, end);
        }
    }

    /// <summary>
    /// Represents the voxel asset
    /// </summary>
    [PreferBinarySerialization]
    public class VoxelAsset : ScriptableObject
    {
        [SerializeField] private string version;
        [SerializeField] private float brightness = 1.0f;
        [SerializeField] private float hueShift = 0.0f;
        [SerializeField] private float saturation = 1.0f;
        [SerializeField] private string importSource;
        [SerializeField] private HierarchyNode hierarchyRoot;
        [SerializeField] private List<Layer> layers = new List<Layer>();
        [SerializeField] private Material[] palette = new Material[256];
        [SerializeField] private AnimationRange animationRange; 

        /// <summary>
        /// Allows access to the palette of the asset
        /// </summary>
        public ReadonlyArray<Material> Palette => new ReadonlyArray<Material>(palette);

        /// <summary>
        /// The animation range of the file. All frames outside the range will be ignored
        /// </summary>
        public AnimationRange AnimationRange
        {
            get => animationRange;
            set => animationRange = value;
        }

        /// <summary>
        /// Imported hue shift value
        /// </summary>
        public float HueShift
        {
            get => hueShift;
            set => hueShift = value;
        }

        /// <summary>
        /// Imported saturation value
        /// </summary>
        public float Saturation
        {
            get => saturation;
            set => saturation = value;
        }

        /// <summary>
        /// Imported brightness value
        /// </summary>
        public float Brightness
        {
            get => brightness;
            set => brightness = value;
        }
        
        /// <summary>
        /// The root object of the hierarchy
        /// </summary>
        public HierarchyNode HierarchyRoot
        {
            get => hierarchyRoot;
            set => hierarchyRoot = value;
        }
        
        /// <summary>
        /// The version of the asset
        /// </summary>
        public string Version
        {
            get => version;
            set => version = value;
        }
        
        /// <summary>
        /// The way the asset was imported
        /// </summary>
        public string InputSource
        {
            get => importSource;
            set => importSource = value;
        }

        /// <summary>
        /// The count of layers in the asset
        /// </summary>
        public int LayersCount => layers.Count;

        /// <summary>
        /// Provides models contained in the asset
        /// </summary>
        /// <returns>The models of the asset</returns>
        public List<Model> Models
        {
            get
            {
                var result = new HashSet<Model>();
                CollectModels(HierarchyRoot, result);

                return result.ToList();
            }
        }

        internal void RemapMaterials(Dictionary<byte, byte> remapping)
        {
            var remappedMaterials = new Material[256];
            foreach (var remappingEntry in remapping)
                remappedMaterials[(byte)(remappingEntry.Value + 1)] = palette[remappingEntry.Key];
            
            palette = remappedMaterials;

            foreach (var model in Models)
                model.RemapMaterials(remapping);
        }
        
        /// <summary>
        /// Sets the material into the palette
        /// </summary>
        /// <param name="index">The index of the materials to be set</param>
        /// <param name="material">The material to be set</param>
        /// <exception cref="Exception">Throws exception if the index is less than 0 or more than 255</exception>
        public void SetPaletteMaterial(int index, Material material)
        {
            if (index < 0 || index > 255)
                throw new Exception("Max palette size is 256");

            palette[(index + 1) % 256] = material;
        }

        /// <summary>
        /// Returns the material from the palette
        /// </summary>
        /// <param name="index">The index of the material</param>
        /// <returns>Material with the index</returns>
        /// <exception cref="Exception">Throws exception if the index is less than 0 or more than 255</exception>
        public Material GetPaletteMaterial(int index)
        {
            if (index < 0 || index > 255)
                throw new Exception("Max palette size is 256");

            return palette[(index + 1) % 256];
        }

        /// <summary>
        /// Adds the layer to the asset
        /// </summary>
        /// <param name="layer">The layer to be added</param>
        public void AddLayer(Layer layer)
        {
            layers.Add(layer);
        }

        /// <summary>
        /// Returns a layer by it's ID
        /// </summary>
        /// <param name="id">The ID of the layer to be found</param>
        /// <returns>The layer with a specified ID</returns>
        public Layer FindLayerById(int id)
        {
            return layers.Find(x => x.ID == id);
        }

        /// <summary>
        /// Returns the layer by its index
        /// </summary>
        /// <param name="index">The index of the layer to return</param>
        /// <returns></returns>
        public Layer GetLayer(int index)
        {
            return layers[index];
        }

        private void CollectModels(HierarchyNode node, HashSet<Model> target)
        {
            if (node is Group group)
            {
                for (var index = 0; index < group.ChildrenCount; index++)
                    CollectModels(group[index], target);
            }
            else if (node is Transformation transformation)
            {
                CollectModels(transformation.Child, target);
            }
            else if (node is Shape shape)
            {
                for (var index = 0; index < shape.Animation.FrameCount; index++)
                    target.Add(shape.Animation[index].Value);
            }
        }
    }
}