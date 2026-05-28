using System.Runtime.InteropServices;
using UnityEngine;

namespace VoxelToolkit
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MaterialParameters
    {
        public readonly byte Roughness;
        public readonly byte Plastic;
        public readonly byte Emit;
        public readonly byte Flux;

        public MaterialParameters(byte roughness, byte plastic, byte emit, byte flux)
        {
            Roughness = roughness;
            Plastic = plastic;
            Emit = emit;
            Flux = flux;
        }
    }
    
    [StructLayout(LayoutKind.Explicit)]
    public struct TransformedMaterial
    {
        [FieldOffset(0)] 
        public readonly byte Index;
        [FieldOffset(1)]
        public readonly Color32 Color;
        [FieldOffset(5)] 
        public readonly ulong Hash;
        [FieldOffset(5)]
        public readonly MaterialType MaterialType;
        [FieldOffset(9)]
        public readonly int Parameters;
        [FieldOffset(9)] 
        public readonly MaterialParameters ParameterValues;

        public TransformedMaterial(byte index, Material material, float hueShift, float saturation, float brightness)
        {
            Hash = 0;
            Index = index;
            MaterialType = index == 0 ? MaterialType.Invalid : material.MaterialType;

            var color = material.Color.ApplyTransformation(hueShift, saturation, brightness);
            color.a = material.color.a;
            var color32 = (UnityEngine.Color32)color;
            Color = new Color32(color32.r, color32.g, color32.b, color32.a);
            
            var roughness = (byte)(material.roughness * 255.0f);
            var plastic = (byte)(material.plastic * 255.0f);
            var emit = (byte)((material.emit / 10.0f) * 255.0f);
            var flux = (byte)((material.flux / 100.0f) * 255.0f);

            Parameters = 0;
            ParameterValues = new MaterialParameters(roughness, plastic, emit, flux);
        }
    }
    
    /// <summary>
    /// Represents the material with its properties
    /// </summary>
    [System.Serializable]
    public struct Material
    {
        /// <summary>
        /// Basic white diffuse material
        /// </summary>
        public static readonly Material Base = new Material()
        {
            Color = Color.white,
            materialType = MaterialType.Basic,
            weight = 1.0f,
            roughness = 1.0f,
            specular = 0.0f,
            ior = 1.0f,
            attenuation = 1.0f,
            flux = 0.0f,
            plastic = 1.0f,
            emit = 0.0f
        };

        [SerializeField] internal Color color;
        [SerializeField] internal MaterialType materialType;
        [SerializeField] internal float weight;
        [SerializeField] internal float roughness;
        [SerializeField] internal float specular;
        [SerializeField] internal float ior;
        [SerializeField] internal float attenuation;
        [SerializeField] internal float flux;
        [SerializeField] internal float plastic;
        [SerializeField] internal float emit;

        /// <summary>
        /// Material's color
        /// </summary>
        public Color Color
        {
            get => color;
            set => color = value;
        }

        /// <summary>
        /// Emission level of the material
        /// </summary>
        public float Emit
        {
            get => emit;
            set => emit = value;
        }

        /// <summary>
        /// The type of the material
        /// </summary>
        public MaterialType MaterialType
        {
            get => materialType;
            set => materialType = value;
        }

        /// <summary>
        /// The weight of the material
        /// </summary>
        public float Weight
        {
            get => weight;
            set => weight = value;
        }

        /// <summary>
        /// The roughness of the material
        /// </summary>
        public float Roughness
        {
            get => roughness;
            set => roughness = value;
        }

        /// <summary>
        /// The specular factor of the material
        /// </summary>
        public float Specular
        {
            get => specular;
            set => specular = value;
        }

        /// <summary>
        /// The IOR level of the material
        /// </summary>
        public float IOR
        {
            get => ior;
            set => ior = value;
        }

        /// <summary>
        /// The attenuation factor of the material
        /// </summary>
        public float Attenuation
        {
            get => attenuation;
            set => attenuation = value;
        }

        /// <summary>
        /// The flux factor of the material
        /// </summary>
        public float Flux
        {
            get => flux;
            set => flux = value;
        }

        /// <summary>
        /// The plasticity level of the material
        /// </summary>
        public float Plastic
        {
            get => plastic;
            set => plastic = value;
        }

        /// <summary>
        /// Creates default material with provided color
        /// </summary>
        /// <param name="color">The color to use for the material</param>
        /// <returns>Base material with provided color</returns>
        public static Material CreateBaseWithColor(Color color)
        {
            var material = Base;
            material.color = color;

            return material;
        }

        public override string ToString()
        {
            return $"[{color} {materialType}]";
        }
    }
}