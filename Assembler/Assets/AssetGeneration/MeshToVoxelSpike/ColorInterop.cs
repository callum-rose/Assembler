using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Boundary conversions between the engine-free voxel-core colour types (<see cref="Rgba32"/> /
    /// <see cref="ColorF"/>) and their <c>UnityEngine</c> equivalents, used only in the editor layer
    /// that builds renderable meshes and loads textures. Keeps every Unity-colour dependency out of
    /// the core assembly.
    /// </summary>
    internal static class ColorInterop
    {
        public static Color32 ToUnity(this Rgba32 c) => new(c.r, c.g, c.b, c.a);

        public static Rgba32 ToCore(this Color32 c) => new(c.r, c.g, c.b, c.a);

        public static ColorF ToCoreLinear(this Color c) => new(c.r, c.g, c.b, c.a);
    }
}
