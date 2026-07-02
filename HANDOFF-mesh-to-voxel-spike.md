# Handoff — Mesh → Stylised Voxel Spike

Continuation notes for picking this up in a fresh (local) Claude Code chat. Everything below
reflects the state of branch **`claude/new-feature-impl-t6pw16`** (PR **#413**).

## What this is

A new, **Editor-only, isolated** mesh→voxel pipeline that turns a messy Meshy mesh into a
**Crossy-Road-style blocky voxel model** (chunky, flat-shaded, limited palette), plus a smooth
comparison mesh. The existing `Window > Voxels > Mesh to Voxels` path is untouched — this is a
side-by-side A/B experiment.

Everything lives under `Assembler/Assets/AssetGeneration/MeshToVoxelSpike/` in a new assembly
`Assembler.AssetGeneration.MeshToVoxelSpike` that references the existing
`Assembler.AssetGeneration.MeshToVoxels` assembly (for the OBJ/FBX importer, `TextureSnapshot`,
`OklabColor`, `DefaultMasterPalette`, `VoxMasterPalette`) and the auto-referenced **geometry3Sharp
(`g3`)** DLL.

Menu to run it: **`Window > Voxels > Mesh to Voxel Spike`**.

## The core idea (plain terms)

1. **Shape** — sample a signed distance field / winding number onto a **coarse** grid → clean blocky
   silhouette that ignores interior junk. Coarse = the stylised chunky look.
2. **Colour** — for each voxel/vertex, find the **nearest point on the original mesh → its UV →
   texture colour**, then flatten to a small palette. (Not the old "average texels into a voxel"
   approach.)
3. Show **every intermediate stage** laid out in the scene for judgement.

## Files (all under `Assembler/Assets/AssetGeneration/MeshToVoxelSpike/`)

Pipeline (UI-free):
- `VoxelGrid.cs` — dense boolean occupancy grid + world placement (origin, cell size). `Index`,
  `InBounds`, `Center(x,y,z)`, `OccupiedCount`.
- `SdfIsosurface.cs` — builds the g3 SDF + marching-cubes isosurface + occupancy grid. **Key detail
  below.**
- `FeatureAwareDownsample.cs` — optional: voxelise finer, collapse `factor³` blocks by coverage
  vote, force-keep thin features via a bounded-erosion thickness map (geometric only).
- `TaubinSmoother.cs` — volume-preserving λ/μ umbrella smoothing of the isosurface (returns a copy).
- `SurfaceReprojection.cs` — optional Newton reseat of smoothed verts onto iso=0 along the SDF
  gradient.
- `ColourReprojector.cs` — nearest-triangle → barycentric UV → `TextureSnapshot.SampleBilinear`,
  per smooth vertex and per blocky voxel; optional wrong-side reject (off by default).
- `ColourModes.cs` — Raw / PerModelPalette (deterministic k-means in Oklab) / MasterPalette snap
  (reuses `OklabColor` + `PaletteSnap`'s chroma-gain penalty).
- `BlockyVoxelMesher.cs` — face-culled flat-colour cube mesh (the primary output).
- `G3MeshConversion.cs` — g3 `DMesh3` ↔ Unity `Mesh` (carries `Color32[]`), + original-mesh preview
  builder.
- `SpikeSettings.cs`, `SpikeStageResult.cs` — settings bundle + result bundle.
- `SpikePipeline.cs` — orchestrates the whole thing (synchronous, main-thread; builds Unity meshes).
- `IsExternalInit.cs` — shim so `init` setters compile in this Unity/netstandard target (one per
  assembly; the one in `MeshToVoxels` is internal to that assembly and not visible here). **Added by
  the user locally — keep it.**

Editor / presentation:
- `MeshToVoxelSpikeWindow.cs` — `EditorWindow`, file picker, `EditorPrefs` persistence, guarded
  `async void` convert. All shape/colour options as live toggles.
- `SpikeStagePreviewer.cs` — instantiates each stage as a GameObject laid out along +X with labels
  under a parent `"MeshToVoxelSpike Preview"`; clears the prior preview each run.
- `VertexColorUnlit.shader` — tiny URP unlit, **`Cull Off`**, vertex-colour × `_BaseColor`. All
  previews use this one material (grey intermediates tint via `_BaseColor`), so winding/culling
  never matters.

Tests:
- `Tests/SpikePipelineTests.cs` (+ asmdef, csc.rsp) — light EditMode checks: unit-cube occupancy /
  non-empty isosurface / per-voxel reprojected colour, and colour-mode logic.

## IMPORTANT: geometry3Sharp API pitfalls already hit (and how)

This was written in a cloud environment **with no Unity and no .NET/mono**, so it could not be
compiled there. Several g3 API guesses were wrong and were fixed reactively from the user's compile
errors. **When diagnosing further g3 errors, do NOT trust the general/public g3 docs — this exact
DLL build differs. Verify against the actual DLL and the existing working usage in
`Assembler/Assets/AssetGeneration/MeshToVoxels/ObjToVoxConverter.cs`.**

The g3 DLL is at:
`Assembler/Packages/nuget-packages/InstalledPackages/geometry3Sharp.1.0.324/lib/netstandard2.0/geometry3Sharp.dll`
(You can't easily reflect over it without a .NET runtime; grepping strings is misleading because the
metadata #Strings heap shares string *suffixes* — e.g. `DenseGridTrilinearImplicit` doesn't appear
as its own token because it's a suffix of `CachingDenseGridTrilinearImplicit`.)

Fixes made so far (all now on the branch):
1. **`MeshSignedDistanceGrid.InsideModes` in this build has only `CrossingCount` and `ParityCount`**
   — NO `WindingNumber` and NO `AnalyticWindingNumber`. (Earlier commits tried both and failed.)
   Current code uses `ParityCount` for the signed grid.
2. **Use `DenseGridTrilinearImplicit(DenseGrid3f, Vector3d, double)`**, NOT
   `CachingDenseGridTrilinearImplicit` (whose ctor is `(Vector3d, double, Vector3i)`).
3. Because there's no winding-number inside-mode, **occupancy is signed by
   `DMeshAABBTree3.FastWindingNumber(Vector3d)` at each SDF grid node** (`|wn| > 0.5`, matching
   `ObjToVoxConverter`), independent of the SDF's parity sign. `SdfIsosurface.Build(mesh, tree,
   maxDimVoxels)` now takes the built AABB tree. This keeps the primary blocky output robust to
   non-watertight / self-intersecting / inverted meshes; the parity SDF only feeds the smooth
   comparison mesh + reprojection gradient.

APIs confirmed working (Unity only ever flagged the three issues above, so the rest compiled):
`MeshSignedDistanceGrid` fields `ComputeSigns / InsideMode / ComputeMode / UseParallel`, method
`Compute()`, props `Grid` (`DenseGrid3f` with public `ni/nj/nk` + `[x,y,z]` indexer), `GridOrigin`
(`Vector3f`), `CellSize`; `ComputeModes.NarrowBand_SpatialFloodFill`; `MarchingCubes` fields
`Implicit / Bounds / CubeSize`, `Generate()`, `Mesh`; `DenseGridTrilinearImplicit.Value(ref)/
Gradient(ref)/Bounds()`; `AxisAlignedBox3d.MaxDim/.Expand(double)`; `DMesh3` iteration/accessors;
`DMeshAABBTree3(mesh).Build()/.FindNearestTriangle(Vector3d)/.FastWindingNumber(Vector3d)`;
`DistPoint3Triangle3(p, Triangle3d).GetSquared()/.TriangleBaryCoords`; `Vector3d.Zero`,
`.LengthSquared`, operators (no `.Dot()/.Cross()` — those are done with explicit components).

If a NEW g3 error appears: check whether the same call already works in `ObjToVoxConverter.cs`
(that file is the ground truth for what this DLL supports), and prefer those exact patterns.

## Current status

- All known compile errors fixed; last pushed commit is the FastWindingNumber occupancy change.
- **Not yet compiled or run successfully by anyone** — the user is iterating locally in Unity.
- No `.meta` files are hand-authored (Unity generates them). Don't create `.meta` files.

## How to continue / verify (locally, in Unity)

1. `git pull` on branch `claude/new-feature-impl-t6pw16`.
2. Let Unity import + compile. If errors, see the g3 pitfalls section — paste the `error CS####`
   lines with file:line.
3. Optional headless checks (each boots Unity in batch mode, slow): `Assembler/Tools/check-compile.sh`,
   `Assembler/Tools/run-tests.sh`.
4. Real test: `Window > Voxels > Mesh to Voxel Spike` → pick a textured Meshy `.obj`/`.fbx` →
   Convert with "Reveal intermediates" on → judge the blocky output on (a) does it capture the
   shape essence / read as clean Crossy-Road, (b) do the reprojected colours beat the old
   `Mesh to Voxels` output.

## Things to watch once it compiles (runtime, not compile)

- **Occupancy sign**: uses `|wn| > 0.5`. If the blocky model comes out hollow or inside-out on some
  mesh, the winding-number threshold or the tree is the place to look.
- **SDF compute mode**: `NarrowBand_SpatialFloodFill` (fast). Only affects the smooth comparison
  mesh now, not the blocky output.
- **Colour orientation**: if reprojected colours look vertically mirrored, flip UV `v` → `1 - v` in
  `ColourReprojector.Sample` (same caveat as the existing converter).
- **Coordinate frame**: g3 is right-handed; meshes go into Unity as-is (no Z-flip) and everything
  renders `Cull Off`, so a global Z-mirror vs "reality" is possible but consistent across all
  stages and irrelevant for the A/B preview. Winding/normals don't affect the unlit shader.

## Non-goals (deferred)

No `.vox` export (output is Unity meshes only). Surface Nets / dual contouring, xatlas atlas baking,
batch/CLI mode all deferred until the approach is validated.
