using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike.Generation
{
    /// <summary>
    /// The loaded set of shared art-direction rules. Rules live in a JSON resource so they can be
    /// extended without a recompile. A missing or malformed resource is a hard, clearly-described
    /// error rather than a silent empty set.
    /// </summary>
    public sealed class StyleRules
    {
        private const string ResourcePath = "GenerationPrompts/VoxelStyleRules";

        private readonly HashSet<string> _ids;

        public IReadOnlyList<StyleRule> Rules { get; }
        public IReadOnlyCollection<string> Ids => _ids;

        private StyleRules(IReadOnlyList<StyleRule> rules)
        {
            Rules = rules;
            _ids = new HashSet<string>(rules.Select(rule => rule.id));
        }

        /// <summary>Loads the rules from <c>Resources/GenerationPrompts/VoxelStyleRules.json</c>.</summary>
        public static StyleRules Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            return asset != null
                ? Parse(asset.text)
                : throw new FileNotFoundException(
                    $"Voxel style-rules resource '{ResourcePath}' is missing.");
        }

        /// <summary>Parses rules from a JSON string (no Resources lookup — used by tests).</summary>
        public static StyleRules Parse(string json)
        {
            StyleRuleSet? set;
            try
            {
                set = JsonUtility.FromJson<StyleRuleSet>(json);
            }
            catch (Exception e)
            {
                throw new InvalidDataException("Voxel style-rules JSON is malformed.", e);
            }

            if (set?.rules is not { } rules)
            {
                throw new InvalidDataException("Voxel style-rules JSON is malformed or has no 'rules' array.");
            }

            return new StyleRules(rules);
        }

        public bool IsKnown(string id) => _ids.Contains(id);
    }
}
