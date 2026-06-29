using System;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels.Generation
{
    /// <summary>
    /// Builds the system prompt for the model-config chooser. The settings reference and the preset
    /// list are reflected from <see cref="VoxPipelineSettings"/> / <see cref="VoxPipelinePreset"/>
    /// at runtime so the prompt can never drift from the real fields, tooltips and ranges.
    /// </summary>
    public static class VoxConfigPromptBuilder
    {
        public const int MinResolution = 1;
        public const int MaxResolution = 256;

        public static string Build(VoxStyleRules rules)
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
            sb.AppendLine(
                "Decide `resolution` (see below) FIRST, then write `imagePrompt` for that voxel budget: " +
                "every distinct colour region and protruding feature must span at least ~2 voxels at the " +
                "chosen resolution — i.e. roughly 2/`resolution` of the model's longest extent or larger. " +
                "Describe nothing finer than that budget (no fine surface texture, thin lines, small " +
                "multi-colour patterns, or gradients), and state the budget explicitly in the prompt you " +
                "emit (e.g. \"rendered as a ~N-voxel-tall low-resolution voxel model, no detail smaller " +
                "than two voxels\"), so the generator doesn't invent detail the voxelizer will mud.");
            sb.AppendLine();

            sb.AppendLine("# Pipeline preset");
            sb.AppendLine("Pick the `preset` whose baseline best fits the asset:");
            foreach (var preset in Enum.GetValues(typeof(VoxPipelinePreset)).Cast<VoxPipelinePreset>())
            {
                sb.AppendLine($"- {preset} — {DescribePreset(preset)}");
            }
            sb.AppendLine();

            sb.AppendLine("# Settings overrides");
            sb.AppendLine(
                "The preset supplies sensible defaults. In `settings`, include ONLY the fields you want " +
                "to change from the chosen preset's baseline — omit everything else. Available fields:");
            foreach (var field in SettingsFields())
            {
                sb.AppendLine(DescribeField(field));
            }
            sb.AppendLine();
            sb.AppendLine(
                "Symmetry (`mirror`, `revolve`) is off in every preset and forcing it can ruin an " +
                "asset, so leave both off unless the model clearly calls for it. Enable `mirror` only " +
                "for an asset meant to be bilaterally symmetric (most creatures, characters, vehicles) " +
                "whose description implies no deliberate one-sided feature (an eyepatch, a raised paw, " +
                "a logo on one side); set `mirrorAxis` to the left/right axis (usually X). Enable " +
                "`revolve` only for a standalone rotationally-symmetric solid (a wheel, barrel, " +
                "bottle, gear); set `revolveAxis` to its spin axis. When in doubt, leave both off.");
            sb.AppendLine();

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

            sb.AppendLine("# Resolution");
            sb.AppendLine(
                $"`resolution` is the longest bounding-box axis in voxels ({MinResolution}–{MaxResolution}); " +
                "the other axes scale proportionally. 32 is a good default. Higher values capture more " +
                "detail but cost much more to convert (resolutions of ~96+ run millions of queries and are " +
                "slow), so only go high for assets that genuinely need the detail. Pick `resolution` before " +
                "writing `imagePrompt` so the image can be described to fit this budget (see # Image prompt).");
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
            sb.AppendLine("  \"appliedRuleIds\": [\"rule-id\", ...],");
            sb.AppendLine("  \"preset\": \"Creature|Prop|RawVoxelCleanup\",");
            sb.AppendLine("  \"resolution\": 32,");
            sb.AppendLine("  \"settings\": { },");
            sb.AppendLine("  \"meshy\": { }");
            sb.AppendLine("}");
            sb.AppendLine("```");

            return sb.ToString();
        }

        private static FieldInfo[] SettingsFields() =>
            typeof(VoxPipelineSettings).GetFields(BindingFlags.Public | BindingFlags.Instance);

        private static FieldInfo[] MeshyFields() =>
            typeof(VoxMeshyConfig).GetFields(BindingFlags.Public | BindingFlags.Instance);

        private static string DescribeField(FieldInfo field)
        {
            var tooltip = field.GetCustomAttribute<TooltipAttribute>()?.tooltip ?? string.Empty;
            var range = field.GetCustomAttribute<RangeAttribute>();
            return $"- {field.Name} ({DescribeType(field.FieldType, range)}): {tooltip}";
        }

        private static string DescribeType(Type type, RangeAttribute? range)
        {
            if (type == typeof(bool))
            {
                return "bool";
            }
            if (type.IsEnum)
            {
                return $"enum: {string.Join("|", Enum.GetNames(type))}";
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

        private static string DescribePreset(VoxPipelinePreset preset)
        {
            var field = typeof(VoxPipelinePreset).GetField(preset.ToString());
            return field?.GetCustomAttribute<DescriptionAttribute>()?.Description ?? preset.ToString();
        }
    }
}
