# Palette Extraction — build handoff

Extract the **fundamental colours** of an object from an AI-generated image (ignoring the
background), producing a small representative palette that **replaces** the voxeliser's current
per-model colour clustering. Part of the fully-automated `text → image → mesh → voxel` pipeline,
so it must run **headlessly and deterministically** with no human intervention.

Example: the turtle image → `{ light green, dark green, black }` (3 colours), *not* the ~7 shaded
variants that naive clustering would produce; the light-blue background is ignored.

This doc is the complete spec. The design was settled in a grilling session; **all decisions below
are locked** unless a build-time discovery forces a change (flag it if so).

---

## Status at handoff

**Done**
- Design fully specified (this doc).
- Tuning corpus generated: **16 JPEGs** in `TuningCorpus/` (regenerated on `gemini-3-pro-image`).
  Each stresses a specific failure mode — see *Tuning corpus* below.
  ⚠️ `gemini-3-pro-image` returns **JPEG** (lossy — the generator exposes no PNG toggle). Fine here:
  flat-shaded content compresses cleanly, and if production uses this model the pipeline feeds JPEG
  anyway, so tuning on JPEG is representative. If a swatch shows JPEG ringing at colour edges, the
  erosion + coverage/compactness defences should absorb it; if not, regenerate that image on
  `gemini-2.5-flash-image` (returns lossless PNG, weaker prompt adherence).
- `Editor/CorpusGeneratorBatch.cs` — throwaway dev tool that generated the corpus by driving
  `ImageGenerationCore` headlessly (reads provider/key from the image window's `EditorPrefs`;
  `CORPUS_MODEL` env var pins the model). Menu: **Assembler → Palette Extraction → Generate Tuning
  Corpus**. Keep it; it's how you regenerate fixtures.
- `Editor/Assembler.AssetGeneration.PaletteExtraction.Editor.asmdef` — exists, references only the
  TextToImage runtime so far (for the corpus tool).

**Not done (the actual build)** — see *Build plan*.

**Incidental fix already applied:** `TextToVoxelPipeline/Editor/VoxelPipeline.cs` was missing
`using Assembler.AssetGeneration.MeshToVoxel.Editor;` (the despike rename `da87458` moved
`ModelLoader` into the `.Editor` namespace). Master already contains the real fix, so this local
one-liner collapses to a no-op on merge — **do not open a separate PR for it.**

> ⚠️ **Read the worktree, not absolute paths.** The user's main checkout is on a *different, older*
> branch (pre-despike, `MeshToVoxelSpike/` naming). This worktree is master (`MeshToVoxel/`).
> Re-read the real files listed below from the worktree before coding — don't trust memory of an
> earlier session's reads.

---

## Locked design decisions

| Decision | Choice | Why |
|---|---|---|
| Approach | **Logical, no AI** | Deterministic, instant, zero API cost in a batch pipeline. AI count-guessing is nondeterministic and per-asset costly. |
| Background removal | **Border-median detect → flood-fill from edges → erode 1–2px** | Flood-fill (not global colour-match) survives an object legitimately containing the bg colour. Erosion kills the anti-alias halo. Tolerance band (not exact match) — bgs have mild vignettes. |
| Alpha channel | Early-out mask if present, else flood-fill | Generators here emit **solid** bgs (no transparency), so flood-fill is the primary path; alpha is a cheap defensive branch. |
| Colour count | **Emergent** via Oklab tolerance-merge (reuse `ColourModes.Consolidate`) | Never pick K. Shading steps fall within tolerance and merge; distinct materials don't. |
| Count bias | **Over-segment** (loose-ish tolerance, generous max-cap) | Under-segmentation is destructive (a whole material vanishes); over-segmentation yields harmless near-duplicate swatches. |
| Spurious-colour defence | **Coverage-threshold + spatial-compactness** | Coverage alone can't tell a 0.5%-coverage black eye (keep) from a 0.5% purple speckle (drop). Compact/connected blob ⇒ keep; scattered ⇒ drop. |
| Extraction stage | **2D source image** (not post-voxel) | Millions of pixels ⇒ stable stats; spatial coherence available; decoupled from mesh quality; upstream so it can drive voxelisation. Post-voxel is circular (voxeliser already clusters) and noisy. |
| Downstream integration | Emit `MasterPalette`; force voxeliser to `ColourMode.MasterPalette`, **replacing** `PerModelPalette` | Voxeliser snaps de-lit voxel colours to the clean image-derived palette — this also *cleans delight noise* for free. |
| Core shape | **Engine-free, unit-testable** core + dev-only editor window | Mirrors `MeshVoxeliser` / `ImageFacingDirection`. Window is for tuning only, NOT part of automation. |
| Colour-math reuse | **Option B** — promote primitives to a shared assembly | Clean layering; avoids a pre-voxel module depending on the voxel assembly. |
| Output | In-memory data only (`IReadOnlyList<Rgba32>` + diagnostics) | No PNG/asset sidecar — pure hand-off to the next pipeline stage. |

---

## Build plan

### 1. Promote colour primitives to a shared assembly (option B)

Create `Assets/AssetGeneration/Colour/` with asmdef **`Assembler.AssetGeneration.Colour`** and
**move** these from `Assets/AssetGeneration/MeshToVoxel/Core/` into it:
`Rgba32.cs`, `OklabColor.cs`, `FMath.cs`, `ColourModes.cs` (+ their `.meta`; let Unity handle metas).

- Renamespace them `Assembler.AssetGeneration.MeshToVoxel` → **`Assembler.AssetGeneration.Colour`**.
- Add `using Assembler.AssetGeneration.Colour;` (or fully-qualify) at every reference site left in
  `MeshToVoxel/Core` and `MeshToVoxel/*` (SpikeSettings/ColourReprojector/etc. use `ColourModes`,
  `Rgba32`, `OklabColor`, `FMath`). Make `MeshToVoxel` (Core) asmdef reference the new one.
- Watch `csc.rsp` needs: the Core has `-langVersion:preview` / nullable settings — replicate what
  the moved files require (see `MeshToVoxel/Core/csc.rsp`). Editor/test asmdefs in this project lack
  the `IsExternalInit` polyfill and nullable context (`CS8632`) — see the repo memory notes.
- Verify with `Tools/check-compile.sh` before proceeding — this refactor touches the whole
  `MeshToVoxel` tree and is the riskiest step.

`ColourModes.Consolidate` / `PerModelPalette` (deterministic k-means / leader-cluster / agglomerative
merge-to-count in Oklab, frequency-weighted means) is the engine you reuse — do **not** reimplement.

### 2. Palette-extraction core (engine-free runtime)

New folder `PaletteExtraction/Runtime/` + asmdef **`Assembler.AssetGeneration.PaletteExtraction`**
referencing `Assembler.AssetGeneration.Colour`. No `UnityEngine` types in the core (take a pixel
array, not `Texture2D`) so it's unit-testable like `MeshVoxeliser`.

```
public readonly struct PaletteExtractionOptions
{
    public float BackgroundTolerance { get; init; }   // Oklab; flood-fill match radius
    public int    ErodePixels { get; init; }          // 1–2; halo kill
    public float  MergeTolerance { get; init; }        // Oklab; shading-step collapse (bias loose)
    public int    MaxColours { get; init; }            // hard cap (generous; over-segment safe)
    public float  MinCoverage { get; init; }           // drop clusters below this fraction of object px…
    public float  MinCompactness { get; init; }        // …UNLESS spatially compact (keeps small features)
    public static PaletteExtractionOptions Default { get; }   // tuned against TuningCorpus
}

public readonly struct PaletteResult
{
    public IReadOnlyList<Rgba32> Palette { get; }      // the fundamental colours (feed as MasterPalette)
    public Rgba32 Background { get; }                  // detected bg colour
    public IReadOnlyList<int> Coverage { get; }        // per-swatch object-pixel count (diagnostics)
    public int ObjectPixelCount { get; }
    public bool[] ObjectMask { get; }                  // for the window's masked preview
}

public static class PaletteExtractor
{
    public static PaletteResult Extract(Rgba32[] pixels, int width, int height, PaletteExtractionOptions options);
}
```

**Algorithm:**
1. **Background detect** — median of border pixels (robust if the object touches an edge).
2. **Mask** — if a real alpha channel: `alpha < threshold` ⇒ bg. Else flood-fill from all 4 edges,
   removing pixels within `BackgroundTolerance` (Oklab) of the bg colour (connected to the edge only).
3. **Erode** the object mask `ErodePixels` to drop the anti-alias halo.
4. **Histogram** the surviving object pixels (exact RGB counts) → feed as the weighted sample set to
   `ColourModes.Consolidate(colours, mask, MergeTolerance, MaxColours)` (or a thin wrapper) →
   emergent clusters with frequency-weighted mean swatches.
5. **Spurious-cluster defence** — for each cluster below `MinCoverage`: keep it only if its member
   pixels form a spatially **compact/connected** region (connected-component / bbox-fill-ratio ≥
   `MinCompactness`); else drop and re-assign its pixels to the nearest surviving swatch. This is the
   piece beyond `Consolidate` — it needs pixel positions, so compute it here, not in `ColourModes`.
6. Order palette by descending coverage; return.

Determinism: no `Random`, no `Date`/time, stable tie-breaks (mirror `Consolidate`'s deterministic
seeding). Must produce identical output for identical input.

### 3. Editor window (dev/tuning only)

`PaletteExtraction/Editor/PaletteExtractionWindow.cs` (menu **Assembler → Palette Extraction →
Extract Palette**). Mirror `ImageOrientationWindow`: load a PNG → decode to `Rgba32[]` (editor does
the `Texture2D`/`ImageConversion` decode) → run `PaletteExtractor.Extract` → show the masked image,
the swatch strip with coverage %, and sliders for every `PaletteExtractionOptions` field. **Not**
part of the automated pipeline.

### 4. Pipeline integration

In `TextToVoxelPipeline/Editor/VoxelPipeline.cs` (`RunAsync`), after the image stage decode the PNG,
call `PaletteExtractor.Extract`, and set the stage-3 voxel settings to
`ColourMode.MasterPalette` with `MasterPalette = result.Palette` — **overriding** the current
`PerModelPalette` default. (Confirm the exact `SpikeSettings`/`ColourMode` field names in the
worktree — `MeshToVoxel/Core/`.) The standalone Mesh→Voxel window keeps `PerModelPalette` available.

### 5. Tests

`PaletteExtraction/Tests/` (EditMode). Feed the `TuningCorpus` JPEGs (decode with
`ImageConversion.LoadImage`) and assert the expected fundamental **count** per image (see table).
Tune `PaletteExtractionOptions.Default` until the corpus passes. Note existing gotchas:
`Tests.*` overrideReferences may need `System.Text.Json.dll` listed; editor/test asmdefs lack
nullable context + `IsExternalInit`.

---

## Tuning corpus (`TuningCorpus/`, gemini-3-pro-image)

Expected fundamentals per image, and what each stresses:

| slug | fundamentals | stresses |
|---|---|---|
| turtle | 3 (+tan plastron) | same-hue two greens; tiny black eye; shading steps |
| fox | 3–4 | mid palette, small dark accents |
| snowman | 4 | **white body with shading** must stay one material |
| crate | **1** | strong face-shading must collapse to a single colour |
| tree | 2 | foliage shading steps → one green |
| ladybug | 2 | small black spots — coverage-vs-feature |
| penguin | 3 | tiny orange accents; neutral black/white |
| chicken | 3–4 | multiple small saturated accents |
| panda | 2 | pure neutrals, zero hue |
| robot | 3–4 | near-neutral body + one vivid accent (chroma-gain) |
| cat | 4 | low-chroma near-tolerance stripe separation |
| traffic-cone | 2 | high-contrast bands both survive |
| parrot | 6–7 | upper-bound count + **max-cap** |
| blue-whale | 3 | **object blue collides with bg blue** — flood-fill + erode |
| bee | 3 | high-freq stripes must not average to mud |
| gem | 1–2 | smooth-gradient shading (assumption-breaker; expected-hard) |

Regenerate with **Assembler → Palette Extraction → Generate Tuning Corpus** (or batch:
`CORPUS_MODEL=gemini-3-pro-image … -executeMethod …CorpusGeneratorBatch.Run`). Requires a Gemini key
saved via the Image Generation window. Note: gemini sometimes adds a faint ground/vignette despite
"plain background / no shadows" — that's why bg detection uses a tolerance band.

---

## Key references (worktree paths — verify before coding)

- Reuse engine: `Assets/AssetGeneration/MeshToVoxel/Core/ColourModes.cs` (`Consolidate`,
  `PerModelPalette`, `ColourMode` enum incl. `MasterPalette`), `OklabColor.cs`, `Rgba32.cs`, `FMath.cs`.
- Voxel settings / snap: `Assets/AssetGeneration/MeshToVoxel/Core/` (`SpikeSettings`,
  `ColourModes.MasterPaletteSnap` — nearest-swatch in Oklab with a chroma-gain penalty).
- Headless-core + window pattern to mirror: `ImageOrientation/ImageFacingDirection.cs` +
  `ImageOrientation/Editor/ImageOrientationWindow.cs`.
- Pipeline: `TextToVoxelPipeline/Editor/VoxelPipeline.cs` (`RunAsync`), README in that folder.
- Corpus tool: `PaletteExtraction/Editor/CorpusGeneratorBatch.cs`.
