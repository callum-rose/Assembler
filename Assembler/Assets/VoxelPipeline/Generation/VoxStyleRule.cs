using System;
using System.Collections.Generic;

namespace Assembler.VoxelPipeline.Generation
{
    /// <summary>
    /// One shared art-direction rule. Kept as a <see cref="SerializableAttribute"/> class with
    /// mutable public fields rather than a record because <see cref="UnityEngine.JsonUtility"/>
    /// (used to load the rule set) only populates public fields.
    /// <see cref="appliesWhen"/> describes the situations the rule is relevant to, so the model
    /// can decide whether to apply it to a given asset.
    /// </summary>
    [Serializable]
    public sealed class VoxStyleRule
    {
        public string id = string.Empty;
        public string text = string.Empty;
        public string appliesWhen = string.Empty;
    }

    /// <summary>
    /// Wrapper around the rule list. <see cref="UnityEngine.JsonUtility"/> can't deserialize a
    /// top-level JSON array, so the rules file nests them under a <c>rules</c> object field.
    /// </summary>
    [Serializable]
    public sealed class VoxStyleRuleSet
    {
        public List<VoxStyleRule> rules = new();
    }
}
