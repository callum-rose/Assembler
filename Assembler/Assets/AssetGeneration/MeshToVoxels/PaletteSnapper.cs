using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels
{
    /// <summary>
    /// Nearest-master-swatch colour snapping in Oklab, shared by the per-voxel <see cref="PaletteSnap"/>
    /// (pipeline step 5) and the texture-space <see cref="TextureQuantize"/> (C2) so the two paths
    /// can never diverge. Holds the palette in Oklab and caches the snap per distinct RGB colour
    /// (textures have far more pixels than distinct colours, so the cache pays for itself heavily).
    /// </summary>
    public sealed class PaletteSnapper
    {
        // Penalises snapping a near-neutral colour onto a saturated swatch. The squared Oklab
        // distance gets an added cost for any CHROMA the swatch introduces beyond the source's
        // own — only ADDED saturation is penalised, desaturating is free. Without it a faintly
        // warm light grey (with no light warm-neutral swatch to land on) snaps to a saturated
        // pink/salmon of the same hue, turning whole hull panels pink. Tuned (well below the
        // ~15 where it starts overriding clear hue matches) to bias near-neutrals toward
        // neutral swatches without ever flipping a saturated colour onto the wrong hue.
        private const float ChromaGainPenalty = 8f;

        private readonly IReadOnlyList<Color32> _palette;
        private readonly OklabColor[] _paletteLab;
        private readonly Dictionary<int, Color32> _cache = new();

        public PaletteSnapper(IReadOnlyList<Color32> palette)
        {
            _palette = palette;
            _paletteLab = palette.Select(OklabColor.FromColor32).ToArray();
        }

        /// <summary>True when there are no swatches to snap to — callers should leave colours untouched.</summary>
        public bool IsEmpty => _paletteLab.Length == 0;

        /// <summary>The nearest master swatch to <paramref name="c"/> in Oklab (cached per distinct RGB).</summary>
        public Color32 Nearest(Color32 c)
        {
            int key = (c.r << 16) | (c.g << 8) | c.b;
            if (!_cache.TryGetValue(key, out Color32 snapped))
            {
                snapped = _palette[NearestIndex(OklabColor.FromColor32(c))];
                _cache[key] = snapped;
            }
            return snapped;
        }

        private int NearestIndex(OklabColor c)
        {
            int best = 0;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < _paletteLab.Length; i++)
            {
                float gain = Mathf.Max(0f, _paletteLab[i].Chroma - c.Chroma);
                float d = c.SquaredDistanceTo(_paletteLab[i]) + ChromaGainPenalty * gain * gain;
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
