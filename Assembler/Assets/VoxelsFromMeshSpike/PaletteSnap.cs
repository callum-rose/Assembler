using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VoxelsFromMeshSpike
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
    /// distinct voxel colour). Snapping is cached per distinct colour.
    /// </summary>
    public static class PaletteSnap
    {
        public static void Apply(VoxModel model, IReadOnlyList<Color32> palette)
        {
            if (palette == null || palette.Count == 0)
            {
                return;
            }

            OklabColor[] paletteLab = palette.Select(OklabColor.FromColor32).ToArray();
            var cache = new Dictionary<int, Color32>();

            for (int i = 0; i < model.Occupied.Length; i++)
            {
                if (!model.Occupied[i])
                {
                    continue;
                }

                Color32 c = model.Colors[i];
                int key = (c.r << 16) | (c.g << 8) | c.b;
                if (!cache.TryGetValue(key, out Color32 snapped))
                {
                    snapped = palette[NearestIndex(OklabColor.FromColor32(c), paletteLab)];
                    cache[key] = snapped;
                }
                model.Colors[i] = snapped;
            }
        }

        private static int NearestIndex(OklabColor c, OklabColor[] paletteLab)
        {
            int best = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < paletteLab.Length; i++)
            {
                float d = c.SquaredDistanceTo(paletteLab[i]);
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = i;
                }
            }
            return best;
        }
    }
}
