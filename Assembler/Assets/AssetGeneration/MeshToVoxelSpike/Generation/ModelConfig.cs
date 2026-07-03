using System.Collections.Generic;

namespace Assembler.AssetGeneration.MeshToVoxelSpike.Generation
{
    /// <summary>
    /// The data the AI layer emits for one model: an image-generation prompt plus the settings for
    /// the full pipeline — the image → mesh (Meshy) parameters (<see cref="Meshy"/>) and the
    /// mesh → voxel conversion (<see cref="Settings"/>, whose <see cref="Settings.MaxDimVoxels"/>
    /// carries the resolution). The layer only produces this — the caller routes
    /// <see cref="ImagePrompt"/> to image generation and the rest to the conversion stages.
    /// </summary>
    public sealed record ModelConfig(
        string RawText,
        string ImagePrompt,
        IReadOnlyList<string> AppliedRuleIds,
        Settings Settings,
        MeshyConfig Meshy);
}
