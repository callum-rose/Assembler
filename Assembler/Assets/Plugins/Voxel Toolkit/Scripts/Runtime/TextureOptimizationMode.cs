namespace VoxelToolkit
{
    /// <summary>
    /// The texture optimization mode to use while generating texture
    /// </summary>
    public enum TextureOptimizationMode
    {
        /// <summary>
        /// No optimization is going to take place
        /// </summary>
        None,
        
        /// <summary>
        /// Use texture rotation permutations to find patterns
        /// </summary>
        Rotations,
        
        /// <summary>
        /// Use texture mirroring permutations to find patterns
        /// </summary>
        Mirroring,
        
        /// <summary>
        /// Use texture mirroring and rotation permutations to find patterns
        /// </summary>
        All
    }
}