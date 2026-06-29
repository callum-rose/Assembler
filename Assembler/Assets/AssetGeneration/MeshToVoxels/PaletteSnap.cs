using System.Collections.Generic;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels
{
    /// <summary>
    /// Pipeline step 5 — the <b>anchored hybrid</b>. Snaps every voxel colour to the nearest
    /// swatch in a shared hand-authored <b>master palette</b>, measured in Oklab. Each model
    /// ends up using only the few swatches it needs (per-model economy) but every colour is
    /// drawn from the shared master set (cross-asset cohesion — a grab-bag of generated models
    /// reads as one game).
    ///
    /// Run after de-light: by then each material region already carries its un-shaded
    /// representative colour, so this snaps the representative (not a shaded sample) and a dark
    /// red can't cross into a neighbouring brown swatch. Works standalone too (just snaps each
    /// distinct voxel colour). The Oklab nearest-swatch logic (and its tuned chroma-gain penalty)
    /// lives in the shared <see cref="PaletteSnapper"/>, so this voxel-space pass and the
    /// texture-space <see cref="TextureQuantize"/> (C2) can't drift apart.
    /// </summary>
    public static class PaletteSnap
    {
        public static void Apply(VoxModel model, IReadOnlyList<Color32> palette)
        {
            if (palette == null || palette.Count == 0)
            {
                return;
            }

            var snapper = new PaletteSnapper(palette);
            for (int i = 0; i < model.Occupied.Length; i++)
            {
                if (!model.Occupied[i])
                {
                    continue;
                }
                model.Colors[i] = snapper.Nearest(model.Colors[i]);
            }
        }
    }
}
