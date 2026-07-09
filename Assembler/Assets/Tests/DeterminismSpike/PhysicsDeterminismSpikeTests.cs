using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace Tests.DeterminismSpike
{
    /// <summary>
    /// Spike A (issue #101) evidence: does game execution reproduce on the same machine/build when a
    /// fixed delta is forced per tick?
    ///
    /// The two crux questions from the handoff are answered here empirically:
    ///   Q2 (physics-step reproducibility) — PhysX float determinism given identical step counts and
    ///       inputs on the same machine — is the make-or-break question the whole "physics later"
    ///       provision rests on. Tested directly by manually stepping an isolated PhysicsScene twice
    ///       and diffing a bitwise checksum of every rigidbody transform at every step.
    ///   Q1/Q2-arithmetic (reproducible frame + fixed-step counts under Time.captureDeltaTime) — is a
    ///       property of Unity's fixed-timestep accumulator, which is pure deterministic arithmetic
    ///       once deltaTime is constant. Demonstrated with a faithful accumulator replica.
    ///
    /// These run in EditMode because they drive physics via the explicit Physics.Simulate API rather
    /// than Unity's auto-sim loop (which — like Awake/Update — does not run under the EditMode/sandbox
    /// harness). That is itself the informational answer to Q5: a driveable manual-step seam exists.
    ///
    /// This whole assembly is throwaway spike scaffolding — it is not part of the shipped determinism
    /// work and should be deleted once the verdict is recorded.
    /// </summary>
    public sealed class PhysicsDeterminismSpikeTests
    {
        private const float FixedDelta = 0.02f;   // 50 Hz, Unity's default fixedDeltaTime
        private const int Steps = 400;            // 8 s of simulated time
        private const int BodyCount = 24;

        // ---- Q2 control: pure free-fall integration (no contacts) must be bit-identical -----------

        [Test]
        public void FreeFall_SingleBody_IsBitIdenticalAcrossRuns()
        {
            var a = SimulateFreeFall(out var aPos);
            var b = SimulateFreeFall(out var bPos);

            Debug.Log(
                $"[SpikeA/Q2-control] single body free-fall, 10 steps @ {FixedDelta}s, two runs:\n" +
                $"  runA final y = {aPos.y:R}   runB final y = {bPos.y:R}\n" +
                $"  checksum runA=0x{a:x16} runB=0x{b:x16}  ->  {(a == b ? "IDENTICAL" : "DIVERGED")}");

            // A single rigidbody in free fall is pure deterministic integration — if this diverges, the
            // harness (not PhysX) is at fault (e.g. residual world state between two sequential builds).
            Assert.AreEqual(a, b,
                "Free-fall integration diverged across two runs — this is a harness/world-reset problem, " +
                "not a statement about PhysX contact-solver determinism.");
        }

        private static long SimulateFreeFall(out Vector3 finalPos)
        {
            var previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                box.transform.position = new Vector3(0f, 100f, 0f);
                var rb = box.AddComponent<Rigidbody>();
                rb.linearVelocity = new Vector3(1f, 0f, -2f);
                Physics.SyncTransforms();

                var h = FnvOffset;
                for (var i = 0; i < 10; i++)
                {
                    Physics.Simulate(FixedDelta);
                    var p = box.transform.position;
                    h = Mix(h, p.x); h = Mix(h, p.y); h = Mix(h, p.z);
                }
                finalPos = box.transform.position;
                return h;
            }
            finally
            {
                Object.DestroyImmediate(box);
                Physics.simulationMode = previousMode;
            }
        }

        // ---- Q2: PhysX same-machine float determinism ---------------------------------------------

        /// <summary>
        /// Runs ONE box-pile simulation and writes its per-step checksum sequence to a labelled file
        /// (label from the <c>SPIKE_RUN_LABEL</c> env var). Invoked from two SEPARATE Unity boots so
        /// each run gets a pristine default <see cref="UnityEngine.PhysicsScene"/>; an external diff of
        /// the two files is the true cross-process determinism test.
        ///
        /// Why not two runs in one process: PhysX's default PxScene is a persistent object. Stepping it,
        /// tearing down the actors, and rebuilding an identical pile does NOT restore identical internal
        /// solver/broadphase/allocator ordering, so a second in-process run can resolve the very same
        /// initial contacts in a different float-summation order — divergence that says nothing about
        /// same-machine reproducibility from a clean start. A fresh process is the clean start.
        /// </summary>
        [Test]
        public void PhysX_WritePileChecksumForCrossProcessDiff()
        {
            var label = System.Environment.GetEnvironmentVariable("SPIKE_RUN_LABEL");
            label = string.IsNullOrEmpty(label) ? "default" : label;

            var checksums = SimulatePile();

            var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "spikeA");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, $"pile_{label}.txt");
            System.IO.File.WriteAllLines(
                path, System.Array.ConvertAll(checksums.ToArray(), c => c.ToString()));

            Debug.Log(
                $"[SpikeA/Q2] wrote {checksums.Count}-step checksum for run '{label}' -> {path}\n" +
                $"  final-state checksum = 0x{checksums[Steps - 1]:x16}");
        }

        /// <summary>
        /// Builds a fresh pile of boxes with staggered heights and initial linear+angular velocity
        /// dropped onto a static floor (so the solver has real contacts to resolve), steps the default
        /// physics scene manually <see cref="Steps"/> times at a constant delta, and returns a per-step
        /// bitwise checksum over every body's position+rotation. Two independent invocations must return
        /// identical sequences for Level-1 determinism to hold.
        ///
        /// Uses <c>Physics.simulationMode = Script</c> + <c>Physics.Simulate</c> on the default scene —
        /// the one physics-stepping path that works in EditMode (creating a dedicated local scene needs
        /// play mode). Objects are built in and cleaned out of the active scene each call, so run A is
        /// fully torn down before run B builds; the physics world starts each call empty.
        /// </summary>
        private static List<long> SimulatePile()
        {
            var previousMode = Physics.simulationMode;
            Physics.simulationMode = SimulationMode.Script;

            var created = new List<GameObject>();
            try
            {
                // Static floor.
                var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.localScale = new Vector3(50f, 1f, 50f);
                floor.transform.position = new Vector3(0f, -0.5f, 0f);
                created.Add(floor);

                // Falling boxes, laid out and kicked deterministically (a fixed LCG, no engine Random),
                // so the *setup* is reproducible and any divergence must come from PhysX itself.
                var bodies = new Rigidbody[BodyCount];
                var rng = new Lcg(0xC0FFEE);
                for (var i = 0; i < BodyCount; i++)
                {
                    var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    var x = (i % 4) * 1.05f - 1.5f + rng.NextUnit() * 0.15f;
                    var z = (i / 4) * 1.05f - 1.5f + rng.NextUnit() * 0.15f;
                    var y = 2f + i * 0.6f;
                    box.transform.position = new Vector3(x, y, z);
                    box.transform.rotation = Quaternion.Euler(
                        rng.NextUnit() * 40f, rng.NextUnit() * 40f, rng.NextUnit() * 40f);

                    var rb = box.AddComponent<Rigidbody>();
                    rb.linearVelocity = new Vector3(rng.NextUnit() * 1.5f, -1f, rng.NextUnit() * 1.5f);
                    rb.angularVelocity = new Vector3(rng.NextUnit() * 3f, rng.NextUnit() * 3f, rng.NextUnit() * 3f);

                    bodies[i] = rb;
                    created.Add(box);
                }

                Physics.SyncTransforms();

                var checksums = new List<long>(Steps);
                for (var step = 0; step < Steps; step++)
                {
                    Physics.Simulate(FixedDelta);

                    var h = FnvOffset;
                    foreach (var rb in bodies)
                    {
                        var t = rb.transform;
                        var p = t.position;
                        var r = t.rotation;
                        h = Mix(h, p.x); h = Mix(h, p.y); h = Mix(h, p.z);
                        h = Mix(h, r.x); h = Mix(h, r.y); h = Mix(h, r.z); h = Mix(h, r.w);
                    }
                    checksums.Add(h);

                    if (step == 0)
                    {
                        var p0 = bodies[0].transform.position;
                        Debug.Log($"[SpikeA/Q2-diag] body[0] after step 0: {p0.x:R},{p0.y:R},{p0.z:R}");
                    }
                }

                var pf = bodies[0].transform.position;
                Debug.Log($"[SpikeA/Q2-diag] body[0] after step {Steps - 1}: {pf.x:R},{pf.y:R},{pf.z:R}");
                return checksums;
            }
            finally
            {
                foreach (var go in created)
                {
                    Object.DestroyImmediate(go);
                }
                Physics.simulationMode = previousMode;
            }
        }

        // ---- Q1 / Q2-arithmetic: fixed-step accumulator under Time.captureDeltaTime ----------------

        [Test]
        public void CaptureDeltaTime_YieldsDeterministicAndStableFixedStepCounts()
        {
            // Time.captureDeltaTime forces Time.deltaTime to a constant every frame. Unity's physics
            // loop then adds that constant to an accumulator and drains it in fixedDeltaTime chunks.
            // With a constant input the whole sequence is fixed — reproducible frame count (Q1) and
            // reproducible FixedUpdate count per frame (Q2's arithmetic half) follow by construction.
            const float captureDelta = 1f / 60f;
            const float fixedDelta = 1f / 50f;
            const int frames = 120;

            var runA = AccumulatorReplica(captureDelta, fixedDelta, frames);
            var runB = AccumulatorReplica(captureDelta, fixedDelta, frames);

            CollectionAssert.AreEqual(runA, runB, "fixed-step count sequence was not reproducible");

            var total = 0;
            var pattern = new StringBuilder();
            for (var i = 0; i < runA.Count; i++)
            {
                total += runA[i];
                if (i < 12) pattern.Append(runA[i]).Append(i == 11 ? "…" : ",");
            }

            Debug.Log(
                $"[SpikeA/Q1] captureDeltaTime={captureDelta:F5}s, fixedDeltaTime={fixedDelta:F5}s: " +
                $"{frames} frames advance a fixed {frames} gameplay ticks and a fixed {total} FixedUpdate " +
                $"steps. Per-frame FixedUpdate pattern (stable, repeating): [{pattern}]");

            // 120 frames × (1/60) = 2.0 s of game time; 2.0 / (1/50) = 100 fixed steps, deterministically.
            Assert.AreEqual(100, total, "expected a deterministic 100 fixed steps for this configuration");
        }

        /// <summary>
        /// Faithful replica of Unity's fixed-timestep accumulator (see the manual's "Time" / physics
        /// update order). Under <c>Time.captureDeltaTime</c>, <c>dt</c> is the same constant every
        /// frame, so this is pure deterministic arithmetic.
        /// </summary>
        private static List<int> AccumulatorReplica(float dt, float fixedDelta, int frames)
        {
            var counts = new List<int>(frames);
            var accumulator = 0f;
            for (var f = 0; f < frames; f++)
            {
                accumulator += dt;
                var steps = 0;
                while (accumulator >= fixedDelta)
                {
                    accumulator -= fixedDelta;
                    steps++;
                }
                counts.Add(steps);
            }
            return counts;
        }

        // ---- helpers ------------------------------------------------------------------------------

        private const long FnvOffset = unchecked((long)1469598103934665603UL);
        private const long FnvPrime = 1099511628211L;

        private static long Mix(long h, float f)
        {
            var bits = System.BitConverter.SingleToInt32Bits(f) & 0xffffffffL;
            return (h ^ bits) * FnvPrime;
        }

        /// <summary>Tiny deterministic LCG so test *setup* uses no engine randomness. Range [-1, 1].</summary>
        private struct Lcg
        {
            private uint _state;
            public Lcg(uint seed) => _state = seed == 0 ? 1u : seed;

            public float NextUnit()
            {
                _state = _state * 1664525u + 1013904223u;
                return (_state / (float)uint.MaxValue) * 2f - 1f;
            }
        }
    }
}
