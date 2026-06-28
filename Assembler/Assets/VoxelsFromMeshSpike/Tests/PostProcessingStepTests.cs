using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VoxelsFromMeshSpike;

namespace Tests.VoxelsFromMeshSpike
{
    public sealed class PostProcessingStepTests
    {
        // ---- Oklab ----

        [Test]
        public void Oklab_White_IsLightAndAchromatic()
        {
            OklabColor white = OklabColor.FromColor32(new Color32(255, 255, 255, 255));
            Assert.That(white.L, Is.EqualTo(1f).Within(0.01f));
            Assert.That(white.A, Is.EqualTo(0f).Within(0.01f));
            Assert.That(white.B, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void Oklab_DarkRedNearerToRedThanGreen()
        {
            OklabColor darkRed = OklabColor.FromColor32(new Color32(150, 30, 30, 255));
            OklabColor red = OklabColor.FromColor32(new Color32(255, 0, 0, 255));
            OklabColor green = OklabColor.FromColor32(new Color32(0, 255, 0, 255));
            Assert.Less(darkRed.SquaredDistanceTo(red), darkRed.SquaredDistanceTo(green));
        }

        // ---- Floater removal ----

        [Test]
        public void Floaters_RemovesSpeck_KeepsBlob()
        {
            // 5x1x1: a 3-voxel run (x0..x2), gap at x3, a 1-voxel speck at x4.
            var model = Model(5, 1, 1,
                (0, 0, 0), (1, 0, 0), (2, 0, 0), (4, 0, 0));

            FloaterRemoval.Apply(model, new FloaterRemoval.Options(2, 0f));

            Assert.IsTrue(model.IsOccupied(0, 0, 0));
            Assert.IsTrue(model.IsOccupied(2, 0, 0));
            Assert.IsFalse(model.IsOccupied(4, 0, 0), "1-voxel speck should be removed");
        }

        [Test]
        public void Floaters_KeepsBothSubstantialDetachedParts()
        {
            // 9x1x1: two separate 3-voxel runs — not largest-component-only.
            var model = Model(9, 1, 1,
                (0, 0, 0), (1, 0, 0), (2, 0, 0),
                (6, 0, 0), (7, 0, 0), (8, 0, 0));

            FloaterRemoval.Apply(model, new FloaterRemoval.Options(2, 0f));

            Assert.IsTrue(model.IsOccupied(0, 0, 0));
            Assert.IsTrue(model.IsOccupied(8, 0, 0));
            Assert.AreEqual(6, CountOccupied(model));
        }

        // ---- De-light ----

        [Test]
        public void DeLight_CollapsesTwoShadedRegionsToTwoColors()
        {
            // 4x1x1: reddish gradient (x0,x1) then greenish gradient (x2,x3).
            var model = new VoxModel(4, 1, 1);
            SetVoxel(model, 0, 0, 0, new Color32(200, 0, 0, 255));
            SetVoxel(model, 1, 0, 0, new Color32(160, 0, 0, 255));
            SetVoxel(model, 2, 0, 0, new Color32(0, 200, 0, 255));
            SetVoxel(model, 3, 0, 0, new Color32(0, 160, 0, 255));

            DeLight.Apply(model, new DeLight.Options(0.15f));

            // Each material flattens to one colour → 2 distinct overall.
            Assert.AreEqual(2, DistinctColors(model).Count);
            Assert.AreEqual(model.Colors[model.Index(0, 0, 0)], model.Colors[model.Index(1, 0, 0)]);
            Assert.AreEqual(model.Colors[model.Index(2, 0, 0)], model.Colors[model.Index(3, 0, 0)]);
        }

        // ---- Palette-snap ----

        [Test]
        public void PaletteSnap_SnapsToNearestSwatchInOklab()
        {
            var palette = new List<Color32>
            {
                new Color32(255, 0, 0, 255), // red
                new Color32(0, 255, 0, 255), // green
                new Color32(0, 0, 255, 255), // blue
            };
            var model = new VoxModel(2, 1, 1);
            SetVoxel(model, 0, 0, 0, new Color32(200, 40, 40, 255)); // unambiguously red
            SetVoxel(model, 1, 0, 0, new Color32(40, 180, 60, 255)); // unambiguously green

            PaletteSnap.Apply(model, palette);

            Assert.AreEqual(new Color32(255, 0, 0, 255), model.Colors[model.Index(0, 0, 0)]);
            Assert.AreEqual(new Color32(0, 255, 0, 255), model.Colors[model.Index(1, 0, 0)]);
        }

        [Test]
        public void PaletteSnap_NearNeutralPrefersNeutralOverSaturatedSameHue()
        {
            // A faintly-warm light grey (a starship hull panel) must not jump to a saturated
            // pink of the same hue — the "hull panels turn pink" bug. The chroma-gain penalty
            // keeps it on the neutral grey even though the pink is the raw Oklab nearest.
            var palette = new List<Color32>
            {
                new Color32(189, 189, 189, 255), // neutral grey
                new Color32(255, 204, 188, 255), // saturated pink/skin (raw nearest)
            };
            var model = new VoxModel(1, 1, 1);
            SetVoxel(model, 0, 0, 0, new Color32(225, 205, 200, 255)); // faintly-warm light grey

            PaletteSnap.Apply(model, palette);

            Assert.AreEqual(new Color32(189, 189, 189, 255), model.Colors[model.Index(0, 0, 0)]);
        }

        [Test]
        public void PaletteSnap_EmptyPalette_LeavesColorsUntouched()
        {
            var model = new VoxModel(1, 1, 1);
            SetVoxel(model, 0, 0, 0, new Color32(12, 34, 56, 255));
            PaletteSnap.Apply(model, new List<Color32>());
            Assert.AreEqual(new Color32(12, 34, 56, 255), model.Colors[model.Index(0, 0, 0)]);
        }

        // ---- Morphology ----

        [Test]
        public void Morphology_FillsEnclosedPinhole()
        {
            // 3x3x3 fully filled except the centre — centre has 6 occupied face-neighbours.
            var model = new VoxModel(3, 3, 3);
            for (int z = 0; z < 3; z++)
            {
                for (int y = 0; y < 3; y++)
                {
                    for (int x = 0; x < 3; x++)
                    {
                        if (!(x == 1 && y == 1 && z == 1))
                        {
                            SetVoxel(model, x, y, z, new Color32(50, 100, 150, 255));
                        }
                    }
                }
            }

            Morphology.Apply(model, new Morphology.Options(-1, 6)); // fill only, no removal

            Assert.IsTrue(model.IsOccupied(1, 1, 1));
            Assert.AreEqual(new Color32(50, 100, 150, 255), model.Colors[model.Index(1, 1, 1)]);
        }

        [Test]
        public void Morphology_RemovesSingleFaceBump()
        {
            // 3x3 plate at z0 + one voxel poking up at (1,1,1) clinging by a single face.
            var model = new VoxModel(3, 3, 2);
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    SetVoxel(model, x, y, 0, new Color32(80, 80, 80, 255));
                }
            }
            SetVoxel(model, 1, 1, 1, new Color32(80, 80, 80, 255));

            Morphology.Apply(model, new Morphology.Options(1, 7)); // remove only, no fill

            Assert.IsFalse(model.IsOccupied(1, 1, 1), "single-face bump should be removed");
            Assert.IsTrue(model.IsOccupied(1, 1, 0), "plate centre should remain");
        }

        // ---- helpers ----

        private static VoxModel Model(int x, int y, int z, params (int x, int y, int z)[] filled)
        {
            var model = new VoxModel(x, y, z);
            foreach ((int fx, int fy, int fz) in filled)
            {
                SetVoxel(model, fx, fy, fz, new Color32(255, 255, 255, 255));
            }
            return model;
        }

        private static void SetVoxel(VoxModel model, int x, int y, int z, Color32 color)
        {
            int i = model.Index(x, y, z);
            model.Occupied[i] = true;
            model.Colors[i] = color;
        }

        private static int CountOccupied(VoxModel model) => model.Occupied.Count(o => o);

        private static HashSet<int> DistinctColors(VoxModel model)
        {
            var set = new HashSet<int>();
            for (int i = 0; i < model.Occupied.Length; i++)
            {
                if (model.Occupied[i])
                {
                    Color32 c = model.Colors[i];
                    set.Add((c.r << 16) | (c.g << 8) | c.b);
                }
            }
            return set;
        }
    }
}
