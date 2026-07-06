using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxel.Generation
{
    /// <summary>
    /// Builds the system prompt for the model-config chooser. The settings reference is reflected from
    /// <see cref="SettingsConfig"/> / <see cref="MeshyConfig"/> at runtime so the prompt can never
    /// drift from the real fields, tooltips and ranges. There is no preset — every config baselines on
    /// <see cref="Settings.Defaults"/> and the AI emits field overrides only.
    /// </summary>
    public static class ConfigPromptBuilder
    {
        public static string Build(StyleRules rules, SettingsRules? settingsRules = null)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            var sb = new StringBuilder();

            sb.AppendLine(
                "You configure a voxel-art asset pipeline. Given one model's description and a shared " +
                "art-direction blurb, you return a single config: an image-generation prompt plus the " +
                "settings that turn the generated image into a voxel model. You only emit data — you do " +
                "not run any conversion.");
            sb.AppendLine();

            sb.AppendLine("# Image prompt");
            sb.AppendLine(
                "Write `imagePrompt`: a vivid description of the single model, suitable for an " +
                "image generator. Describe one centred object on a plain background. Fold in the shared " +
                "art direction and any style rules you decide apply (see below).");
            sb.AppendLine();

            sb.AppendLine("# Base name");
            sb.AppendLine(
                "Write `baseName`: a short, filesystem-safe identifier for this asset that names its " +
                "output files (the shared base for the .png / .obj / .vox). Use lowercase words joined " +
                "by underscores, no spaces or punctuation, ideally 1–3 words — e.g. `red_dragon`, " +
                "`wooden_crate`, `steam_locomotive`.");
            sb.AppendLine();

            sb.AppendLine("# Settings overrides");
            sb.AppendLine(
                "The pipeline has sensible defaults. In `settings`, include ONLY the fields you want to " +
                "change from those defaults — omit everything else. By default `MaxDimVoxels` is the " +
                "resolution: the longest bounding-box axis in voxels (4–96), the other axes scaling " +
                "proportionally. Keep it low (~10–24) for a chunky stylised read; higher captures more " +
                "detail but costs much more to convert, so only go high for assets that genuinely need " +
                "it. Alternatively set `ResolutionInput` to `WorldSize` and give `TargetWorldSize` (the " +
                "object's longest real-world dimension in metres) and `VoxelWorldSize` (metres per " +
                "voxel); the resolution is then their ratio, clamped to 4–96. Available fields:");
            foreach (var field in SettingsFields())
            {
                sb.AppendLine(DescribeField(field));
            }
            sb.AppendLine();
            sb.AppendLine(
                "`Symmetry` is None in the defaults and forcing it can ruin an asset, so leave it None " +
                "unless the model clearly calls for it. Enable it (e.g. `\"X\"`) only for an asset meant " +
                "to be bilaterally symmetric (most creatures, characters, vehicles) whose description " +
                "implies no deliberate one-sided feature (an eyepatch, a raised paw, a logo on one side); " +
                "the axis is usually X (left/right). When in doubt, leave it None.");
            sb.AppendLine();

            if (settingsRules is { Rules: { Count: > 0 } settingsRuleList })
            {
                sb.AppendLine("# Settings rules");
                sb.AppendLine(
                    "These shared rules tell you HOW to choose the settings above for the kind of object " +
                    "being made. Apply a rule ONLY when it is relevant to this asset; when it applies, set " +
                    "the settings fields it names in `settings`, and list the ids you applied in " +
                    "`appliedSettingsRuleIds`. These drive the voxel settings, not the image prompt.");
                foreach (var rule in settingsRuleList)
                {
                    sb.AppendLine($"- {rule.id} — {rule.text} (apply when: {rule.appliesWhen})");
                }
                sb.AppendLine();
            }

            sb.AppendLine("# Mesh generation (Meshy)");
            sb.AppendLine(
                "The image is turned into a 3D mesh by Meshy.ai before voxelizing. Choose the mesh " +
                "generation parameters in `meshy`; include ONLY the fields you want to change from the " +
                "defaults shown. Available fields:");
            foreach (var field in MeshyFields())
            {
                sb.AppendLine(DescribeField(field));
            }
            sb.AppendLine();

            sb.AppendLine("# Style rules");
            sb.AppendLine(
                "These shared rules constrain the look. Apply a rule ONLY when it is relevant to this " +
                "asset, and list the ids you applied in `appliedRuleIds`. When a rule applies, fold its " +
                "instruction into the image prompt. Do NOT mention a rule that is irrelevant to this " +
                "asset — naming irrelevant constraints degrades the generated image.");
            foreach (var rule in rules.Rules)
            {
                sb.AppendLine($"- {rule.id} — {rule.text} (apply when: {rule.appliesWhen})");
            }
            sb.AppendLine();

            sb.AppendLine("# Output");
            sb.AppendLine("Reply with exactly one fenced ```json block and nothing else, in this shape:");
            sb.AppendLine("```json");
            sb.AppendLine("{");
            sb.AppendLine("  \"imagePrompt\": \"...\",");
            sb.AppendLine("  \"baseName\": \"...\",");
            sb.AppendLine("  \"appliedRuleIds\": [\"rule-id\", ...],");
            if (settingsRules is { Rules: { Count: > 0 } })
            {
                sb.AppendLine("  \"appliedSettingsRuleIds\": [\"rule-id\", ...],");
            }
            sb.AppendLine("  \"settings\": { },");
            sb.AppendLine("  \"meshy\": { }");
            sb.AppendLine("}");
            sb.AppendLine("```");

            return sb.ToString();
        }

        private static FieldInfo[] SettingsFields() =>
            typeof(SettingsConfig).GetFields(BindingFlags.Public | BindingFlags.Instance);

        private static FieldInfo[] MeshyFields() =>
            typeof(MeshyConfig).GetFields(BindingFlags.Public | BindingFlags.Instance);

        private static string DescribeField(FieldInfo field)
        {
            var tooltip = field.GetCustomAttribute<TooltipAttribute>()?.tooltip ?? string.Empty;
            var range = field.GetCustomAttribute<RangeAttribute>();
            var suffix = string.IsNullOrEmpty(tooltip) ? string.Empty : $": {tooltip}";
            return $"- {field.Name} ({DescribeType(field.FieldType, range)}, default {DescribeDefault(field)}){suffix}";
        }

        private static string DescribeDefault(FieldInfo field)
        {
            // A fresh instance carries each field's initializer value — the true default the AI
            // should assume when it omits a field. Cache per declaring type to avoid re-allocating.
            var instance = DefaultInstance(field.DeclaringType);
            var value = field.GetValue(instance);
            return value switch
            {
                bool b => b ? "true" : "false",
                null => "null",
                _ => value.ToString()
            };
        }

        private static readonly Dictionary<Type, object> DefaultInstances = new();

        private static object DefaultInstance(Type type)
        {
            if (!DefaultInstances.TryGetValue(type, out var instance))
            {
                // SettingsConfig's authoritative defaults come from Settings.Defaults via Seeded(),
                // not its field initializers (which could drift); everything else uses its own.
                instance = type == typeof(SettingsConfig)
                    ? SettingsConfig.Seeded()
                    : Activator.CreateInstance(type);
                DefaultInstances[type] = instance;
            }
            return instance;
        }

        private static string DescribeType(Type type, RangeAttribute? range)
        {
            if (type == typeof(bool))
            {
                return "bool";
            }
            if (type.IsEnum)
            {
                var names = $"enum: {string.Join("|", Enum.GetNames(type))}";
                // [Flags] enums (e.g. Symmetry) take a comma-separated combination of the names.
                return type.IsDefined(typeof(FlagsAttribute), false) ? $"{names}; combine with commas" : names;
            }
            if (type == typeof(int))
            {
                return range != null ? $"int, {(int)range.min}–{(int)range.max}" : "int";
            }
            if (type == typeof(float))
            {
                return range != null ? $"float, {range.min}–{range.max}" : "float";
            }
            return type.Name;
        }
    }
}
