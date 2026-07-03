namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// A loaded mesh ready for voxelisation: the g3 geometry plus its colour source. Engine-free —
    /// the editor-side loader (which reads .obj/.fbx and decodes textures with Unity) produces this,
    /// and <see cref="MeshVoxeliser"/> consumes it without touching Unity, so the pipeline can run
    /// headlessly (or outside Unity entirely) given a pre-loaded model.
    /// </summary>
    public sealed class LoadedModel
    {
        public g3.DMesh3 Mesh { get; }
        public bool HasUVs { get; }
        public ColorSource Colors { get; }

        public LoadedModel(g3.DMesh3 mesh, bool hasUVs, ColorSource colors)
        {
            Mesh = mesh;
            HasUVs = hasUVs;
            Colors = colors;
        }
    }

    /// <summary>Where a surface point's colour comes from: a texture snapshot, else the flat material colour.</summary>
    public sealed class ColorSource
    {
        public TextureSnapshot? Texture { get; init; }
        public Rgba32 FlatColor { get; init; }
        public bool HasTexture => Texture != null;
    }

    /// <summary>
    /// A CPU-side, engine-free copy of a texture's pixels (decoded to <b>linear</b> space) plus a
    /// managed bilinear sampler — the portable stand-in for <c>Texture2D.GetPixelBilinear</c>.
    /// Captured on the main thread by the editor loader; sampled here with no Unity dependency.
    /// </summary>
    public sealed class TextureSnapshot
    {
        private readonly ColorF[] _linearPixels; // row-major, (0,0) bottom-left
        private readonly int _width;
        private readonly int _height;

        public TextureSnapshot(ColorF[] linearPixels, int width, int height)
        {
            _linearPixels = linearPixels;
            _width = width;
            _height = height;
        }

        public int Width => _width;
        public int Height => _height;

        /// <summary>Copy of the linear-space pixel buffer (row-major, (0,0) bottom-left).</summary>
        public ColorF[] CopyLinearPixels() => (ColorF[])_linearPixels.Clone();

        /// <summary>
        /// Repeat-wrapped bilinear sample in LINEAR space — the off-Unity equivalent of
        /// <c>Texture2D.GetPixelBilinear</c> in a linear-colour-space project. Callers re-encode the
        /// result to gamma (<see cref="ColorF.ToGamma32"/>) for the (sRGB) stored voxel colour.
        /// </summary>
        public ColorF SampleBilinear(float u, float v)
        {
            if (_width <= 0 || _height <= 0)
            {
                return ColorF.Clear;
            }

            float fx = FMath.Repeat(u, 1f) * _width - 0.5f;
            float fy = FMath.Repeat(v, 1f) * _height - 0.5f;
            int x0 = FMath.FloorToInt(fx);
            int y0 = FMath.FloorToInt(fy);
            float tx = fx - x0;
            float ty = fy - y0;

            ColorF bottom = ColorF.Lerp(Texel(x0, y0), Texel(x0 + 1, y0), tx);
            ColorF top = ColorF.Lerp(Texel(x0, y0 + 1), Texel(x0 + 1, y0 + 1), tx);
            return ColorF.Lerp(bottom, top, ty);
        }

        private ColorF Texel(int x, int y)
        {
            x = ((x % _width) + _width) % _width;
            y = ((y % _height) + _height) % _height;
            return _linearPixels[y * _width + x];
        }
    }
}
