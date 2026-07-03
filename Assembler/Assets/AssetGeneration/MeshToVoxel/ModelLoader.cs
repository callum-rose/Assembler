using MeshToVoxelsConverter = Assembler.AssetGeneration.MeshToVoxels.ObjToVoxConverter;

namespace Assembler.AssetGeneration.MeshToVoxel.Editor
{
    /// <summary>
    /// Editor-side mesh loading for the pipeline: reads a .obj/.fbx from disk (OBJ via a local parser,
    /// FBX via Unity's importer / <c>AssetDatabase</c>) and decodes its texture with Unity, producing
    /// the engine-free <see cref="LoadedModel"/> that <see cref="MeshVoxeliser"/> consumes. Loading is
    /// the one step that genuinely needs the editor (Unity's FBX importer + <c>Texture2D</c> decode),
    /// so it lives here rather than in the portable core — a headless/non-Unity caller supplies its
    /// own already-loaded <see cref="LoadedModel"/> instead.
    ///
    /// Currently bridges the existing <see cref="MeshToVoxels.ObjToVoxConverter"/> loader (the shared
    /// OBJ/FBX + texture-snapshot code) into the core's portable colour types.
    /// </summary>
    public static class ModelLoader
    {
        /// <summary>Load <paramref name="meshPath"/> (.obj/.fbx). Main thread only. Throws on unsupported formats / IO errors.</summary>
        public static LoadedModel Load(string meshPath)
        {
            MeshToVoxelsConverter.LoadedModel loaded = MeshToVoxelsConverter.LoadScene(meshPath);
            return new LoadedModel(loaded.Mesh, loaded.HasUVs, ToCore(loaded.Colors));
        }

        private static ColorSource ToCore(MeshToVoxelsConverter.ColorSource colors) => new()
        {
            Texture = colors.Texture != null ? ToCore(colors.Texture) : null,
            FlatColor = colors.FlatColor.ToCore(),
        };

        private static TextureSnapshot ToCore(MeshToVoxelsConverter.TextureSnapshot snapshot)
        {
            UnityEngine.Color[] linear = snapshot.CopyLinearPixels();
            var pixels = new ColorF[linear.Length];
            for (int i = 0; i < linear.Length; i++)
            {
                pixels[i] = linear[i].ToCoreLinear();
            }
            return new TextureSnapshot(pixels, snapshot.Width, snapshot.Height);
        }
    }
}
