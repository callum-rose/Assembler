# Palette Extraction

Extract the **fundamental colours** of an object from an AI-generated image, ignoring the background,
producing a small representative palette. This **replaces** the voxeliser's per-model colour clustering
in the `text → image → mesh → voxel` pipeline: the palette is fed to the voxeliser as a master palette
so voxel colours snap to a clean, image-derived set (which also cleans de-light noise for free).

Example: the turtle image → `{ dark green, light green (head), light green (legs), black eye }` — not the
~7 shaded variants naive clustering yields; the light-blue background is ignored.

Runs **headlessly and deterministically** (no AI, no `Random`/time; identical input → identical output),
so it drops into the automated pipeline with no human intervention.

## Layout

| Path | Assembly | Role |
|---|---|---|
| `../Colour/` | `Assembler.AssetGeneration.Colour` | Shared, engine-free colour primitives (`Rgba32`, `OklabColor`, `FMath`, `ColourModes`) promoted out of the voxeliser so a pre-voxel module can reuse them without depending on the voxel assembly. |
| `Runtime/` | `Assembler.AssetGeneration.PaletteExtraction` | Engine-free core: `PaletteExtractor.Extract(Rgba32[], w, h, options)` → `PaletteResult`. Unit-testable, no `UnityEngine`. |
| `Editor/` | `Assembler.AssetGeneration.PaletteExtraction.Editor` | `PaletteExtractionWindow` (dev/tuning UI, **not** part of automation) + `CorpusGeneratorBatch` (regenerates the tuning corpus). |
| `Tests/` | `Assembler.AssetGeneration.PaletteExtraction.Tests` | Structural unit tests + the corpus count gate. |

## Algorithm

1. **Background detect** — per-channel median of the border pixels (robust to an object touching an edge).
2. **Mask** — a real alpha channel early-outs (`alpha < 128` ⇒ background); otherwise a tolerance
   flood-fill inward from the edges (Oklab band around the background colour). Flood-fill, not a global
   colour match, so an object may legitimately contain the background colour (e.g. the blue whale on a
   blue background) without being eaten.
3. **Erode** the object mask (`ErodePixels`) to drop the anti-alias / JPEG-ringing halo.
4. **Consolidate** the surviving object pixels into emergent fundamentals via `ColourModes.Consolidate`
   (frequency-weighted Oklab tolerance-merge, capped at `MaxColours`). The count is emergent — never a
   fixed K — so shading steps collapse while distinct materials stay apart.
5. **Spurious-cluster defence** — a cluster below `MinCoverage` is dropped **unless** its pixels fill at
   least `MinCompactness` of their bounding box. A compact blob (a small black eye) survives; a spray of
   JPEG/AA speckle scattered along colour edges has a huge bbox and is dropped, its pixels folded into the
   nearest surviving swatch.
6. **Order** by descending coverage.

The bias throughout is **over-segmentation**: under-segmentation is destructive (a whole material
vanishes), whereas an extra near-duplicate swatch is harmless.

## Tuned defaults

`PaletteExtractionOptions.Default` passes all 16 corpus images and sits in the centre of a broad passing
region (so it is robust to small JPEG-decode differences, not balanced on a knife-edge):

| Field | Value |
|---|---|
| `BackgroundTolerance` | 0.10 |
| `ErodePixels` | 2 |
| `MergeTolerance` | 0.135 |
| `MaxColours` | 12 |
| `MinCoverage` | 0.03 |
| `MinCompactness` | 0.40 |

## Tuning corpus (`TuningCorpus/`, `gemini-3-pro-image`)

Each image stresses a specific failure mode; the corpus test asserts each resolves to its expected count
of fundamentals (±1). `gemini-3-pro-image` returns **JPEG** (lossy); flat-shaded content compresses
cleanly and the erosion + coverage/compactness defences absorb the ringing.

| slug | fundamentals | stresses |
|---|---|---|
| turtle | 3–4 | same-hue two greens; tiny black eye; shading steps |
| fox | 3–4 | mid palette, small dark accents |
| snowman | 3–5 | **white body with shading** must stay one material |
| crate | 1–2 | strong face-shading must collapse to a single colour |
| tree | 2–3 | foliage shading steps → one green |
| ladybug | 2–3 | small black spots — coverage-vs-feature |
| penguin | 3–4 | tiny orange accents; neutral black/white |
| chicken | 3–4 | multiple small saturated accents |
| panda | 2–3 | pure neutrals, zero hue |
| robot | 3–4 | near-neutral body + one vivid accent |
| cat | 3–5 | low-chroma near-tolerance stripe separation |
| traffic-cone | 2–3 | high-contrast bands both survive |
| parrot | 6–8 | upper-bound count + max-cap |
| blue-whale | 3–4 | **object blue collides with bg blue** — flood-fill + erode |
| bee | 3–4 | high-freq stripes must not average to mud |
| gem | 1–3 | smooth-gradient shading (assumption-breaker; expected-hard) |

Regenerate with **Assembler → Palette Extraction → Generate Tuning Corpus** (or batch:
`CORPUS_MODEL=gemini-3-pro-image … -executeMethod …CorpusGeneratorBatch.Run`). Requires a Gemini key
saved via the Image Generation window.

## Usage

- **Tune interactively**: **Assembler → Palette Extraction → Extract Palette** — load an image, see the
  masked object, the swatch strip with coverage, and a live slider per option.
- **Pipeline**: `VoxelPipeline.RunAsync` extracts the palette from the accepted source image at stage 3
  and snaps the voxels to it automatically (falling back to the configured colour mode if the image can't
  be read). The standalone Mesh → Voxel window keeps its own colour modes.
- **Test the corpus gate**: `Tools/run-tests.sh Assembler.AssetGeneration.PaletteExtraction.Tests`.
