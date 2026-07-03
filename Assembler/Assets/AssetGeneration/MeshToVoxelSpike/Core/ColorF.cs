namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Portable floating-point RGBA colour — the engine-free stand-in for <c>UnityEngine.Color</c>
    /// used for the <b>linear-space</b> texture pixels the colour reprojection samples. Carries the
    /// arithmetic the UV-island dilation needs (component add / scalar divide) and the linear→sRGB
    /// gamma encode (<see cref="ToGamma32"/>) that mirrors <c>Color.gamma</c> → <c>Color32</c>.
    /// </summary>
    public readonly struct ColorF
    {
        public readonly float r;
        public readonly float g;
        public readonly float b;
        public readonly float a;

        public ColorF(float r, float g, float b, float a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        /// <summary>Transparent black (0,0,0,0) — the accumulation seed, mirroring <c>Color.clear</c>.</summary>
        public static ColorF Clear => new(0f, 0f, 0f, 0f);

        /// <summary>Straight 0–255 → 0–1 expansion (no colour-space change), mirroring the <c>Color32</c>→<c>Color</c> cast.</summary>
        public static ColorF FromRgba32(Rgba32 c) => new(c.r / 255f, c.g / 255f, c.b / 255f, c.a / 255f);

        public static explicit operator ColorF(Rgba32 c) => FromRgba32(c);

        public static ColorF operator +(ColorF x, ColorF y) => new(x.r + y.r, x.g + y.g, x.b + y.b, x.a + y.a);

        public static ColorF operator /(ColorF c, float d) => new(c.r / d, c.g / d, c.b / d, c.a / d);

        public static ColorF operator /(ColorF c, int d) => c / (float)d;

        /// <summary>Component lerp, mirroring <c>Color.Lerp</c> (unclamped t is fine for the internal bilinear use).</summary>
        public static ColorF Lerp(ColorF from, ColorF to, float t) => new(
            from.r + (to.r - from.r) * t,
            from.g + (to.g - from.g) * t,
            from.b + (to.b - from.b) * t,
            from.a + (to.a - from.a) * t);

        /// <summary>
        /// Encode this linear colour to an 8-bit sRGB <see cref="Rgba32"/>, mirroring
        /// <c>UnityEngine.Color.gamma</c> followed by the implicit <c>Color32</c> conversion:
        /// per-channel <see cref="FMath.LinearToGammaSpace"/> on RGB, alpha left linear, then a
        /// clamped round to bytes.
        /// </summary>
        public Rgba32 ToGamma32() => new(
            ToByte(FMath.LinearToGammaSpace(r)),
            ToByte(FMath.LinearToGammaSpace(g)),
            ToByte(FMath.LinearToGammaSpace(b)),
            ToByte(a));

        private static byte ToByte(float v) => (byte)FMath.RoundToInt(FMath.Clamp01(v) * 255f);
    }
}
