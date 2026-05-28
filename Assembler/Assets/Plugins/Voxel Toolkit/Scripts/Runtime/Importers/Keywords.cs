using UnityEngine;

namespace VoxelToolkit
{
    public static class Keywords
    {
        public static readonly string Textured = "VTK_TEXTURED";
        public static readonly string PropertiesTexture = "VTK_PROPERTIES_TEXTURE";
        public static readonly int TexturedID = Shader.PropertyToID("VTK_TEXTURED");
        public static readonly int PropertiesTextureID = Shader.PropertyToID("VTK_PROPERTIES_TEXTURE");
    }
}