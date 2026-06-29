using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Assembler.VoxelPipeline.Generation
{
    /// <summary>
    /// The loaded set of shared art-direction rules. Rules live in a JSON resource so they can be
    /// extended without a recompile. Mirrors <c>VoxelPromptBuilder.LoadRequired</c>: a missing or
    /// malformed resource is a hard, clearly-described error rather than a silent empty set.
    /// </summary>
    public sealed class VoxStyleRules
    {
        private const string ResourcePath = "GenerationPrompts/VoxelStyleRules";

        private readonly HashSet<string> _ids;

        public IReadOnlyList<VoxStyleRule> Rules { get; }
        public IReadOnlyCollection<string> Ids => _ids;

        private VoxStyleRules(IReadOnlyList<VoxStyleRule> rules)
        {
            Rules = rules;
            _ids = new HashSet<string>(rules.Select(rule => rule.id));
        }

        /// <summary>Loads the rules from <c>Resources/GenerationPrompts/VoxelStyleRules.json</c>.</summary>
        public static VoxStyleRules Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            return asset != null
                ? Parse(asset.text)
                : throw new FileNotFoundException(
                    $"Voxel style-rules resource '{ResourcePath}' is missing.");
        }

        /// <summary>Parses rules from a JSON string (no Resources lookup — used by tests).</summary>
        public static VoxStyleRules Parse(string json)
        {
            VoxStyleRuleSet? set;
            try
            {
                set = JsonUtility.FromJson<VoxStyleRuleSet>(json);
            }
            catch (Exception e)
            {
                throw new InvalidDataException("Voxel style-rules JSON is malformed.", e);
            }

            if (set?.rules is not { } rules)
            {
                throw new InvalidDataException("Voxel style-rules JSON is malformed or has no 'rules' array.");
            }

            return new VoxStyleRules(rules);
        }

        public bool IsKnown(string id) => _ids.Contains(id);
    }
}
