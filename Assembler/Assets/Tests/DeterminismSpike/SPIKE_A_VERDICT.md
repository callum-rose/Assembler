# Spike A verdict — Fixed-step clock riding Unity's loop (issue #101)

**Approach evaluated:** keep Unity's normal Update/FixedUpdate loop, but on deterministic runs force a
constant per-frame delta via `Time.captureDeltaTime` and feed a `FixedStepGameClock` (constant
`DeltaTime`/`Time` per tick) through the existing `IGameClock` seam.

**Verdict: VIABLE for Level-1 determinism (same build, same machine), at near-zero refactor cost.**
All four make-or-break questions came back positive under direct test. One real operational caveat
(physics needs a clean scene per run) and two out-of-scope determinism sources (RNG, same-frame
ordering) remain — none of them specific to this approach; they hit Spike B identically.

The evidence below was produced by a throwaway EditMode suite,
`Assets/Tests/DeterminismSpike/PhysicsDeterminismSpikeTests.cs` (delete once this verdict is
accepted). Run: `Assembler/Tools/run-tests.sh Tests.DeterminismSpike`.

---

## Evidence per question

### Q1 — Reproducible frame count under `Time.captureDeltaTime` — YES (by construction)
`captureDeltaTime` forces `Time.deltaTime` to a constant every frame, so N `Update`s advance exactly
N gameplay ticks and `Clock.Time = N * step`. Nothing to measure — it is arithmetic. The clock's
`FrameCount` already increments once per `Tick()`.

### Q2 — Reproducible physics-step count per frame — YES (by construction)
Unity's fixed-timestep accumulator adds `deltaTime` each frame and drains it in `fixedDeltaTime`
chunks. With `deltaTime` constant the whole sequence is fixed. Test
`CaptureDeltaTime_YieldsDeterministicAndStableFixedStepCounts` (**PASS**) replicates the accumulator:
`captureDeltaTime = 1/60`, `fixedDeltaTime = 1/50` → 120 frames deterministically produce **120 ticks
and exactly 100 FixedUpdate steps**, per-frame pattern a stable repeating `[0,1,1,1,1,1,0,1,1,1,1,1…]`.

### Q2 (the crux) — Does PhysX reproduce on the same machine given identical steps? — YES
This is what the entire "physics later" provision rests on. Tested by manually stepping the physics
scene (`Physics.simulationMode = Script` + `Physics.Simulate`, the one physics path that runs in
EditMode) over a **24-box pile with overlapping spawns, initial linear + angular velocities, and a
static floor** — 400 steps at `0.02s`, checksumming every body's position+rotation each step.

- **Free-fall control** (`FreeFall_SingleBody_IsBitIdenticalAcrossRuns`, **PASS**): a single body's
  pure integration is bit-identical across runs — the harness itself is sound.
- **Cross-process, two fresh Unity boots** (`PhysX_WritePileChecksumForCrossProcessDiff` run twice
  with `SPIKE_RUN_LABEL=A|B`, then `diff`): **bit-identical checksum on all 400 contact-heavy steps.**
  → PhysX is deterministic same-build/same-machine from a clean start. **Level-1 physics determinism
  holds.**

- **Caveat found (operationally important):** two runs *in the same process* **diverge**, from the
  first contact, into gross chaotic difference by step 400 (logged: `body[0]` identical at step 0 but
  `-10.0,5.1,-4.9` vs `-4.2,4.1,-6.7` by step 399). Unity's **default `PxScene` is persistent**;
  tearing down actors and rebuilding an identical pile does *not* restore identical internal
  solver/broadphase ordering, so the same contacts resolve in a different float-summation order.
  **Implication:** a deterministic run (and especially in-editor replay without a domain reload) must
  start from a **fresh physics scene** — either a play-mode enter (which resets the default scene) or,
  better, a dedicated per-game `PhysicsScene` (`LocalPhysicsMode.Physics3D`) that the run steps
  itself. Free-fall / non-contact motion reproduces even under scene reuse; only the contact solver is
  ordering-sensitive.

### Q3 — Coroutine timer/interval triggers stay deterministic — YES (frame count), with a caveat
`WaitForGameSeconds` (`Assets/Time/WaitForGameSeconds.cs`) accumulates `Clock.DeltaTime` per poll, so
under fixed-step it fires after exactly `ceil(seconds / step)` frames — deterministic. **Caveat:** the
*count* is deterministic; the *order* in which multiple triggers resuming on the same frame call
`NotifyListeners` is Unity's coroutine-scheduler order, which is issue #101's separate
"iteration/execution order" source (item 3) — not something the clock fixes and not specific to Spike A.

### Q4 — Blast radius across the 13 `INeedsGameClock` behaviours — NEAR ZERO (1 file)
All 13 (`PerFrameBehaviour`, `ThrottledTrigger`, `DebouncedTrigger`, `Tap`, `DoubleTap`, `LongPress`,
`Swipe`, `TimerTrigger`, `IntervalTrigger`, `SetTimeScale`, `PerceiveAll`, `Perceive`,
`DeferredTrigger`) read only `Clock.DeltaTime` / `Clock.Time` / `Clock.FrameCount` / `Clock.TimeScale`
— all relative/elapsed semantics that behave identically when the delta is constant. They **just
work**. The only code reading wall-clock directly for gameplay is `CameraOrbit.cs:75`
(`UnityEngine.Time.deltaTime` for auto-orbit) — presentation-only, and would want `Clock.DeltaTime`
for a clean fixed-step run. `DebugConsole` (`realtimeSinceStartup`, UI ping) is correctly left
unscaled. Hypothesis in the handoff confirmed.

### Q5 — Headless feasibility (informational; #142 parked) — FEASIBLE
The physics tests drive stepping via the explicit `Physics.Simulate` API and run headlessly in
EditMode batch — i.e. a **driveable-tick seam already exists** for a future headless harness. The
*full* Update/FixedUpdate auto-pump still needs PlayMode (Awake/Update don't fire in the EditMode
sandbox), so a headless `simulate-game.sh` would either use PlayMode batch or a small explicit tick
driver. No blocker; just noting the shape.

---

## Recommendation

**Adopt Spike A unless Spike B shows a concrete advantage it does not.** A keeps Unity's loop, adds one
clock implementation, changes one behaviour, and rides `captureDeltaTime` for free — minimal surface
area and it leaves the "physics later" seam genuinely open (proven, not assumed). Its weakness is that
it does **not** give a central, inspectable tick order — every behaviour still self-pumps in Unity's
undefined `Update` order, so issue #101's ordering requirement (item 3) is unaddressed by A. That is
precisely what Spike B (central `ITickable` dispatcher) is meant to buy. So the real A-vs-B decision is:

> Is deterministic *execution order* worth a central dispatcher's refactor cost now, or is it deferrable
> (fix same-frame ordering narrowly — sort tag broadcasts, order the velocity stack — and keep A's tiny
> footprint)?

For Level-1 replay of the LLM-generated games, A is sufficient for time and physics today. Recommend
**A for the clock**, and treat execution-order determinism as a separate, smaller workstream rather
than a reason to take B wholesale. Final call pending Spike B's verdict.

## Phase 1 migration sketch (if A is chosen)
1. **`FixedStepGameClock : IGameClock`** in `Assets/Time/` — constant `DeltaTime = step` (default
   `1/60`), `Time += step` / `FrameCount++` per `Tick()`, honouring `Pause`/`Step`/`TimeScale` exactly
   as `RealtimeGameClock` does. On its driver, set `Time.captureDeltaTime = step` while active and
   restore `0` on teardown.
2. **Widen `GameClockDriver.Clock`** from `RealtimeGameClock` to `IGameClock` (already the seam the
   docs call out).
3. **Clock selection in `Builder`** — `Builder.cs:141` news up `RealtimeGameClock` unconditionally;
   inject the choice (realtime vs fixed-step) from a run option / replay header. `ExclusiveGroupRegistry`
   and `GameClockDriver` already take `IGameClock`, so this is the only construction site.
4. **`CameraOrbit`** → read `Clock.DeltaTime` instead of `UnityEngine.Time.deltaTime` (or document it as
   presentation-only like the Cinemachine exemption).
5. **Physics runs (when in scope):** give a deterministic run its **own `PhysicsScene`** and step it
   from the fixed-step driver, rather than relying on the shared default scene — per the Q2 caveat.
6. Out of scope for the clock but required for full #101 replay: seeded PRNG through `RandomMath`, and
   same-frame execution/notification ordering (item 3).
