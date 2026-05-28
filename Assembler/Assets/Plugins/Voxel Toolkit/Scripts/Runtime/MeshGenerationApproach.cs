namespace VoxelToolkit
{
    /// <summary>
    /// The approach used to generate meshes
    /// </summary>
    public enum MeshGenerationApproach
    {
        /// <summary>
        /// No textures used. All data is in the vertices
        /// </summary>
        Textureless,
        /// <summary>
        /// Color data is encoded to texture
        /// </summary>
        Textured
    }
    
    /// <summary>
    /// Defines where material properties should be embedded to
    /// </summary>
    public enum MaterialPropertiesEmbeddingMode
    {
        /// <summary>
        /// Properties are embedded into the mesh
        /// </summary>
        Vertex,
        /// <summary>
        /// Properties are embedded into the texture
        /// </summary>
        Texture
    }
}