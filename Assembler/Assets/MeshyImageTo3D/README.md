# Meshy Image → 3D (spike)

A small, self-contained editor tool that calls the [Meshy.ai](https://meshy.ai)
**Image to 3D** OpenAPI endpoint: feed it a reference image, get a textured
model (OBJ or FBX) back on disk.

This is independent of the bundled `Assets/Plugins/ai.meshy` "Bridge" plugin
(which pushes models *from* the website). Everything here lives in its own
folder and assembly (`Assembler.MeshyImageTo3D.Editor`).

## Usage

1. Get an API key from <https://app.meshy.ai> (Settings → API).
2. Open **Assembler → Meshy Image → 3D**.
3. Paste the API key and click **Save** (stored in `EditorPrefs`).
4. Pick a **Reference Image** (png/jpg/webp) and an **Output Model Path**.
5. Choose **OBJ** or **FBX**, toggle PBR/remesh, then **Generate**.

The job is submitted, polled until it finishes (progress shown in the window),
then downloaded. If the output path is under `Assets/`, the project is
refreshed so Unity imports it.

## What gets written

Next to the chosen output file (`model.obj` → `MeshyOutput/`):

- the model itself (`model.obj` / `model.fbx`)
- for OBJ, the material library (`model.mtl`)
- texture maps: `model_basecolor.*`, and with PBR on,
  `model_metallic.*` / `model_roughness.*` / `model_normal.*`

## Spike caveats

- Texturing is always requested (`should_texture`), so the model comes back
  textured. The maps are downloaded as separate files — wiring them onto the
  imported material may need a manual pass (OBJ/MTL relative paths in
  particular). If you want a single self-contained file with embedded textures,
  add GLB as an output option in `MeshyApiClient` (`model_urls.glb`).
- No retry/backoff beyond the simple poll loop; one image per run.
