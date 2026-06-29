using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.VoxelPipeline
{
    /// <summary>
    /// A hand-authored master palette asset for <see cref="PaletteSnap"/> — the shared
    /// art-direction knob. Edit the swatches in the inspector; create one seeded from
    /// <see cref="DefaultMasterPalette"/> via the conversion window's "Create starter palette" button
    /// (or right-click ▸ Create ▸ Voxels ▸ Master Palette for an empty one).
    /// </summary>
    [CreateAssetMenu(fileName = "VoxMasterPalette", menuName = "Voxels/Master Palette", order = 0)]
    public sealed class VoxMasterPalette : ScriptableObject
    {
        [SerializeField]
        [Tooltip("Flat, cheerful swatches shared across all assets. Snapped to in Oklab.")]
        private List<Color> _colors = new();

        public IReadOnlyList<Color32> ToColor32() =>
            _colors.Select(c => (Color32)c).ToList();

        public void SetColors(IEnumerable<Color32> colors) =>
            _colors = colors.Select(c => (Color)c).ToList();
    }
}
