using System.Collections.Generic;

namespace Assembler.AssetGeneration.MeshToVoxelSpike.Generation
{
    /// <summary>
    /// The data the AI layer emits for one model: an image-generation prompt, a short output file
    /// base name (<see cref="BaseName"/>, shared by the image/mesh/.vox), plus the settings for the
    /// full pipeline — the image → mesh (Meshy) parameters (<see cref="Meshy"/>) and the
    /// mesh → voxel conversion (<see cref="Settings"/>, whose <see cref="Settings.MaxDimVoxels"/>
    /// carries the resolution). <see cref="AppliedRuleIds"/> lists the style rules folded into the
    /// image prompt; <see cref="AppliedSettingsRuleIds"/> lists the settings rules applied to
    /// <see cref="Settings"/>. The layer only produces this — the caller routes
    /// <see cref="ImagePrompt"/> to image generation, <see cref="BaseName"/> to the output naming,
    /// and the rest to the conversion stages.
    /// </summary>
    public sealed record ModelConfig(
        string RawText,
        string ImagePrompt,
        string BaseName,
        IReadOnlyList<string> AppliedRuleIds,
        IReadOnlyList<string> AppliedSettingsRuleIds,
        Settings Settings,
        MeshyConfig Meshy);
}
