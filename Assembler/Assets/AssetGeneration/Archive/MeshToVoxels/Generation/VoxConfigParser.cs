using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Assembler.Anthropic;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels.Generation
{
    /// <summary>
    /// Turns an assistant reply into a <see cref="VoxModelConfig"/>, leniently. The only hard
    /// failure is a missing/unparseable json block; everything else falls back to safe defaults:
    /// preset baseline for omitted settings, clamped ranges, and dropped unknown rule ids.
    ///
    /// Hybrid parse by necessity: <c>System.Text.Json</c> navigates the envelope (string preset,
    /// rule-id array), then <see cref="JsonUtility.FromJsonOverwrite"/> applies the partial
    /// <c>settings</c> object onto the preset instance so omitted fields keep their preset value.
    /// Enum settings fields are re-applied by hand afterwards because JsonUtility only understands
    /// enums as ints, while the model naturally emits their names.
    /// </summary>
    public static class VoxConfigParser
    {
        /// <summary>
        /// Parses a full assistant reply: extracts its fenced <c>```json</c> block (a missing block
        /// is the one hard failure) and parses it. <paramref name="rules"/> is optional — when
        /// supplied, unknown applied-rule ids are dropped; when null, they are kept as-is.
        /// </summary>
        public static VoxModelConfig Parse(string rawText, VoxStyleRules? rules = null)
        {
            var json = VoxConfigExtractor.Extract(rawText)
                ?? throw new AnthropicRequestException(200, "AI model-config response contained no ```json block.");
            return ParseJson(json, rules, rawText);
        }

        /// <summary>
        /// Parses an already-extracted JSON config object (no fenced block needed). Used by callers
        /// that paste the config json directly. Same lenient rules as <see cref="Parse"/>.
        /// </summary>
        public static VoxModelConfig ParseJson(string json, VoxStyleRules? rules = null, string? rawText = null)
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException e)
            {
                throw new AnthropicRequestException(200, "AI model-config json was not valid JSON.", e);
            }

            using (doc)
            {
                var root = doc.RootElement;

                var imagePrompt = GetString(root, "imagePrompt") ?? string.Empty;
                var preset = ParsePreset(root);
                var resolution = Mathf.Clamp(
                    GetInt(root, "resolution", 32),
                    VoxConfigPromptBuilder.MinResolution,
                    VoxConfigPromptBuilder.MaxResolution);

                var settings = VoxPipelinePresets.For(preset);
                ApplyPartialObject(settings, root, "settings");

                var meshy = new VoxMeshyConfig();
                ApplyPartialObject(meshy, root, "meshy");

                var appliedRuleIds = GetStringArray(root, "appliedRuleIds")
                    .Where(id => rules == null || rules.IsKnown(id))
                    .Distinct()
                    .ToList();

                return new VoxModelConfig(rawText ?? json, imagePrompt, appliedRuleIds, preset, resolution, settings, meshy);
            }
        }

        private static VoxPipelinePreset ParsePreset(JsonElement root)
        {
            var raw = GetString(root, "preset");
            return raw != null && Enum.TryParse<VoxPipelinePreset>(raw, ignoreCase: true, out var preset)
                ? preset
                : VoxPipelinePreset.Creature;
        }

        // Applies the named JSON sub-object onto <paramref name="target"/> as a partial overwrite:
        // omitted fields keep their existing (preset/default) value. Works for any [Serializable]
        // settings object — JsonUtility handles the field overwrite, then enums (which JsonUtility
        // only understands as ints) are re-applied by name, and [Range] fields are clamped.
        private static void ApplyPartialObject(object target, JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            JsonUtility.FromJsonOverwrite(el.GetRawText(), target);
            OverwriteEnumFields(target, el);
            ClampRanges(target);
        }

        private static void OverwriteEnumFields(object target, JsonElement el)
        {
            foreach (var field in Fields(target).Where(f => f.FieldType.IsEnum))
            {
                if (!el.TryGetProperty(field.Name, out var value))
                {
                    continue;
                }

                if (value.ValueKind == JsonValueKind.String && value.GetString() is { } name)
                {
                    try
                    {
                        field.SetValue(target, Enum.Parse(field.FieldType, name, ignoreCase: true));
                    }
                    catch (ArgumentException)
                    {
                        // Unrecognised name — leave the existing value in place.
                    }
                }
                else if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i) && Enum.IsDefined(field.FieldType, i))
                {
                    field.SetValue(target, Enum.ToObject(field.FieldType, i));
                }
            }
        }

        private static void ClampRanges(object target)
        {
            foreach (var field in Fields(target))
            {
                if (field.GetCustomAttribute<RangeAttribute>() is not { } range)
                {
                    continue;
                }

                if (field.FieldType == typeof(float))
                {
                    field.SetValue(target, Mathf.Clamp((float)field.GetValue(target)!, range.min, range.max));
                }
                else if (field.FieldType == typeof(int))
                {
                    field.SetValue(target, Mathf.Clamp((int)field.GetValue(target)!, (int)range.min, (int)range.max));
                }
            }
        }

        private static FieldInfo[] Fields(object target) =>
            target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);

        private static string? GetString(JsonElement root, string name) =>
            root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String
                ? el.GetString()
                : null;

        private static int GetInt(JsonElement root, string name, int fallback) =>
            root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var i)
                ? i
                : fallback;

        private static IEnumerable<string> GetStringArray(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out var el) || el.ValueKind != JsonValueKind.Array)
            {
                yield break;
            }

            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString() is { } s)
                {
                    yield return s;
                }
            }
        }
    }
}
