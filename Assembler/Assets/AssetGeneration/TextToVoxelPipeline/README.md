# Text → Voxels (pipeline)

Chains the three asset-generation stages end to end so a single text prompt
produces a MagicaVoxel `.vox`:

```
prompt ──▶ image ──▶ mesh ──▶ voxels
        (1)       (2)       (3)
   ImageGenerationCore  MeshyConversionCore  MeshVoxeliser
```

Each stage drives the *existing* core of its module — nothing is reimplemented —
so this pipeline and the standalone windows take an identical path. Lives in its
own folder and assembly (`Assembler.AssetGeneration.TextToVoxelPipeline.Editor`), which
references the three stage assemblies.

## Two entry points

- **Headless core** — [`VoxelPipeline.RunAsync`](Editor/VoxelPipeline.cs) is a
  UI-free `static` method: hand it a `VoxelPipelineSettings`, get back a `Result`
  bundling all three stages' outputs. No EditorPrefs / window state.
- **Editor window** — **Assembler → Text to Voxels (pipeline)** drives the same
  core with a GUI (image preview, progress, persisted inputs).

## Reviewable gaps between stages

`RunAsync` takes two optional *review gates* — `reviewImage` and `reviewMesh` —
awaited after stages 1 and 2. A headless caller omits them and the pipeline runs
straight through; the window passes gates that **pause** the run so you can check
the intermediate before paying for the next stage:

- tick **Review image before meshing** → the run stops after the image is
  written, shows a preview, and waits for **Continue**, **Retry**, or **Cancel**.
- tick **Review mesh before voxelizing** → it stops after the mesh downloads,
  shows the path (with a *Select in Project* button), and waits for the same.

Each gate returns a `ReviewDecision`: **Continue** accepts the output and moves
on; **Retry** discards it and re-runs that same stage (overwriting the
shared-base-name file), looping until you Continue or Cancel. **Cancel** aborts
the whole pipeline by cancelling the token (the window's **Cancel** button does
this), which surfaces as `OperationCanceledException`.

## Usage

1. Get a **Gemini** key (<https://aistudio.google.com/apikey>) and a **Meshy**
   key (<https://app.meshy.ai> → Settings → API).
2. Open **Assembler → Text to Voxels (pipeline)**, paste both keys, **Save**.
3. Type a **Prompt**, pick the mesh/voxel options, set an **Output Directory**.
4. (Optional) tick the review gates, then **Run pipeline**.

All three files share one base name in the output directory
(`<base>.png` → `<base>.obj` + maps → `<base>.vox`); a blank base name is slugged
from the prompt. If the directory is under `Assets/`, each stage refreshes the
project so Unity imports the result.

## Notes

- Stage 3 (voxelization) mirrors the standalone **Mesh → Voxel Spike** window's
  full control set and runs **synchronously on the main thread** (mesh import +
  texture decode need the editor, and the engine-free `MeshVoxeliser` is called
  straight through), so the editor blocks while it runs; a progress bar is shown.
  Keep **Max dimension** / **Fine factor** modest to keep it quick. Because it
  blocks the main thread, `VoxelPipeline.RunAsync` must be called from it.
- The AI Model Config layer (**Window → Voxels → AI Model Config**) emits a JSON
  config that fully configures this pipeline — paste it into the **Import AI
  config** box. It targets the same `Settings` the stage-3 controls expose.
- Per-stage caveats (texture wiring, API retries, palette snapping) are inherited
  from each stage — see their READMEs.
