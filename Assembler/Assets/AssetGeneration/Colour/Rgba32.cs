namespace Assembler.AssetGeneration.Colour
{
    /// <summary>
    /// Portable 8-bit sRGB RGBA colour — the engine-free stand-in for <c>UnityEngine.Color32</c> in
    /// the voxel core, so the pipeline can run outside Unity. Field names/order mirror
    /// <c>Color32</c> (<c>r,g,b,a</c> bytes) so the boundary conversion in the editor layer is a
    /// straight field copy. Value equality is defined so palette/consolidation code and tests can
    /// compare colours directly.
    /// </summary>
    public readonly struct Rgba32
    {
        public readonly byte r;
        public readonly byte g;
        public readonly byte b;
        public readonly byte a;

        public Rgba32(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public bool Equals(Rgba32 other) => r == other.r && g == other.g && b == other.b && a == other.a;

        public override bool Equals(object? obj) => obj is Rgba32 other && Equals(other);

        public override int GetHashCode() => (r << 24) | (g << 16) | (b << 8) | a;

        public static bool operator ==(Rgba32 x, Rgba32 y) => x.Equals(y);

        public static bool operator !=(Rgba32 x, Rgba32 y) => !x.Equals(y);

        public override string ToString() => $"RGBA({r}, {g}, {b}, {a})";
    }
}
