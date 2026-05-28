using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelToolkit
{
    /// <summary>
    /// Provides information about mesh group update
    /// </summary>
    public struct MeshGroupUpdateResult : IDisposable
    {
        /// <summary>
        /// The status of the update
        /// </summary>
        public MeshGroupUpdateResultStatus Status { get; private set; }
        
        /// <summary>
        /// The atlas result of the mesh update
        /// </summary>
        public Texture2D Atlas { get; private set; }
        
        /// <summary>
        /// The atlas result of the mesh update containing material information
        /// </summary>
        public Texture2D PaletteAtlas { get; private set; }

        internal MeshGroupUpdateResult(Texture2D atlas, Texture2D paletteAtlas, MeshGroupUpdateResultStatus status)
        {
            Atlas = atlas;
            Status = status;
            PaletteAtlas = paletteAtlas;
        }

        /// <summary>
        /// Disposes the result
        /// </summary>
        public void Dispose()
        {
            Object.DestroyImmediate(Atlas);
        }
    }
}