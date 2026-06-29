using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels.Tests
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

        // ---- Histogram-peak snap ----

        [Test]
        public void HistogramSnap_ReducesToTopNDominantColors()
        {
            // 6x1x1: three reds (the dominant peak), two greens, one blue. Keep 2 peaks → red+green
            // (the blue is a lone speck, gated out of candidacy), and it must snap to whichever peak is
            // nearer (green, in Oklab).
            var model = new VoxModel(6, 1, 1);
            SetVoxel(model, 0, 0, 0, new Color32(200, 0, 0, 255));
            SetVoxel(model, 1, 0, 0, new Color32(200, 0, 0, 255));
            SetVoxel(model, 2, 0, 0, new Color32(200, 0, 0, 255));
            SetVoxel(model, 3, 0, 0, new Color32(0, 200, 0, 255));
            SetVoxel(model, 4, 0, 0, new Color32(0, 200, 0, 255));
            SetVoxel(model, 5, 0, 0, new Color32(0, 0, 200, 255));

            HistogramSnap.Apply(model, 2, 0.10f);

            HashSet<int> colors = DistinctColors(model);
            Assert.AreEqual(2, colors.Count, "should reduce to exactly the 2 dominant peaks");
            Assert.IsTrue(colors.Contains((200 << 16)), "red peak kept");
            Assert.IsTrue(colors.Contains((200 << 8)), "green peak kept");
            Assert.AreEqual(new Color32(200, 0, 0, 255), model.Colors[model.Index(0, 0, 0)]);
        }

        [Test]
        public void HistogramSnap_FewerDistinctThanN_LeavesColorsUntouched()
        {
            // Two distinct colours, ask for 8 peaks — nothing to reduce, both survive verbatim.
            var model = new VoxModel(2, 1, 1);
            SetVoxel(model, 0, 0, 0, new Color32(200, 0, 0, 255));
            SetVoxel(model, 1, 0, 0, new Color32(0, 200, 0, 255));

            HistogramSnap.Apply(model, 8, 0.10f);

            Assert.AreEqual(new Color32(200, 0, 0, 255), model.Colors[model.Index(0, 0, 0)]);
            Assert.AreEqual(new Color32(0, 200, 0, 255), model.Colors[model.Index(1, 0, 0)]);
        }

        [Test]
        public void HistogramSnap_PrefersDistinctColor_OverMoreFrequentNearDuplicate()
        {
            // The headline of variety-driven selection: redB is the SECOND most common colour but a
            // near-duplicate of redA; green is rarer but perceptually distinct. With a cap of 2, a plain
            // top-by-frequency would keep both reds and discard green — max-min keeps redA + green and
            // folds the near-duplicate redB onto redA.
            var model = new VoxModel(9, 1, 1);
            for (int x = 0; x < 4; x++)
            {
                SetVoxel(model, x, 0, 0, new Color32(200, 0, 0, 255)); // redA ×4 (most common)
            }
            for (int x = 4; x < 7; x++)
            {
                SetVoxel(model, x, 0, 0, new Color32(190, 5, 5, 255)); // redB ×3 (near-duplicate)
            }
            SetVoxel(model, 7, 0, 0, new Color32(0, 200, 0, 255)); // green ×2 (distinct)
            SetVoxel(model, 8, 0, 0, new Color32(0, 200, 0, 255));

            HistogramSnap.Apply(model, 2, 0.10f);

            HashSet<int> colors = DistinctColors(model);
            Assert.AreEqual(2, colors.Count);
            Assert.IsTrue(colors.Contains(200 << 16), "dominant red kept as the seed");
            Assert.IsTrue(colors.Contains(200 << 8), "distinct green kept over the near-duplicate red");
            Assert.IsFalse(colors.Contains((190 << 16) | (5 << 8) | 5), "near-duplicate red folded onto redA");
        }

        [Test]
        public void HistogramSnap_VarietyThreshold_StopsBelowCap()
        {
            // The variety threshold — not the count — drives the result: the cap is 8 but only two
            // colours are far enough apart to keep, so the near-duplicate red merges away and we stop
            // at 2 well before the cap.
            var model = new VoxModel(9, 1, 1);
            for (int x = 0; x < 4; x++)
            {
                SetVoxel(model, x, 0, 0, new Color32(200, 0, 0, 255)); // redA
            }
            for (int x = 4; x < 7; x++)
            {
                SetVoxel(model, x, 0, 0, new Color32(190, 5, 5, 255)); // redB (within threshold of redA)
            }
            SetVoxel(model, 7, 0, 0, new Color32(0, 200, 0, 255)); // green (well beyond threshold)
            SetVoxel(model, 8, 0, 0, new Color32(0, 200, 0, 255));

            HistogramSnap.Apply(model, 8, 0.15f);

            Assert.AreEqual(2, DistinctColors(model).Count, "stops at 2 despite a cap of 8 — threshold gated redB");
        }

        [Test]
        public void HistogramSnap_LoneSpeck_NotChosenAsPeak()
        {
            // A single maximally-distinct voxel (a magenta speck) is the farthest-point winner, but the
            // population gate keeps it out of candidacy so it can't become a peak — it snaps to a red
            // peak instead. Without the gate, variety selection would chase noise.
            var model = new VoxModel(8, 1, 1);
            for (int x = 0; x < 4; x++)
            {
                SetVoxel(model, x, 0, 0, new Color32(200, 0, 0, 255)); // redA ×4
            }
            for (int x = 4; x < 7; x++)
            {
                SetVoxel(model, x, 0, 0, new Color32(190, 5, 5, 255)); // redB ×3
            }
            SetVoxel(model, 7, 0, 0, new Color32(255, 0, 255, 255)); // magenta speck ×1

            HistogramSnap.Apply(model, 8, 0.10f);

            HashSet<int> colors = DistinctColors(model);
            Assert.IsFalse(colors.Contains((255 << 16) | 255), "lone magenta speck must not become a peak");
            Assert.AreNotEqual(new Color32(255, 0, 255, 255), model.Colors[model.Index(7, 0, 0)],
                "the speck snaps to a surviving red peak");
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
