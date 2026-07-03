using System.Linq;
using System.Reflection;
using Assembler.Anthropic;
using Assembler.AssetGeneration.ImageToMesh;
using NUnit.Framework;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike.Generation.Tests
{
    public sealed class ConfigGenerationTests
    {
        // --- Style rules ---------------------------------------------------

        [Test]
        public void StyleRules_LoadFromResource_HasSeededRules()
        {
            var rules = StyleRules.Load();

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
            Assert.Throws<System.IO.InvalidDataException>(() => StyleRules.Parse("not json at all"));
        }

        // --- Prompt builder ------------------------------------------------

        [Test]
        public void PromptBuilder_ContainsEverySettingTooltip_AndRule()
        {
            var rules = StyleRules.Parse(
                "{\"rules\":[{\"id\":\"no-eyes\",\"text\":\"No eyes please.\",\"appliesWhen\":\"faces\"}]}");

            var prompt = ConfigPromptBuilder.Build(rules);

            // Every settings field's tooltip + name is present (built by reflection, so it can't drift).
            foreach (var field in typeof(SettingsConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var tooltip = field.GetCustomAttribute<TooltipAttribute>()?.tooltip;
                if (!string.IsNullOrEmpty(tooltip))
                {
                    StringAssert.Contains(tooltip, prompt);
                }
                StringAssert.Contains(field.Name, prompt);
            }

            // Every Meshy field's tooltip + name.
            foreach (var field in typeof(MeshyConfig).GetFields(BindingFlags.Public | BindingFlags.Instance))
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
            var config = ConfigParser.Parse(
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
            var config = ConfigParser.Parse(Wrap("{\"settings\":{\"MaxDimVoxels\":16}}"), Rules());

            var defaults = new MeshyConfig();
            Assert.That(config.Meshy.MeshAiModel, Is.EqualTo(defaults.MeshAiModel));
            Assert.That(config.Meshy.MeshFormat, Is.EqualTo(defaults.MeshFormat));
            Assert.That(config.Meshy.TargetPolycount, Is.EqualTo(defaults.TargetPolycount));
        }

        // --- Extractor -----------------------------------------------------

        [Test]
        public void Extractor_PullsJsonBlock()
        {
            const string raw = "Here is the config:\n```json\n{\"settings\": {}}\n```\nThanks.";
            Assert.That(ConfigExtractor.Extract(raw), Is.EqualTo("{\"settings\": {}}"));
        }

        [Test]
        public void Extractor_ReturnsNull_WhenNoBlock()
        {
            Assert.That(ConfigExtractor.Extract("no fenced block here"), Is.Null);
        }

        // --- Parser: lenient overwrite -------------------------------------

        [Test]
        public void Parse_OmittedSettingsFields_KeepDefaults()
        {
            var config = ConfigParser.Parse(
                Wrap("{\"settings\":{\"Coverage\":0.8}}"),
                Rules());

            Assert.That(config.Settings.Coverage, Is.EqualTo(0.8f).Within(1e-5f), "override applied");
            Assert.That(config.Settings.GridSearch, Is.EqualTo(Settings.Defaults.GridSearch), "untouched field keeps default");
            Assert.That(config.Settings.MultiSampleColour, Is.EqualTo(Settings.Defaults.MultiSampleColour), "untouched field keeps default");
            // ToSettings always resolves the direct max-dimension slider path.
            Assert.That(config.Settings.ResolutionInput, Is.EqualTo(ResolutionInput.MaxDimSlider));
        }

        [Test]
        public void Parse_EnumSettingField_AcceptsName()
        {
            var config = ConfigParser.Parse(
                Wrap("{\"settings\":{\"ColourMode\":\"Raw\",\"Symmetry\":\"X\"}}"),
                Rules());

            Assert.That(config.Settings.ColourMode, Is.EqualTo(ColourMode.Raw));
            Assert.That(config.Settings.Symmetry, Is.EqualTo(SymmetryAxes.X));
        }

        [Test]
        public void Parse_FlagsSymmetry_AcceptsCommaSeparatedNames()
        {
            var config = ConfigParser.Parse(
                Wrap("{\"settings\":{\"Symmetry\":\"X,Y\"}}"),
                Rules());

            Assert.That(config.Settings.Symmetry, Is.EqualTo(SymmetryAxes.X | SymmetryAxes.Y));
        }

        // --- Parser: clamping ----------------------------------------------

        [Test]
        public void Parse_OutOfRangeFields_AreClamped()
        {
            var config = ConfigParser.Parse(
                Wrap("{\"settings\":{\"Coverage\":999,\"PottsStrength\":-5,\"PaletteSize\":999,\"MaxDimVoxels\":999}}"),
                Rules());

            Assert.That(config.Settings.Coverage, Is.EqualTo(1f).Within(1e-5f), "float range max");
            Assert.That(config.Settings.PottsStrength, Is.EqualTo(0f).Within(1e-5f), "float range min");
            Assert.That(config.Settings.PaletteSize, Is.EqualTo(32), "int range max");
            Assert.That(config.Settings.MaxDimVoxels, Is.EqualTo(96), "resolution range max");
        }

        // --- Parser: rule-id filtering -------------------------------------

        [Test]
        public void Parse_UnknownRuleIds_AreDropped_KnownSurvive()
        {
            var config = ConfigParser.Parse(
                Wrap("{\"appliedRuleIds\":[\"no-eyes\",\"bogus\",\"glass-blue\",\"no-eyes\"]}"),
                Rules("no-eyes", "glass-blue"));

            Assert.That(config.AppliedRuleIds, Is.EquivalentTo(new[] { "no-eyes", "glass-blue" }));
        }

        // --- Parser: hard failure ------------------------------------------

        [Test]
        public void Parse_MissingJsonBlock_Throws()
        {
            Assert.Throws<AnthropicRequestException>(
                () => ConfigParser.Parse("Sorry, I could not produce a config.", Rules()));
        }

        // --- Helpers -------------------------------------------------------

        private static string Wrap(string json) => "Config:\n```json\n" + json + "\n```";

        private static StyleRules Rules(params string[] ids)
        {
            var entries = string.Join(",", ids.Select(id =>
                $"{{\"id\":\"{id}\",\"text\":\"t\",\"appliesWhen\":\"w\"}}"));
            return StyleRules.Parse("{\"rules\":[" + entries + "]}");
        }
    }
}
