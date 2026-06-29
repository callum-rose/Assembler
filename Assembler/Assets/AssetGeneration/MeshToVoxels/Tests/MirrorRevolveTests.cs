using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxels.Tests
{
    public sealed class MirrorRevolveTests
    {
        // ---- Mirror: offset-solve + confidence gate ----

        [Test]
        public void Mirror_OffsetSolve_FindsOffCentrePlane()
        {
            // 6x1x1: occupied {1,2,4,5} — perfectly symmetric about the plane at sum 6
            // (1↔5, 2↔4), which is off the grid centre (sum 5). The solver must find sum 6.
            var model = Model(6, 1, 1, white: true, (1, 0, 0), (2, 0, 0), (4, 0, 0), (5, 0, 0));

            Mirror.Result result = Mirror.Apply(model, Mirror.Options.Default);

            Assert.AreEqual(6, result.PlaneSum, "offset-solve should centre on the model, not the grid");
            Assert.That(result.Score, Is.EqualTo(1f).Within(0.0001f));
            Assert.IsTrue(result.Applied);
            // Already symmetric → forcing changes nothing.
            CollectionAssert.AreEquivalent(
                new[] { 1, 2, 4, 5 }, OccupiedAlongX(model));
        }

        [Test]
        public void Mirror_AsymmetricShape_FailsGate_AndSkips()
        {
            // 4x1x1: occupied {0,1,3} — no plane mirrors it well (best overlap 2/3 < 0.85).
            var model = Model(4, 1, 1, white: true, (0, 0, 0), (1, 0, 0), (3, 0, 0));

            Mirror.Result result = Mirror.Apply(model, Mirror.Options.Default);

            Assert.IsFalse(result.Applied, "below-confidence model must be left untouched");
            Assert.Less(result.Score, 0.85f);
            CollectionAssert.AreEquivalent(new[] { 0, 1, 3 }, OccupiedAlongX(model),
                "occupancy must be unchanged when the gate fails");
        }

        [Test]
        public void Mirror_Force_OverwritesHalf_WithMirrorOfOther()
        {
            // 6x1x1: low half {1,2} red, lone high voxel {5} blue (missing 4). Best plane is
            // sum 6 (centre tie-break). Forcing keeps the richer low half and overwrites the high.
            var model = new VoxModel(6, 1, 1);
            SetVoxel(model, 1, 0, 0, new Color32(255, 0, 0, 255));
            SetVoxel(model, 2, 0, 0, new Color32(255, 0, 0, 255));
            SetVoxel(model, 5, 0, 0, new Color32(0, 0, 255, 255));

            // Occupancy-only score (colourWeight 0) so the plane choice is unambiguous; force past the gate.
            var options = new Mirror.Options(SymmetryAxis.X, 0.85f, force: true, colourWeight: 0f, colourTolerance: 0.15f);
            Mirror.Result result = Mirror.Apply(model, options);

            Assert.IsTrue(result.Applied);
            Assert.AreEqual(6, result.PlaneSum);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 4, 5 }, OccupiedAlongX(model),
                "high half should become the mirror of the low half");
            Assert.AreEqual(new Color32(255, 0, 0, 255), model.Colors[model.Index(4, 0, 0)],
                "x4 should take x2's colour");
            Assert.AreEqual(new Color32(255, 0, 0, 255), model.Colors[model.Index(5, 0, 0)],
                "x5's blue should be overwritten by the mirror of x1 (red)");
        }

        [Test]
        public void Mirror_EmptyModel_DoesNothing()
        {
            var model = new VoxModel(4, 1, 1);
            Mirror.Result result = Mirror.Apply(model, Mirror.Options.Default);
            Assert.IsFalse(result.Applied);
            Assert.AreEqual(0, model.Occupied.Count(o => o));
        }

        // ---- Revolve: radial profile → rotationally symmetric ----

        [Test]
        public void Revolve_MakesRingsUniform_FillingAndClearingByThreshold()
        {
            // 5x1x5 disc about the Y axis, centred on (2,2). Two partial rings:
            //   r=1 (8 cells): 4 face cells occupied → 0.5 ⇒ ring fills (diagonals join).
            //   r=2 (12 cells): 4 axis cells occupied → 0.33 ⇒ ring clears.
            // The occupied set is 4-fold symmetric, so the centroid lands exactly on (2,2).
            var colour = new Color32(40, 160, 220, 255);
            var model = new VoxModel(5, 1, 5);
            SetVoxel(model, 2, 0, 2, colour);                                  // centre (r0)
            foreach ((int x, int z) in new[] { (1, 2), (3, 2), (2, 1), (2, 3) }) // r1 faces
            {
                SetVoxel(model, x, 0, z, colour);
            }
            foreach ((int x, int z) in new[] { (0, 2), (4, 2), (2, 0), (2, 4) }) // r2 axis
            {
                SetVoxel(model, x, 0, z, colour);
            }

            Revolve.Apply(model, new Revolve.Options(SymmetryAxis.Y, 0.5f));

            Assert.IsTrue(model.IsOccupied(2, 0, 2), "centre stays");
            Assert.IsTrue(model.IsOccupied(1, 0, 1), "r1 diagonal should fill (ring made uniform)");
            Assert.IsTrue(model.IsOccupied(3, 0, 3), "r1 diagonal should fill (ring made uniform)");
            Assert.IsFalse(model.IsOccupied(0, 0, 2), "sparse r2 ring should clear");
            Assert.IsFalse(model.IsOccupied(2, 0, 0), "sparse r2 ring should clear");
            Assert.AreEqual(colour, model.Colors[model.Index(1, 0, 1)], "filled ring takes the ring colour");

            AssertRotationallySymmetric(model, SymmetryAxis.Y);
        }

        [Test]
        public void Revolve_RoundsALumpyDisc()
        {
            // A filled disc (r<=2 about (2,2)) with an opposite pair of edge voxels chipped out.
            // Chipping a symmetric pair keeps the centroid exactly on (2,2); revolve restores both
            // (their ring stays a majority) and leaves a clean solid of revolution.
            var colour = new Color32(200, 80, 60, 255);
            var model = new VoxModel(5, 1, 5);
            for (int z = 0; z < 5; z++)
            {
                for (int x = 0; x < 5; x++)
                {
                    if (Mathf.RoundToInt(Mathf.Sqrt((x - 2f) * (x - 2f) + (z - 2f) * (z - 2f))) <= 2)
                    {
                        SetVoxel(model, x, 0, z, colour);
                    }
                }
            }
            model.Occupied[model.Index(0, 0, 2)] = false; // chip opposite edges
            model.Occupied[model.Index(4, 0, 2)] = false;

            Revolve.Apply(model, new Revolve.Options(SymmetryAxis.Y, 0.5f));

            Assert.IsTrue(model.IsOccupied(0, 0, 2), "chipped edge should be revolved back in");
            Assert.IsTrue(model.IsOccupied(4, 0, 2), "chipped edge should be revolved back in");
            AssertRotationallySymmetric(model, SymmetryAxis.Y);
        }

        // ---- §8 anti-mirror guard ----

        [Test]
        public void VoxWriter_RoundTrip_DoesNotMirrorAnAsymmetricL()
        {
            // An "L" lying in the XZ plane (y=0): a vertical bar at x=0 (z=0..2) plus a foot
            // along z=0 (x=0..2). It is asymmetric in both X and Z, so any accidental axis flip
            // in the write remap would show up as a mirrored shape.
            var model = new VoxModel(3, 1, 3);
            foreach ((int x, int z) in new[] { (0, 0), (0, 1), (0, 2), (1, 0), (2, 0) })
            {
                SetVoxel(model, x, 0, z, new Color32(255, 255, 255, 255));
            }
            VoxResult result = model.ToResult();

            // Expected VOX-space set per VoxWriter's handedness-preserving remap:
            // vx = gridX-1-x, vy = z, vz = y. That linear map has determinant +1 (a proper
            // rotation — 180° about the vertical relative to a plain axis swap), so an
            // asymmetric shape is rotated, never mirrored.
            var expected = new HashSet<(int x, int y, int z)>(
                result.Cells.Select(c => (x: result.GridX - 1 - c.X, y: c.Z, z: c.Y)));

            string path = Path.Combine(Application.temporaryCachePath, "anti_mirror_guard.vox");
            VoxWriter.Write(path, result);
            HashSet<(int x, int y, int z)> actual = ReadVoxPositions(path);
            File.Delete(path);

            CollectionAssert.AreEquivalent(expected, actual,
                "written voxels must match the non-mirrored remap exactly");

            // And the shape must not equal its own mirror about the vx axis — i.e. asymmetry survived.
            int maxX = expected.Max(v => v.x);
            var mirrored = new HashSet<(int x, int y, int z)>(
                expected.Select(v => (x: maxX - v.x, v.y, v.z)));
            CollectionAssert.AreNotEquivalent(mirrored, actual, "round-trip must not be left/right mirrored");
        }

        [Test]
        public void Pipeline_WithSymmetryOff_LeavesAsymmetricShapeUntouched()
        {
            // Mirror/revolve default off → an asymmetric shape's geometry must be preserved through
            // the pipeline (the colour steps may recolour, but they must not move/mirror voxels).
            var model = new VoxModel(4, 1, 1);
            SetVoxel(model, 0, 0, 0, new Color32(255, 255, 255, 255));
            SetVoxel(model, 1, 0, 0, new Color32(255, 255, 255, 255));
            SetVoxel(model, 3, 0, 0, new Color32(255, 255, 255, 255));

            var settings = new VoxPipelineSettings { deLight = false, snapToPalette = false, morphology = false, removeFloaters = false };
            VoxPipeline.FromSettings(settings, new List<Color32>()).Run(model);

            CollectionAssert.AreEquivalent(new[] { 0, 1, 3 }, OccupiedAlongX(model));
        }

        // ---- helpers ----

        private static VoxModel Model(int x, int y, int z, bool white, params (int x, int y, int z)[] filled)
        {
            var model = new VoxModel(x, y, z);
            var colour = white ? new Color32(255, 255, 255, 255) : default;
            foreach ((int fx, int fy, int fz) in filled)
            {
                SetVoxel(model, fx, fy, fz, colour);
            }
            return model;
        }

        private static void SetVoxel(VoxModel model, int x, int y, int z, Color32 color)
        {
            int i = model.Index(x, y, z);
            model.Occupied[i] = true;
            model.Colors[i] = color;
        }

        private static int[] OccupiedAlongX(VoxModel model)
        {
            var xs = new List<int>();
            for (int i = 0; i < model.Occupied.Length; i++)
            {
                if (model.Occupied[i])
                {
                    xs.Add(model.Coords(i).x);
                }
            }
            return xs.ToArray();
        }

        /// <summary>Asserts occupancy is a function of (axial slice, radius) only — i.e. truly revolved.</summary>
        private static void AssertRotationallySymmetric(VoxModel model, SymmetryAxis axis)
        {
            // axis Y here: centre of the XZ plane is the occupied centroid; group by integer radius
            // within each axial slice and assert every cell in a group agrees.
            (float cx, float cz, int count) = (0f, 0f, 0);
            for (int i = 0; i < model.Occupied.Length; i++)
            {
                if (!model.Occupied[i])
                {
                    continue;
                }
                (int x, int _, int z) = model.Coords(i);
                cx += x;
                cz += z;
                count++;
            }
            if (count == 0)
            {
                return;
            }
            cx /= count;
            cz /= count;

            var byRing = new Dictionary<(int y, int r), bool>();
            for (int i = 0; i < model.Occupied.Length; i++)
            {
                (int x, int y, int z) = model.Coords(i);
                int r = Mathf.RoundToInt(Mathf.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz)));
                var key = (y, r);
                if (byRing.TryGetValue(key, out bool occ))
                {
                    Assert.AreEqual(occ, model.Occupied[i],
                        $"ring (y{y}, r{r}) must be uniformly filled or empty after revolve");
                }
                else
                {
                    byRing[key] = model.Occupied[i];
                }
            }
        }

        /// <summary>Minimal reader: pulls the voxel (x,y,z) set out of a single-model .vox file.</summary>
        private static HashSet<(int x, int y, int z)> ReadVoxPositions(string path)
        {
            var positions = new HashSet<(int x, int y, int z)>();
            using var reader = new BinaryReader(File.OpenRead(path));
            reader.ReadBytes(4); // "VOX "
            reader.ReadInt32();  // version

            // Flat chunk scan: every chunk here has childBytes 0 except MAIN (contentBytes 0),
            // whose children follow contiguously — so reading id/content/child then the content
            // bytes walks straight through SIZE, XYZI, RGBA.
            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                string id = new string(reader.ReadChars(4));
                int contentBytes = reader.ReadInt32();
                reader.ReadInt32(); // childBytes (0 for all but MAIN)

                if (id == "XYZI")
                {
                    int num = reader.ReadInt32();
                    for (int i = 0; i < num; i++)
                    {
                        int x = reader.ReadByte();
                        int y = reader.ReadByte();
                        int z = reader.ReadByte();
                        reader.ReadByte(); // colour index
                        positions.Add((x, y, z));
                    }
                }
                else
                {
                    reader.ReadBytes(contentBytes);
                }
            }
            return positions;
        }
    }
}
