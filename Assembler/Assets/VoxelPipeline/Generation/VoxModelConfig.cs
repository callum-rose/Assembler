using System.Collections.Generic;

namespace Assembler.AssetGeneration.VoxelPipeline.Generation
{
    /// <summary>
    /// The data the AI layer emits for one model: an image-generation prompt plus the settings for
    /// the full pipeline — the image → mesh (Meshy) parameters (<see cref="Meshy"/>) and the
    /// mesh → voxel conversion (<see cref="Preset"/> + <see cref="Settings"/> + <see cref="Resolution"/>).
    /// The layer only produces this — the caller routes <see cref="ImagePrompt"/> to image generation
    /// and the rest to the conversion stages.
    /// </summary>
    public sealed record VoxModelConfig(
        string RawText,
        string ImagePrompt,
        IReadOnlyList<string> AppliedRuleIds,
        VoxPipelinePreset Preset,
        int Resolution,
        VoxPipelineSettings Settings,
        VoxMeshyConfig Meshy);
}
