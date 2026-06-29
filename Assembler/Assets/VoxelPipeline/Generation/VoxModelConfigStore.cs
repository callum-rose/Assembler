using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEngine;

namespace Assembler.VoxelPipeline.Generation
{
    /// <summary>
    /// EditorPrefs-backed hand-off for the most recently chosen <see cref="VoxModelConfig"/>. The AI
    /// config window saves here; a conversion window (e.g. the text → voxel pipeline window) imports
    /// from here. This keeps the two windows decoupled — the consumer depends on this layer, not the
    /// other way around — while still passing a typed config rather than loose prefs.
    /// </summary>
    public static class VoxModelConfigStore
    {
        private const string Prefix = "Assembler.VoxelPipeline.Generation.LastConfig.";

        public static bool HasStored => EditorPrefs.GetBool(Prefix + "HasValue", false);

        public static void Save(VoxModelConfig config)
        {
            EditorPrefs.SetString(Prefix + "ImagePrompt", config.ImagePrompt);
            EditorPrefs.SetInt(Prefix + "Preset", (int)config.Preset);
            EditorPrefs.SetInt(Prefix + "Resolution", config.Resolution);
            EditorPrefs.SetString(Prefix + "Settings", JsonUtility.ToJson(config.Settings));
            EditorPrefs.SetString(Prefix + "AppliedRuleIds", string.Join("\n", config.AppliedRuleIds));
            EditorPrefs.SetBool(Prefix + "HasValue", true);
        }

        public static bool TryLoad([NotNullWhen(true)] out VoxModelConfig? config)
        {
            config = null;
            if (!HasStored)
            {
                return false;
            }

            var settings = new VoxPipelineSettings();
            var settingsJson = EditorPrefs.GetString(Prefix + "Settings", "");
            if (!string.IsNullOrEmpty(settingsJson))
            {
                JsonUtility.FromJsonOverwrite(settingsJson, settings);
            }

            var ruleIdsRaw = EditorPrefs.GetString(Prefix + "AppliedRuleIds", "");
            var ruleIds = string.IsNullOrEmpty(ruleIdsRaw)
                ? new List<string>()
                : new List<string>(ruleIdsRaw.Split('\n'));

            config = new VoxModelConfig(
                RawText: "",
                ImagePrompt: EditorPrefs.GetString(Prefix + "ImagePrompt", ""),
                AppliedRuleIds: ruleIds,
                Preset: (VoxPipelinePreset)EditorPrefs.GetInt(Prefix + "Preset", (int)VoxPipelinePreset.Creature),
                Resolution: EditorPrefs.GetInt(Prefix + "Resolution", 32),
                Settings: settings);
            return true;
        }
    }
}
