using System.IO;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Stage 1 — launder a messy mesh into a clean scalar field and pull a manifold isosurface out
    /// of it. Builds a generalized-winding-number <see cref="g3.MeshSignedDistanceGrid"/> (robust to
    /// non-watertight / self-intersecting / inverted Meshy geometry), wraps it in a trilinear
    /// implicit, and runs g3 <see cref="g3.MarchingCubes"/> at iso 0 for the smooth shape-capture
    /// intermediate. The same signed grid yields the boolean occupancy grid the blocky mesher and
    /// feature-aware downsampler consume, and the implicit is kept for optional SDF reprojection.
    /// </summary>
    public static class SdfIsosurface
    {
        public readonly struct Result
        {
            /// <summary>Raw marching-cubes isosurface (before any smoothing).</summary>
            public g3.DMesh3 Iso { get; init; }

            /// <summary>Trilinear-interpolated SDF, for marching cubes and gradient reprojection.</summary>
            public g3.DenseGridTrilinearImplicit Field { get; init; }

            /// <summary>Boolean occupancy (<c>sign &lt; 0</c>) at the SDF grid resolution.</summary>
            public VoxelGrid Occupancy { get; init; }

            public double CellSize { get; init; }
        }

        /// <summary>
        /// Build the SDF, isosurface and occupancy grid at <paramref name="maxDimVoxels"/> voxels
        /// along the longest bounding-box axis. <paramref name="tree"/> (a built
        /// <see cref="g3.DMeshAABBTree3"/> over the same mesh) supplies the fast-winding-number
        /// occupancy test.
        /// </summary>
        public static Result Build(g3.DMesh3 mesh, g3.DMeshAABBTree3 tree, int maxDimVoxels)
        {
            g3.AxisAlignedBox3d bounds = mesh.GetBounds();
            double maxDim = bounds.MaxDim;
            if (maxDim <= 0)
            {
                throw new InvalidDataException("Mesh has zero extent; nothing to voxelize.");
            }

            double cellSize = maxDim / Mathf.Max(1, maxDimVoxels);

            // This g3 build's MeshSignedDistanceGrid only offers crossing/parity inside tests (no
            // winding-number mode), which are unreliable on messy Meshy topology. So the signed grid
            // — used only for the smooth marching-cubes comparison mesh and the reprojection gradient
            // — uses parity, while the occupancy grid that drives the primary blocky output is signed
            // by the AABB tree's fast winding number instead (robust to non-watertight / self-
            // intersecting / inverted geometry; same test the existing ObjToVoxConverter uses).
            var sdf = new g3.MeshSignedDistanceGrid(mesh, cellSize)
            {
                ComputeSigns = true,
                InsideMode = g3.MeshSignedDistanceGrid.InsideModes.ParityCount,
                ComputeMode = g3.MeshSignedDistanceGrid.ComputeModes.NarrowBand_SpatialFloodFill,
                UseParallel = true,
            };
            sdf.Compute();

            var origin = new g3.Vector3d(sdf.GridOrigin.x, sdf.GridOrigin.y, sdf.GridOrigin.z);
            var field = new g3.DenseGridTrilinearImplicit(sdf.Grid, origin, sdf.CellSize);

            g3.DMesh3 iso = MarchingCubes(field, cellSize);
            VoxelGrid occupancy = BuildOccupancy(sdf.Grid, origin, sdf.CellSize, tree);

            return new Result
            {
                Iso = iso,
                Field = field,
                Occupancy = occupancy,
                CellSize = cellSize,
            };
        }

        private static g3.DMesh3 MarchingCubes(g3.DenseGridTrilinearImplicit field, double cellSize)
        {
            g3.AxisAlignedBox3d bounds = field.Bounds();
            // Pad by a cell so a surface grazing the grid edge still closes into a watertight cap.
            bounds.Expand(cellSize);

            // g3's MarchingCubes defaults to IsoValue 0 — exactly the surface we want.
            var mc = new g3.MarchingCubes
            {
                Implicit = field,
                Bounds = bounds,
                CubeSize = cellSize,
            };
            mc.Generate();
            return mc.Mesh;
        }

        // Occupancy is signed by the fast winding number at each SDF grid node, not by the parity
        // sign of the distance field — so the primary blocky output stays robust on garbage meshes.
        // Dimensions/origin come straight off the distance grid so the blocky model shares the exact
        // frame of the marching-cubes mesh. |wn| > 0.5 (rather than wn > 0.5) keeps an inverted /
        // inside-out mesh reading as solid, matching ObjToVoxConverter.
        private static VoxelGrid BuildOccupancy(
            g3.DenseGrid3f dense, g3.Vector3d origin, double cellSize, g3.DMeshAABBTree3 tree)
        {
            var grid = new VoxelGrid(dense.ni, dense.nj, dense.nk)
            {
                Origin = origin,
                CellSize = cellSize,
            };

            for (int z = 0; z < dense.nk; z++)
            {
                for (int y = 0; y < dense.nj; y++)
                {
                    for (int x = 0; x < dense.ni; x++)
                    {
                        var p = origin + new g3.Vector3d(x, y, z) * cellSize;
                        double wn = tree.FastWindingNumber(p);
                        grid.Occupied[grid.Index(x, y, z)] = wn > 0.5 || wn < -0.5;
                    }
                }
            }

            return grid;
        }
    }
}
