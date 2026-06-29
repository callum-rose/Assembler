using System.Collections.Generic;

namespace Assembler.VoxelPipeline.Generation
{
    /// <summary>
    /// The data the AI layer emits for one model: an image-generation prompt plus the conversion
    /// settings (preset baseline + overrides + resolution). The layer only produces this — the
    /// caller routes <see cref="ImagePrompt"/> to image generation and the rest to the conversion.
    /// </summary>
    public sealed record VoxModelConfig(
        string RawText,
        string ImagePrompt,
        IReadOnlyList<string> AppliedRuleIds,
        VoxPipelinePreset Preset,
        int Resolution,
        VoxPipelineSettings Settings);
}
