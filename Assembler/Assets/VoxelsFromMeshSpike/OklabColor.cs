using UnityEngine;

namespace VoxelsFromMeshSpike
{
    /// <summary>
    /// A colour in <b>Oklab</b> perceptual space (L = lightness, a/b = chroma axes).
    /// Region-grow (de-light) and palette-snap compare here rather than in RGB because
    /// raw RGB Euclidean distance mismatches perceived similarity — the reason naive
    /// snapping turns a dark-but-saturated red into brown.
    ///
    /// Conversion follows Björn Ottosson's reference Oklab (sRGB → linear → LMS → Oklab).
    /// </summary>
    public readonly struct OklabColor
    {
        public readonly float L;
        public readonly float A;
        public readonly float B;

        public OklabColor(float l, float a, float b)
        {
            L = l;
            A = a;
            B = b;
        }

        public static OklabColor FromColor32(Color32 c)
        {
            float r = SrgbToLinear(c.r / 255f);
            float g = SrgbToLinear(c.g / 255f);
            float b = SrgbToLinear(c.b / 255f);

            float l = 0.4122214708f * r + 0.5363325363f * g + 0.0514459929f * b;
            float m = 0.2119034982f * r + 0.6806995451f * g + 0.1073969566f * b;
            float s = 0.0883024619f * r + 0.2817188376f * g + 0.6299787005f * b;

            float lCbrt = Cbrt(l);
            float mCbrt = Cbrt(m);
            float sCbrt = Cbrt(s);

            return new OklabColor(
                0.2104542553f * lCbrt + 0.7936177850f * mCbrt - 0.0040720468f * sCbrt,
                1.9779984951f * lCbrt - 2.4285922050f * mCbrt + 0.4505937099f * sCbrt,
                0.0259040371f * lCbrt + 0.7827717662f * mCbrt - 0.8086757660f * sCbrt);
        }

        /// <summary>Chroma (saturation): distance from the neutral axis in the a/b plane.</summary>
        public float Chroma => Mathf.Sqrt(A * A + B * B);

        public float SquaredDistanceTo(OklabColor other)
        {
            float dl = L - other.L;
            float da = A - other.A;
            float db = B - other.B;
            return dl * dl + da * da + db * db;
        }

        public float DistanceTo(OklabColor other) => Mathf.Sqrt(SquaredDistanceTo(other));

        private static float SrgbToLinear(float c) =>
            c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        private static float Cbrt(float x) =>
            x < 0f ? -Mathf.Pow(-x, 1f / 3f) : Mathf.Pow(x, 1f / 3f);
    }
}
