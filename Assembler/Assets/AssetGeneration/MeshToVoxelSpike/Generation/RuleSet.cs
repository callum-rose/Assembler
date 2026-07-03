using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike.Generation
{
    /// <summary>
    /// A loaded set of shared rules (<see cref="StyleRule"/>: id / text / appliesWhen) backed by a
    /// JSON resource so the set can be extended without a recompile. Concrete subclasses fix the
    /// resource path and the noun used in error messages (<see cref="StyleRules"/> for the
    /// appearance rules folded into the image prompt, <see cref="SettingsRules"/> for the rules that
    /// pick the voxel-pipeline settings). A missing or malformed resource is a hard, clearly-described
    /// error rather than a silent empty set.
    /// </summary>
    public abstract class RuleSet
    {
        private readonly HashSet<string> _ids;

        public IReadOnlyList<StyleRule> Rules { get; }
        public IReadOnlyCollection<string> Ids => _ids;

        protected RuleSet(IReadOnlyList<StyleRule> rules)
        {
            Rules = rules;
            _ids = new HashSet<string>(rules.Select(rule => rule.id));
        }

        public bool IsKnown(string id) => _ids.Contains(id);

        /// <summary>Loads the rule list from a Resources <see cref="TextAsset"/>, or throws when it is absent.</summary>
        protected static IReadOnlyList<StyleRule> LoadRules(string resourcePath, string what)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            return asset != null
                ? ParseRules(asset.text, what)
                : throw new FileNotFoundException($"Voxel {what} resource '{resourcePath}' is missing.");
        }

        /// <summary>Parses a rule list from a JSON string (no Resources lookup — used by tests).</summary>
        protected static IReadOnlyList<StyleRule> ParseRules(string json, string what)
        {
            StyleRuleSet? set;
            try
            {
                set = JsonUtility.FromJson<StyleRuleSet>(json);
            }
            catch (Exception e)
            {
                throw new InvalidDataException($"Voxel {what} JSON is malformed.", e);
            }

            if (set?.rules is not { } rules)
            {
                throw new InvalidDataException($"Voxel {what} JSON is malformed or has no 'rules' array.");
            }

            return rules;
        }
    }
}
