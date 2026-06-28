using System.Collections.Generic;
using UnityEngine;

namespace VoxelsFromMeshSpike
{
    /// <summary>
    /// Thin <see cref="IVoxStep"/> adapters over the stateless step algorithms
    /// (<see cref="FloaterRemoval"/>, <see cref="DeLight"/>, <see cref="PaletteSnap"/>,
    /// <see cref="Morphology"/>). Each wrapper owns its enabled flag and params so the
    /// pipeline can treat every step uniformly. The algorithms stay independently testable;
    /// these just bind config to them.
    /// </summary>
    public sealed class RemoveFloatersStep : IVoxStep
    {
        private readonly FloaterRemoval.Options _options;

        public RemoveFloatersStep(bool enabled, FloaterRemoval.Options options)
        {
            Enabled = enabled;
            _options = options;
        }

        public string Name => "Remove floaters";
        public bool Enabled { get; }
        public void Apply(VoxModel model) => FloaterRemoval.Apply(model, _options);
    }

    public sealed class MirrorStep : IVoxStep
    {
        private readonly Mirror.Options _options;

        public MirrorStep(bool enabled, Mirror.Options options)
        {
            Enabled = enabled;
            _options = options;
        }

        public string Name => "Mirror (symmetry)";
        public bool Enabled { get; }
        public void Apply(VoxModel model) => Mirror.Apply(model, _options);
    }

    public sealed class RevolveStep : IVoxStep
    {
        private readonly Revolve.Options _options;

        public RevolveStep(bool enabled, Revolve.Options options)
        {
            Enabled = enabled;
            _options = options;
        }

        public string Name => "Revolve (symmetry)";
        public bool Enabled { get; }
        public void Apply(VoxModel model) => Revolve.Apply(model, _options);
    }

    public sealed class DeLightStep : IVoxStep
    {
        private readonly DeLight.Options _options;

        public DeLightStep(bool enabled, DeLight.Options options)
        {
            Enabled = enabled;
            _options = options;
        }

        public string Name => "De-light";
        public bool Enabled { get; }
        public void Apply(VoxModel model) => DeLight.Apply(model, _options);
    }

    public sealed class PaletteSnapStep : IVoxStep
    {
        private readonly IReadOnlyList<Color32> _palette;

        public PaletteSnapStep(bool enabled, IReadOnlyList<Color32> palette)
        {
            Enabled = enabled;
            _palette = palette;
        }

        public string Name => "Snap to master palette";
        public bool Enabled { get; }
        public void Apply(VoxModel model) => PaletteSnap.Apply(model, _palette);
    }

    public sealed class MorphologyStep : IVoxStep
    {
        private readonly Morphology.Options _options;

        public MorphologyStep(bool enabled, Morphology.Options options)
        {
            Enabled = enabled;
            _options = options;
        }

        public string Name => "Despeckle / fill";
        public bool Enabled { get; }
        public void Apply(VoxModel model) => Morphology.Apply(model, _options);
    }
}
