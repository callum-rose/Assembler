using System.Collections.Generic;

namespace Assembler.AssetGeneration.MeshToVoxelSpike.Generation
{
    /// <summary>
    /// The shared art-direction rules that constrain a model's appearance — the model folds the
    /// applicable ones into the image prompt. Loaded from a JSON resource (see <see cref="RuleSet"/>).
    /// </summary>
    public sealed class StyleRules : RuleSet
    {
        /// <summary>Resources-relative path (no extension) of the style-rules JSON.</summary>
        public const string ResourcePath = "GenerationPrompts/VoxelStyleRules";

        private StyleRules(IReadOnlyList<StyleRule> rules) : base(rules)
        {
        }

        /// <summary>Loads the rules from <c>Resources/GenerationPrompts/VoxelStyleRules.json</c>.</summary>
        public static StyleRules Load() => new(LoadRules(ResourcePath, "style-rules"));

        /// <summary>Parses rules from a JSON string (no Resources lookup — used by tests).</summary>
        public static StyleRules Parse(string json) => new(ParseRules(json, "style-rules"));
    }
}
