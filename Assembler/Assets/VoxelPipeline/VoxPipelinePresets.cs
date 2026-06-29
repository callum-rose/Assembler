using System.ComponentModel;

namespace Assembler.VoxelPipeline
{
    /// <summary>
    /// Named category presets — sensible starting bundles per asset kind (§4.3). Each member
    /// carries a <see cref="DescriptionAttribute"/> mirroring its XML doc so the AI prompt builder
    /// can reflect the same prose (XML <c>&lt;summary&gt;</c> isn't reflectable at runtime).
    /// </summary>
    public enum VoxPipelinePreset
    {
        /// <summary>Organic Meshy models: clean + flatten colour, but keep thin features (morphology off).</summary>
        [Description("Organic Meshy models: clean + flatten colour, but keep thin features (morphology off).")]
        Creature,

        /// <summary>Hard-surface props: same colour treatment plus mild morphological closing.</summary>
        [Description("Hard-surface props: same colour treatment plus mild morphological closing.")]
        Prop,

        /// <summary>A voxel source that is already flat-coloured: tidy geometry only, leave colour alone.</summary>
        [Description("A voxel source that is already flat-coloured: tidy geometry only, leave colour alone.")]
        RawVoxelCleanup,
    }

    /// <summary>
    /// Presets are just data: each returns a fresh <see cref="VoxPipelineSettings"/>. The window
    /// loads one as the default, then the per-step toggles act as the per-asset override on top.
    /// </summary>
    public static class VoxPipelinePresets
    {
        public static VoxPipelineSettings For(VoxPipelinePreset preset)
        {
            switch (preset)
            {
                case VoxPipelinePreset.Creature:
                    return new VoxPipelineSettings
                    {
                        removeFloaters = true,
                        floaterMinPercent = 0.5f,
                        deLight = true,
                        deLightThreshold = 0.12f,
                        snapToPalette = true,
                        morphology = false, // preserve antennae, legs, thin features
                    };

                case VoxPipelinePreset.Prop:
                    return new VoxPipelineSettings
                    {
                        removeFloaters = true,
                        floaterMinPercent = 0.5f,
                        deLight = true,
                        deLightThreshold = 0.10f,
                        snapToPalette = true,
                        morphology = true, // hard surfaces tolerate closing
                    };

                case VoxPipelinePreset.RawVoxelCleanup:
                    return new VoxPipelineSettings
                    {
                        removeFloaters = true,
                        floaterMinPercent = 0.25f,
                        deLight = false, // input is already flat-coloured
                        snapToPalette = false,
                        morphology = true,
                    };

                default:
                    return new VoxPipelineSettings();
            }
        }
    }
}
