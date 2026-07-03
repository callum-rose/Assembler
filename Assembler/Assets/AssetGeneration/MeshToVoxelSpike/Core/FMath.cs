using System;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Engine-free float-math helpers — the portable stand-in for the handful of <c>UnityEngine.Mathf</c>
    /// functions the voxel core uses, so it can compile and run without <c>UnityEngine</c>. Rounding
    /// semantics deliberately match Unity's (<see cref="RoundToInt"/> rounds half-to-even, like
    /// <c>Mathf.RoundToInt</c>) because the grid-placement search relies on them for deterministic
    /// tie-breaks. Named <c>FMath</c> rather than <c>Mathf</c> so it never clashes with the real
    /// <c>Mathf</c> in the editor-side assembly that shares this namespace.
    /// </summary>
    public static class FMath
    {
        public static int Max(int a, int b) => a > b ? a : b;

        public static float Max(float a, float b) => a > b ? a : b;

        public static float Max(float a, float b, float c) => Max(Max(a, b), c);

        public static int Min(int a, int b) => a < b ? a : b;

        public static float Min(float a, float b) => a < b ? a : b;

        public static float Min(float a, float b, float c) => Min(Min(a, b), c);

        public static float Abs(float v) => Math.Abs(v);

        public static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        public static float Pow(float f, float p) => (float)Math.Pow(f, p);

        public static float Sqrt(float f) => (float)Math.Sqrt(f);

        public static float Exp(float f) => (float)Math.Exp(f);

        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        // Half-to-even, matching Mathf.RoundToInt (Math.Round defaults to ToEven).
        public static int RoundToInt(float f) => (int)Math.Round(f, MidpointRounding.ToEven);

        public static int FloorToInt(float f) => (int)Math.Floor(f);

        public static int CeilToInt(float f) => (int)Math.Ceiling(f);

        /// <summary>Loops <paramref name="t"/> into <c>[0, length)</c>, mirroring <c>Mathf.Repeat</c>.</summary>
        public static float Repeat(float t, float length) => Clamp(t - (float)Math.Floor(t / length) * length, 0f, length);

        /// <summary>Mirrors <c>Mathf.LinearToGammaSpace</c> (the sRGB OETF Unity applies for <c>Color.gamma</c>).</summary>
        public static float LinearToGammaSpace(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }
            if (value <= 0.0031308f)
            {
                return value * 12.92f;
            }
            if (value < 1f)
            {
                return 1.055f * (float)Math.Pow(value, 0.4166667f) - 0.055f;
            }
            return (float)Math.Pow(value, 0.45454545f);
        }
    }
}
