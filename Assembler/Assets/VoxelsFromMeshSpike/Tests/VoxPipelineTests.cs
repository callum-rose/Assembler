using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VoxelsFromMeshSpike;

namespace Tests.VoxelsFromMeshSpike
{
    public sealed class VoxPipelineTests
    {
        // ---- runner ----

        [Test]
        public void Run_AppliesEnabledStepsInOrder_SkippingDisabled()
        {
            var log = new List<string>();
            var pipeline = new VoxPipeline(new IVoxStep[]
            {
                new RecordingStep("a", enabled: true, log),
                new RecordingStep("b", enabled: false, log),
                new RecordingStep("c", enabled: true, log),
            });

            pipeline.Run(new VoxModel(1, 1, 1));

            CollectionAssert.AreEqual(new[] { "a", "c" }, log);
        }

        [Test]
        public void Run_ReportsProgressPerEnabledStep()
        {
            var reported = new List<(string name, float fraction)>();
            var pipeline = new VoxPipeline(new IVoxStep[]
            {
                new RecordingStep("a", enabled: true, new List<string>()),
                new RecordingStep("b", enabled: false, new List<string>()),
                new RecordingStep("c", enabled: true, new List<string>()),
            });

            pipeline.Run(new VoxModel(1, 1, 1), (name, fraction) => reported.Add((name, fraction)));

            Assert.AreEqual(2, reported.Count);
            Assert.AreEqual("a", reported[0].name);
            Assert.AreEqual(0f, reported[0].fraction, 0.0001f);
            Assert.AreEqual("c", reported[1].name);
            Assert.AreEqual(0.5f, reported[1].fraction, 0.0001f);
        }

        // ---- canonical order ----

        [Test]
        public void FromSettings_BuildsAllStepsInCanonicalOrder()
        {
            var settings = new VoxPipelineSettings();
            VoxPipeline pipeline = VoxPipeline.FromSettings(settings, new List<Color32>());

            string[] names = pipeline.Steps.Select(s => s.Name).ToArray();
            CollectionAssert.AreEqual(
                new[]
                {
                    "Remove floaters",
                    "Mirror (symmetry)",
                    "Revolve (symmetry)",
                    "De-light",
                    "Snap to histogram peaks",
                    "Snap to master palette",
                    "Despeckle / fill",
                },
                names);
        }

        [Test]
        public void FromSettings_PlacesSymmetryBetweenFloatersAndDeLight()
        {
            VoxPipeline pipeline = VoxPipeline.FromSettings(new VoxPipelineSettings(), new List<Color32>());
            string[] names = pipeline.Steps.Select(s => s.Name).ToArray();

            int floaters = System.Array.IndexOf(names, "Remove floaters");
            int mirror = System.Array.IndexOf(names, "Mirror (symmetry)");
            int revolve = System.Array.IndexOf(names, "Revolve (symmetry)");
            int deLight = System.Array.IndexOf(names, "De-light");

            Assert.Less(floaters, mirror, "mirror must run after floaters");
            Assert.Less(mirror, revolve, "mirror must run before revolve");
            Assert.Less(revolve, deLight, "symmetry must run before de-light (colour still raw)");
        }

        [Test]
        public void FromSettings_SymmetryStepsOffByDefault()
        {
            VoxPipeline pipeline = VoxPipeline.FromSettings(new VoxPipelineSettings(), new List<Color32>());
            Dictionary<string, bool> enabled = pipeline.Steps.ToDictionary(s => s.Name, s => s.Enabled);
            Assert.IsFalse(enabled["Mirror (symmetry)"], "mirror is opt-in");
            Assert.IsFalse(enabled["Revolve (symmetry)"], "revolve is opt-in");
        }

        [Test]
        public void Preset_Creature_KeepsSymmetryOff()
        {
            VoxPipelineSettings s = VoxPipelinePresets.For(VoxPipelinePreset.Creature);
            Assert.IsFalse(s.mirror);
            Assert.IsFalse(s.revolve);
        }

        [Test]
        public void FromSettings_EnabledFlagsMirrorSettings()
        {
            var settings = new VoxPipelineSettings
            {
                removeFloaters = true,
                deLight = false,
                snapToPalette = true,
                morphology = false,
            };
            VoxPipeline pipeline = VoxPipeline.FromSettings(settings, new List<Color32>());

            Dictionary<string, bool> enabled = pipeline.Steps.ToDictionary(s => s.Name, s => s.Enabled);
            Assert.IsTrue(enabled["Remove floaters"]);
            Assert.IsFalse(enabled["De-light"]);
            Assert.IsTrue(enabled["Snap to master palette"]);
            Assert.IsFalse(enabled["Despeckle / fill"]);
        }

        // ---- presets ----

        [Test]
        public void Preset_Creature_KeepsThinFeatures_MorphologyOff()
        {
            VoxPipelineSettings s = VoxPipelinePresets.For(VoxPipelinePreset.Creature);
            Assert.IsTrue(s.deLight);
            Assert.IsTrue(s.snapToPalette);
            Assert.IsFalse(s.morphology);
        }

        [Test]
        public void Preset_Prop_EnablesMorphology()
        {
            VoxPipelineSettings s = VoxPipelinePresets.For(VoxPipelinePreset.Prop);
            Assert.IsTrue(s.morphology);
            Assert.IsTrue(s.deLight);
        }

        [Test]
        public void Preset_RawVoxelCleanup_LeavesColourAlone()
        {
            VoxPipelineSettings s = VoxPipelinePresets.For(VoxPipelinePreset.RawVoxelCleanup);
            Assert.IsFalse(s.deLight);
            Assert.IsFalse(s.snapToPalette);
            Assert.IsTrue(s.removeFloaters);
            Assert.IsTrue(s.morphology);
        }

        [Test]
        public void Preset_ReturnsFreshInstance_NotShared()
        {
            VoxPipelineSettings a = VoxPipelinePresets.For(VoxPipelinePreset.Creature);
            VoxPipelineSettings b = VoxPipelinePresets.For(VoxPipelinePreset.Creature);
            a.deLight = false;
            Assert.IsTrue(b.deLight, "mutating one preset result must not affect another");
        }

        // ---- end-to-end ----

        [Test]
        public void EndToEnd_RemovesSpeck_FlattensRegion_SnapsToPalette()
        {
            // 5x1x1: a 3-voxel reddish gradient (x0..x2) + a 1-voxel speck at x4.
            var model = new VoxModel(5, 1, 1);
            SetVoxel(model, 0, 0, 0, new Color32(200, 0, 0, 255));
            SetVoxel(model, 1, 0, 0, new Color32(180, 0, 0, 255));
            SetVoxel(model, 2, 0, 0, new Color32(160, 0, 0, 255));
            SetVoxel(model, 4, 0, 0, new Color32(170, 0, 0, 255)); // speck

            var palette = new List<Color32>
            {
                new Color32(255, 0, 0, 255),
                new Color32(0, 255, 0, 255),
                new Color32(0, 0, 255, 255),
            };
            var settings = new VoxPipelineSettings
            {
                removeFloaters = true,
                floaterMinPercent = 0.5f, // floor is MinVoxels=2, so the 1-voxel speck goes
                deLight = true,
                deLightThreshold = 0.15f,
                snapToPalette = true,
                morphology = false,
            };

            VoxPipeline.FromSettings(settings, palette).Run(model);

            Assert.IsFalse(model.IsOccupied(4, 0, 0), "1-voxel speck should be removed");
            Assert.AreEqual(3, CountOccupied(model));
            for (int x = 0; x <= 2; x++)
            {
                Assert.AreEqual(new Color32(255, 0, 0, 255), model.Colors[model.Index(x, 0, 0)],
                    $"voxel x{x} should flatten + snap to the red swatch");
            }
        }

        // ---- helpers ----

        private static void SetVoxel(VoxModel model, int x, int y, int z, Color32 color)
        {
            int i = model.Index(x, y, z);
            model.Occupied[i] = true;
            model.Colors[i] = color;
        }

        private static int CountOccupied(VoxModel model) => model.Occupied.Count(o => o);

        private sealed class RecordingStep : IVoxStep
        {
            private readonly List<string> _log;

            public RecordingStep(string name, bool enabled, List<string> log)
            {
                Name = name;
                Enabled = enabled;
                _log = log;
            }

            public string Name { get; }
            public bool Enabled { get; }
            public void Apply(VoxModel model) => _log.Add(Name);
        }
    }
}
