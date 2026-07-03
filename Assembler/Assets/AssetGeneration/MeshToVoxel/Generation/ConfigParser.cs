using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Assembler.Anthropic;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxel.Generation
{
    /// <summary>
    /// Turns an assistant reply into a <see cref="ModelConfig"/>, leniently. The only hard failure is
    /// a missing/unparseable json block; everything else falls back to safe defaults: the
    /// <see cref="Settings.Defaults"/> baseline for omitted settings, clamped ranges, and dropped
    /// unknown rule ids.
    ///
    /// Hybrid parse by necessity: <c>System.Text.Json</c> navigates the envelope (rule-id array), then
    /// <see cref="JsonUtility.FromJsonOverwrite"/> applies the partial <c>settings</c> object onto a
    /// Defaults-seeded <see cref="SettingsConfig"/> so omitted fields keep their default value. Enum
    /// settings fields are re-applied by hand afterwards because JsonUtility only understands enums as
    /// ints, while the model naturally emits their names (comma-separated for the [Flags] symmetry).
    /// </summary>
    public static class ConfigParser
    {
        /// <summary>
        /// Parses a full assistant reply: extracts its fenced <c>```json</c> block (a missing block
        /// is the one hard failure) and parses it. <paramref name="rules"/> and
        /// <paramref name="settingsRules"/> are optional — when supplied, unknown applied-rule ids
        /// (style and settings respectively) are dropped; when null, they are kept as-is.
        /// </summary>
        public static ModelConfig Parse(string rawText, StyleRules? rules = null, SettingsRules? settingsRules = null)
        {
            var json = ConfigExtractor.Extract(rawText)
                ?? throw new AnthropicRequestException(200, "AI model-config response contained no ```json block.");
            return ParseJson(json, rules, rawText, settingsRules);
        }

        /// <summary>
        /// Parses an already-extracted JSON config object (no fenced block needed). Used by callers
        /// that paste the config json directly. Same lenient rules as <see cref="Parse"/>.
        /// </summary>
        public static ModelConfig ParseJson(
            string json, StyleRules? rules = null, string? rawText = null, SettingsRules? settingsRules = null)
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
                var baseName = GetString(root, "baseName") ?? string.Empty;

                var settings = SettingsConfig.Seeded();
                ApplyPartialObject(settings, root, "settings");

                var meshy = new MeshyConfig();
                ApplyPartialObject(meshy, root, "meshy");

                var appliedRuleIds = GetStringArray(root, "appliedRuleIds")
                    .Where(id => rules == null || rules.IsKnown(id))
                    .Distinct()
                    .ToList();

                var appliedSettingsRuleIds = GetStringArray(root, "appliedSettingsRuleIds")
                    .Where(id => settingsRules == null || settingsRules.IsKnown(id))
                    .Distinct()
                    .ToList();

                return new ModelConfig(
                    rawText ?? json, imagePrompt, baseName, appliedRuleIds, appliedSettingsRuleIds,
                    settings.ToSettings(), meshy);
            }
        }

        // Applies the named JSON sub-object onto <paramref name="target"/> as a partial overwrite:
        // omitted fields keep their existing (seeded/default) value. Works for any [Serializable]
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
                        // Enum.Parse handles both a single name and a comma-separated [Flags] combination.
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
