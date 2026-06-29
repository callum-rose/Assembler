using System;
using System.Linq;
using System.Reflection;
using Assembler.Anthropic;
using Assembler.MeshyImageTo3D;
using Assembler.VoxelPipeline;
using Assembler.VoxelPipeline.Generation;
using NUnit.Framework;
using UnityEngine;

namespace Tests.VoxelPipeline.Generation
{
    public sealed class VoxConfigGenerationTests
    {
        // --- Style rules ---------------------------------------------------

        [Test]
        public void StyleRules_LoadFromResource_HasSeededRules()
        {
            var rules = VoxStyleRules.Load();

            Assert.That(rules.Rules, Is.Not.Empty);
            Assert.That(rules.IsKnown("no-eyes"), Is.True);
            Assert.That(rules.IsKnown("glass-blue"), Is.True);
            // Each rule round-trips its three fields.
            foreach (var rule in rules.Rules)
            {
                Assert.That(rule.id, Is.Not.Empty);
                Assert.That(rule.text, Is.Not.Empty);
                Assert.That(rule.appliesWhen, Is.Not.Empty);
            }
        }

        [Test]
        public void StyleRules_Parse_MalformedJson_Throws()
        {
            Assert.Throws<System.IO.InvalidDataException>(() => VoxStyleRules.Parse("not json at all"));
        }

        // --- Prompt builder ------------------------------------------------

        [Test]
        public void PromptBuilder_ContainsEverySettingTooltip_PresetDescription_AndRule()
        {
            var rules = VoxStyleRules.Parse(
                "{\"rules\":[{\"id\":\"no-eyes\",\"text\":\"No eyes please.\",\"appliesWhen\":\"faces\"}]}");

            var prompt = VoxConfigPromptBuilder.Build(rules);

            // Every settings field's tooltip is present (built by reflection, so it can't drift).
            foreach (var field in typeof(VoxPipelineSettings).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var tooltip = field.GetCustomAttribute<TooltipAttribute>()?.tooltip;
                if (!string.IsNullOrEmpty(tooltip))
                {
                    StringAssert.Contains(tooltip, prompt);
                }
                StringAssert.Contains(field.Name, prompt);
            }

            // Every preset and its [Description].
            foreach (var preset in Enum.GetValues(typeof(VoxPipelinePreset)).Cast<VoxPipelinePreset>())
            {
                StringAssert.Contains(preset.ToString(), prompt);
                var description = typeof(VoxPipelinePreset).GetField(preset.ToString())!
                    .GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()!.Description;
                StringAssert.Contains(description, prompt);
            }

            // Every Meshy field's tooltip + name.
            foreach (var field in typeof(VoxMeshyConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var tooltip = field.GetCustomAttribute<TooltipAttribute>()?.tooltip;
                if (!string.IsNullOrEmpty(tooltip))
                {
                    StringAssert.Contains(tooltip, prompt);
                }
                StringAssert.Contains(field.Name, prompt);
            }

            // The rule id and text.
            StringAssert.Contains("no-eyes", prompt);
            StringAssert.Contains("No eyes please.", prompt);
        }

        // --- Parser: meshy settings ----------------------------------------

        [Test]
        public void Parse_MeshySettings_OverridesApplied_DefaultsKept_EnumsByName_RangeClamped()
        {
            var config = VoxConfigParser.Parse(
                Wrap("{\"meshy\":{\"MeshAiModel\":\"meshy-5\",\"GenerateTexture\":false," +
                     "\"MeshFormat\":\"Fbx\",\"Decimation\":\"High\",\"TargetPolycount\":999999}}"),
                Rules());

            Assert.That(config.Meshy.MeshAiModel, Is.EqualTo("meshy-5"), "string override applied");
            Assert.That(config.Meshy.GenerateTexture, Is.False, "bool override applied");
            Assert.That(config.Meshy.MeshFormat, Is.EqualTo(ModelFormat.Fbx), "enum by name");
            Assert.That(config.Meshy.Decimation, Is.EqualTo(DecimationMode.High), "enum by name");
            Assert.That(config.Meshy.TargetPolycount, Is.EqualTo(300000), "int range clamped");
            Assert.That(config.Meshy.Remesh, Is.True, "untouched field keeps default");
        }

        [Test]
        public void Parse_NoMeshyObject_KeepsAllDefaults()
        {
            var config = VoxConfigParser.Parse(Wrap("{\"preset\":\"Creature\"}"), Rules());

            var defaults = new VoxMeshyConfig();
            Assert.That(config.Meshy.MeshAiModel, Is.EqualTo(defaults.MeshAiModel));
            Assert.That(config.Meshy.MeshFormat, Is.EqualTo(defaults.MeshFormat));
            Assert.That(config.Meshy.TargetPolycount, Is.EqualTo(defaults.TargetPolycount));
        }

        // --- Extractor -----------------------------------------------------

        [Test]
        public void Extractor_PullsJsonBlock()
        {
            const string raw = "Here is the config:\n```json\n{\"resolution\": 16}\n```\nThanks.";
            Assert.That(VoxConfigExtractor.Extract(raw), Is.EqualTo("{\"resolution\": 16}"));
        }

        [Test]
        public void Extractor_ReturnsNull_WhenNoBlock()
        {
            Assert.That(VoxConfigExtractor.Extract("no fenced block here"), Is.Null);
        }

        // --- Parser: lenient overwrite -------------------------------------

        [Test]
        public void Parse_OmittedSettingsFields_KeepPresetDefaults()
        {
            var preset = VoxPipelinePresets.For(VoxPipelinePreset.Prop);
            Assume.That(preset.morphology, Is.True, "Prop preset should enable morphology");

            var config = VoxConfigParser.Parse(
                Wrap("{\"preset\":\"Prop\",\"settings\":{\"deLightThreshold\":0.2}}"),
                Rules());

            Assert.That(config.Preset, Is.EqualTo(VoxPipelinePreset.Prop));
            Assert.That(config.Settings.deLightThreshold, Is.EqualTo(0.2f).Within(1e-5f), "override applied");
            Assert.That(config.Settings.morphology, Is.True, "untouched field keeps preset default");
            Assert.That(config.Settings.snapToPalette, Is.True, "untouched field keeps preset default");
        }

        [Test]
        public void Parse_UnknownPreset_DefaultsToCreature()
        {
            var config = VoxConfigParser.Parse(Wrap("{\"preset\":\"Nonsense\"}"), Rules());
            Assert.That(config.Preset, Is.EqualTo(VoxPipelinePreset.Creature));
        }

        [Test]
        public void Parse_EnumSettingField_AcceptsName()
        {
            var config = VoxConfigParser.Parse(
                Wrap("{\"preset\":\"Creature\",\"settings\":{\"mirror\":true,\"mirrorAxis\":\"Z\"}}"),
                Rules());

            Assert.That(config.Settings.mirror, Is.True);
            Assert.That(config.Settings.mirrorAxis, Is.EqualTo(SymmetryAxis.Z));
        }

        // --- Parser: clamping ----------------------------------------------

        [Test]
        public void Parse_OutOfRangeFields_AreClamped()
        {
            var config = VoxConfigParser.Parse(
                Wrap("{\"settings\":{\"floaterMinPercent\":999,\"mirrorConfidence\":-5,\"histogramPeakCount\":999}}"),
                Rules());

            Assert.That(config.Settings.floaterMinPercent, Is.EqualTo(10f).Within(1e-5f), "float range max");
            Assert.That(config.Settings.mirrorConfidence, Is.EqualTo(0f).Within(1e-5f), "float range min");
            Assert.That(config.Settings.histogramPeakCount, Is.EqualTo(64), "int range max");
        }

        [Test]
        public void Parse_OutOfRangeResolution_IsClamped()
        {
            Assert.That(VoxConfigParser.Parse(Wrap("{\"resolution\":999}"), Rules()).Resolution,
                Is.EqualTo(VoxConfigPromptBuilder.MaxResolution));
            Assert.That(VoxConfigParser.Parse(Wrap("{\"resolution\":0}"), Rules()).Resolution,
                Is.EqualTo(VoxConfigPromptBuilder.MinResolution));
        }

        // --- Parser: rule-id filtering -------------------------------------

        [Test]
        public void Parse_UnknownRuleIds_AreDropped_KnownSurvive()
        {
            var config = VoxConfigParser.Parse(
                Wrap("{\"appliedRuleIds\":[\"no-eyes\",\"bogus\",\"glass-blue\",\"no-eyes\"]}"),
                Rules("no-eyes", "glass-blue"));

            Assert.That(config.AppliedRuleIds, Is.EquivalentTo(new[] { "no-eyes", "glass-blue" }));
        }

        // --- Parser: hard failure ------------------------------------------

        [Test]
        public void Parse_MissingJsonBlock_Throws()
        {
            Assert.Throws<AnthropicRequestException>(
                () => VoxConfigParser.Parse("Sorry, I could not produce a config.", Rules()));
        }

        // --- Helpers -------------------------------------------------------

        private static string Wrap(string json) => "Config:\n```json\n" + json + "\n```";

        private static VoxStyleRules Rules(params string[] ids)
        {
            var entries = string.Join(",", ids.Select(id =>
                $"{{\"id\":\"{id}\",\"text\":\"t\",\"appliesWhen\":\"w\"}}"));
            return VoxStyleRules.Parse("{\"rules\":[" + entries + "]}");
        }
    }
}
