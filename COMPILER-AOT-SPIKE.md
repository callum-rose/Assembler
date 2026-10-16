# Compiler AOT spike — cross-platform harness

**Branch:** `claude/compiler-aot-spike-handoff-53437c` — nothing here merges to `master`.
**Status:** everything green in the editor. **Zero device evidence yet** — the builds are yours to run.

Closes the two open items under ROADMAP § "Phase 0 — iOS AOT de-risk", and is the (larger) version of
SHIPPLAN § "Phase 1" Days 1–2.

---

## The question

Does `ExpressionMethodCompiler` — which builds expression trees and calls `lambda.Compile()` at
runtime — work under IL2CPP on iOS and Android? Under AOT there is no JIT, so a delegate the compiler
assembles at runtime can only run if IL2CPP already emitted native code for every generic
instantiation it touches. It cannot see an expression tree statically, so anything it did not emit is
an `ExecutionEngineException` at best and a hard crash at worst.

The answer this harness gives is a **per-construct verdict**, not a binary pass/fail: 233 individually
named cases, each of which must **compile *and* invoke**. AOT can fail at either point, and a
compile-only check would sail past the invocation failures that are the more common symptom.

## What's here

| Path | What |
|---|---|
| `Assembler/Assets/Spike/CompilerHarness/` | The harness — runtime asmdef, runner, case corpus |
| `Assembler/Assets/Spike/CompilerHarness/Editor/` | Editor-only batch runners and scene setup |
| `Assembler/Assets/Spike/CompilerHarness/CompilerHarness.unity` | The scene, build scene **0** |
| `Assembler/Assets/StreamingAssets/StressTest.yaml` | The descriptor half |

Nothing in a shipping assembly is touched. `Spike.CompilerHarness` is a new runtime asmdef referencing
only `Assembler.Compiler`, `Assembler.Libraries`, `Assembler.Building` and `Assembler.Input`.

**No NUnit in the build.** The asserts are a bespoke, deliberately non-generic `Check` class. NUnit
does its own reflection and generic instantiation under IL2CPP, so a failure inside it would be
indistinguishable from a failure of the thing being measured.

### The corpus — 233 cases

| Group | Cases | Source |
|---|--:|---|
| Ported EditMode suites | **161** | one-to-one port of all 9 files in `Assets/Tests/Compiler/` |
| Adversarial: value-type generics | 17 | new — highest priority |
| Adversarial: high-arity delegates | 26 | new — drives `DelegateTypeHelper` to its 16/17 ceiling |
| Adversarial: boxing & numeric promotion | 16 | new — asserts *values*, catches wrong answers |
| Adversarial: nesting & closure capture | 13 | new |

The 161 are ported rather than reused because `Tests.Compiler.asmdef` is `includePlatforms: ["Editor"]`
with a `UNITY_INCLUDE_TESTS` constraint — it cannot enter a player build. Bodies were copied verbatim
(hence `-langVersion:preview` in the harness `csc.rsp`, matching the test assembly): a transcription
slip would masquerade as an AOT finding, which is the one failure mode the handover gate exists to
eliminate.

### The two halves, two verdicts

1. **Flat case list** → drives `ExpressionMethodCompiler` directly.
2. **`StressTest.yaml`** → goes through `Builder`, reaching the *parse-layer* `MakeGenericType` sites
   (`ValueSourceFactory`, `ExpressionSynthesis`, `TransformContext`) that a raw-compiler harness never
   touches. It pushes a value of every supported type — int, float, bool, string, vector, colour, and
   the int/float/vector list forms — through a named expression with explicit `ArgumentTypes` into the
   matching typed setter, every frame.

---

## Running it

### Editor (already done, re-runnable)

```bash
cd Assembler && ./Tools/check-compile.sh
```

Menu items under **Assembler > Spike**, or headless:

```bash
cd Assembler && "/Applications/Unity/Hub/Editor/6000.4.5f1/Unity.app/Contents/MacOS/Unity" -batchmode -nographics -projectPath "$PWD" -executeMethod Spike.CompilerHarness.Editor.CompilerSpikeBatch.RunCases -logFile -
```

That runs the flat corpus without entering Play mode and exits non-zero on any failure. The full
runner (both halves) can be driven headlessly through Play mode with
`Spike.CompilerHarness.Editor.CompilerSpikePlayModeBatch.RunPlayMode`.

### Device

The scene is already build scene 0, so a normal player build boots straight into it. `Bootstrap.unity`
is left enabled and untouched, just no longer first — nothing loads it.

**Your prerequisites, not done here:**
- **Android Build Support is not installed.** `PlaybackEngines/` has only `iOSSupport`, `ANDROID_HOME`
  is unset, and the Android bundle id is still the URP template default
  (`com.UnityTechnologies.com.unity.template.urpblank`). Install the module and set a real package name.
- **iOS signing.** SHIPPLAN says the Apple Developer account doesn't exist yet. Free provisioning with
  a personal Apple ID (7-day dev builds) should be enough.

Readout is `Debug.Log` only — Xcode console and `adb logcat`. There is deliberately no on-screen UI: a
real IL2CPP failure can be a hard crash that an on-screen report wouldn't survive.

### Reading the log

```
COMPILER-SPIKE START platform=IPhonePlayer unity=6000.4.5f1 il2cpp=True
COMPILER-SPIKE CASES: 233 total, starting at index 0
RUN [0] ArithmeticAndOperator/CompilerTestsSimplePasses
RUN [1] ArithmeticAndOperator/SimpleAddition
...
COMPILER-SPIKE DESCRIPTOR: PASS
COMPILER-SPIKE SUMMARY: 233 passed, 0 failed
```

- `RUN [n] <id>` is logged **before** the case executes, so if the process dies the last `RUN` line
  names the offender.
- `FAIL [n] <id>: <Exception>: <message>` for a catchable failure.
- `COMPILER-SPIKE SUMMARY` is always the last line — grep for it.

**If a case hard-crashes the player:** note its index from the last `RUN` line, set `_startIndex` on
the `Compiler Spike Runner` GameObject past it, rebuild, and continue. Repeat to collect every
offender rather than stopping at the first.

**Do not reorder `AllCases.Register`** once a device run has started — indices are what `_startIndex`
takes.

### The two runs

1. **Run 1** — as-shipped `Assets/link.xml` (permissive: 11 whole assemblies with `preserve="all"`,
   its own comment says "Trim this list later").
2. **Run 2** — with `link.xml` trimmed.

The delta is the evidence for how much can be stripped. A case that passes in run 1 and fails in run 2
was being kept alive by the permissive preserve, which is exactly what you want to know before
trimming.

---

## What a red case means

A red case on device means **AOT, full stop** — no triage needed. That is the entire point of the
handover gate: every one of the 233 cases is green in the editor, so a mis-authored case cannot be
confused for an AOT finding.

Fixes are **out of scope**. They would land in shipping assemblies, which is the opposite of
throwaway. Report the failing case ids and decide separately.

---

## Known gaps — read before trusting a green run

1. **The first evidence anything survives IL2CPP comes from your build.** The iOS Simulator smoke test
   was offered and declined, so a build-time problem in the harness surfaces on your machine, not here.
   A green editor run says the corpus is well-formed; it says nothing about AOT.
2. **Two constructs from the spec could not be covered — both compiler limitations, neither an AOT
   finding:**
   - **`Aggregate`** — its useful overloads take a two-parameter lambda; the compiler supports
     single-parameter lambdas only.
   - **`SelectMany`** — `groups.SelectMany(g => g)` fails to compile: the lambda's return type infers
     as `IEnumerable<object>` rather than `IEnumerable<int>`. Nested-collection flattening is covered by
     `NestedGenericListFlattenedByIndexer` instead, which reaches the same `List<List<int>>`
     instantiations through an indexer.

   Both are worth filing against the compiler independently of this spike.
3. **`BoxingValueProvider` is only reached by the descriptor half.** It lives in `Assembler.Resolving`
   and adapts an `IValueProvider` to `IValueProvider<object>` — reachable only through a resolved
   descriptor value, never from the raw compiler. The C# family covers the compiler-level boxing
   conversion (the same CLR operation); the provider wrapper itself rides on the descriptor verdict.
4. **`StressTest.yaml` has no UI.** `text label` needs the `UiPrefabLibrary` asset, and a missing asset
   would fail the build for a reason unrelated to AOT — exactly the false negative this spike exists to
   avoid. So the UI behaviours are not part of the descriptor verdict.
5. **The Android StreamingAssets path is handled here but not in `GameBootstrap`.** On Android
   `Application.streamingAssetsPath` is a `jar:file://` URL into the APK where `File` silently fails.
   The harness reads it with `UnityWebRequest`; `GameBootstrap.cs` still uses `File` directly because it
   was written for iOS only. **That is a real bug for any future Android player build** and is worth
   fixing on `master` separately.

---

## Disposal

1. **Assembler > Spike > Remove Harness Scene From Build Settings** (restores `Bootstrap.unity` to
   scene 0).
2. Delete `Assembler/Assets/Spike/`.
3. Delete `Assembler/Assets/StreamingAssets/StressTest.yaml`.

Nothing else was modified. `ROADMAP.md` on `master` is deliberately untouched — what `master` learns
from this is your call.
