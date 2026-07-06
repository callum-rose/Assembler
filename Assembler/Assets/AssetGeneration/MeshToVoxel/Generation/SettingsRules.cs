using System.Collections.Generic;

namespace Assembler.AssetGeneration.MeshToVoxel.Generation
{
    /// <summary>
    /// The shared rules that tell the model how to CHOOSE the voxel-pipeline settings for the kind of
    /// object being made (resolution, symmetry, colour mode, corner fill, etc.). Unlike
    /// <see cref="StyleRules"/> — which shape the image prompt — these drive the <c>settings</c> block.
    /// Loaded from a JSON resource (see <see cref="RuleSet"/>).
    /// </summary>
    public sealed class SettingsRules : RuleSet
    {
        /// <summary>Resources-relative path (no extension) of the settings-rules JSON.</summary>
        public const string ResourcePath = "GenerationPrompts/VoxelSettingsRules";

        private SettingsRules(IReadOnlyList<StyleRule> rules) : base(rules)
        {
        }

        /// <summary>Loads the rules from <c>Resources/GenerationPrompts/VoxelSettingsRules.json</c>.</summary>
        public static SettingsRules Load() => new(LoadRules(ResourcePath, "settings-rules"));

        /// <summary>Parses rules from a JSON string (no Resources lookup — used by tests).</summary>
        public static SettingsRules Parse(string json) => new(ParseRules(json, "settings-rules"));
    }
}
