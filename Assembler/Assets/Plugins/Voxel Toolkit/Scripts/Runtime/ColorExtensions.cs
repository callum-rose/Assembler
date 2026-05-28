using UnityEngine;

namespace VoxelToolkit
{
    public static class ColorExtensions
    {
        public static Color ApplyTransformation(this Color color, float hueShift, float saturation, float brightness)
        {
            UnityEngine.Color.RGBToHSV(color, out float h, out float s, out float v);

            var newBrightness = v + brightness;
            if (newBrightness < 0.0f)
                newBrightness = 0.0f;
            else if (newBrightness > 1.0f)
                newBrightness = 1.0f;
            
            return UnityEngine.Color.HSVToRGB(h + hueShift, s * saturation, newBrightness);
        }
    }
}