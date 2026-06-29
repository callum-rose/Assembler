using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.AssetGeneration.VoxelPipeline
{
    /// <summary>
    /// Fixed-order runner: holds an ordered list of <see cref="IVoxStep"/> and applies the
    /// enabled ones in sequence. Order is canonical and baked into <see cref="FromSettings"/>
    /// (§4.4) — it is intentionally <i>not</i> a reorderable graph, because the valid orderings
    /// are essentially unique (floaters before colour; de-light before palette-snap; morphology last).
    /// </summary>
    public sealed class VoxPipeline
    {
        private readonly IReadOnlyList<IVoxStep> _steps;

        public VoxPipeline(IReadOnlyList<IVoxStep> steps)
        {
            _steps = steps ?? throw new ArgumentNullException(nameof(steps));
        }

        public IReadOnlyList<IVoxStep> Steps => _steps;

        /// <summary>
        /// Builds the canonical-order pipeline from settings. The palette is passed in separately
        /// (it is a shared, window-level art-direction knob, not a per-asset setting). The opt-in
        /// symmetry steps (mirror, then revolve) slot in between floaters and de-light, per §4.4.
        /// </summary>
        public static VoxPipeline FromSettings(VoxPipelineSettings settings, IReadOnlyList<Color32> palette)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var steps = new List<IVoxStep>
            {
                new RemoveFloatersStep(
                    settings.removeFloaters,
                    new FloaterRemoval.Options(2, settings.floaterMinPercent / 100f)),
                // Symmetry (§6.4) runs here, between floaters and de-light, while colour is still raw.
                new MirrorStep(
                    settings.mirror,
                    new Mirror.Options(
                        settings.mirrorAxis,
                        settings.mirrorConfidence,
                        settings.mirrorForce,
                        Mirror.Options.Default.ColourWeight,
                        Mirror.Options.Default.ColourTolerance)),
                new RevolveStep(
                    settings.revolve,
                    new Revolve.Options(settings.revolveAxis, settings.revolveFillThreshold)),
                new DeLightStep(
                    settings.deLight,
                    new DeLight.Options(settings.deLightThreshold)),
                // Per-model colour reduction (top-N histogram peaks) runs before the shared-palette
                // snap: collapse a noisy model to its own dominant colours, then map those onto the
                // master swatches.
                new HistogramSnapStep(
                    settings.snapToHistogramPeaks,
                    settings.histogramPeakCount,
                    settings.histogramPeakVariety),
                new PaletteSnapStep(
                    settings.snapToPalette,
                    palette),
                new MorphologyStep(
                    settings.morphology,
                    Morphology.Options.Default),
            };
            return new VoxPipeline(steps);
        }

        /// <summary>
        /// Applies every enabled step to <paramref name="model"/> in order. <paramref name="onStep"/>,
        /// if supplied, is called before each enabled step with its name and a 0..1 progress fraction.
        /// </summary>
        public void Run(VoxModel model, Action<string, float>? onStep = null)
        {
            int enabledCount = 0;
            foreach (IVoxStep step in _steps)
            {
                if (step.Enabled)
                {
                    enabledCount++;
                }
            }

            int done = 0;
            foreach (IVoxStep step in _steps)
            {
                if (!step.Enabled)
                {
                    continue;
                }
                onStep?.Invoke(step.Name, enabledCount == 0 ? 1f : (float)done / enabledCount);
                step.Apply(model);
                done++;
            }
        }

        /// <summary>Convenience: round-trip a <see cref="VoxResult"/> through a working model.</summary>
        public VoxResult Run(VoxResult result, Action<string, float>? onStep = null)
        {
            VoxModel model = VoxModel.FromResult(result);
            Run(model, onStep);
            return model.ToResult();
        }
    }
}
