using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels
{
    /// <summary>
    /// A bootstrap ~51-swatch master palette in a flat, cheerful <b>Crossy-Road</b> register
    /// (bright albedo, hard steps, no baked shading). This is the starter art-direction set:
    /// it's meant to be edited — create a <see cref="VoxMasterPalette"/> asset seeded from it
    /// and tune the swatches there. <see cref="PaletteSnap"/> falls back to this when no asset
    /// is assigned.
    /// </summary>
    public static class DefaultMasterPalette
    {
        private static readonly string[] Hex =
        {
            // Greens (grass / foliage)
            "7CB342", "8BC34A", "AED581", "558B2F", "33691E", "9CCC65",
            // Blues / cyans (sky / water)
            "4FC3F7", "29B6F6", "0288D1", "01579B", "81D4FA", "26C6DA",
            // Reds
            "E53935", "EF5350", "C62828", "FF8A80", "D32F2F",
            // Oranges / yellows
            "FB8C00", "FFA726", "FFB300", "FFD54F", "FFF176", "F57F17",
            // Browns (wood / dirt)
            "8D6E63", "6D4C41", "4E342E", "A1887F", "3E2723", "BCAAA4",
            // Purples / pinks
            "AB47BC", "8E24AA", "EC407A", "F06292", "CE93D8",
            // Teals
            "26A69A", "00897B", "4DB6AC",
            // Skin / tan
            "FFCCBC", "FFAB91", "D7A86E", "F5DEB3",
            // Warm neutrals (greige) — a light/mid/low warm-grey ramp. Without these, faintly
            // warm hull/panel colours (ubiquitous in Meshy albedo) have no light warm-neutral to
            // snap to and jump to the saturated pink/salmon swatches above. See PaletteSnap.
            "EFE7DB", "D8CEC1", "BBB1A5",
            // Neutrals (greys + near-white / near-black)
            "FAFAFA", "EEEEEE", "BDBDBD", "9E9E9E", "616161", "424242", "1A1A1A",
        };

        /// <summary>The starter swatches as opaque <see cref="Color32"/>s.</summary>
        public static IReadOnlyList<Color32> Colors { get; } =
            Hex.Select(FromHex).ToArray();

        private static Color32 FromHex(string hex) => new Color32(
            (byte)System.Convert.ToInt32(hex.Substring(0, 2), 16),
            (byte)System.Convert.ToInt32(hex.Substring(2, 2), 16),
            (byte)System.Convert.ToInt32(hex.Substring(4, 2), 16),
            255);
    }
}
