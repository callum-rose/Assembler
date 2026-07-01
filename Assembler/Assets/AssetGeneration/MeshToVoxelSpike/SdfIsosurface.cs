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
            public g3.CachingDenseGridTrilinearImplicit Field { get; init; }

            /// <summary>Boolean occupancy (<c>sign &lt; 0</c>) at the SDF grid resolution.</summary>
            public VoxelGrid Occupancy { get; init; }

            public double CellSize { get; init; }
        }

        /// <summary>
        /// Build the SDF, isosurface and occupancy grid at <paramref name="maxDimVoxels"/> voxels
        /// along the longest bounding-box axis.
        /// </summary>
        public static Result Build(g3.DMesh3 mesh, int maxDimVoxels)
        {
            g3.AxisAlignedBox3d bounds = mesh.GetBounds();
            double maxDim = bounds.MaxDim;
            if (maxDim <= 0)
            {
                throw new InvalidDataException("Mesh has zero extent; nothing to voxelize.");
            }

            double cellSize = maxDim / Mathf.Max(1, maxDimVoxels);

            // Generalized winding number as the inside test: robust to the topological garbage
            // (non-manifold edges, flipped normals, interior ghosts, floaters) typical of Meshy
            // output. Narrow-band exact distances + a full-grid sign flood-fill give correct
            // occupancy everywhere and a clean field around the surface for marching cubes.
            var sdf = new g3.MeshSignedDistanceGrid(mesh, cellSize)
            {
                ComputeSigns = true,
                InsideMode = g3.MeshSignedDistanceGrid.InsideModes.AnalyticWindingNumber,
                ComputeMode = g3.MeshSignedDistanceGrid.ComputeModes.NarrowBand_SpatialFloodFill,
                UseParallel = true,
            };
            sdf.Compute();

            var origin = new g3.Vector3d(sdf.GridOrigin.x, sdf.GridOrigin.y, sdf.GridOrigin.z);
            var field = new g3.CachingDenseGridTrilinearImplicit(sdf.Grid, origin, sdf.CellSize);

            g3.DMesh3 iso = MarchingCubes(field, cellSize);
            VoxelGrid occupancy = BuildOccupancy(sdf, origin);

            return new Result
            {
                Iso = iso,
                Field = field,
                Occupancy = occupancy,
                CellSize = cellSize,
            };
        }

        private static g3.DMesh3 MarchingCubes(g3.CachingDenseGridTrilinearImplicit field, double cellSize)
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

        private static VoxelGrid BuildOccupancy(g3.MeshSignedDistanceGrid sdf, g3.Vector3d origin)
        {
            // Read dimensions straight off the distance grid so the loop bounds and the indexer below
            // can never disagree (nodes = cells + 1 padding included).
            g3.DenseGrid3f dense = sdf.Grid;
            var grid = new VoxelGrid(dense.ni, dense.nj, dense.nk)
            {
                Origin = origin,
                CellSize = sdf.CellSize,
            };

            for (int z = 0; z < dense.nk; z++)
            {
                for (int y = 0; y < dense.nj; y++)
                {
                    for (int x = 0; x < dense.ni; x++)
                    {
                        grid.Occupied[grid.Index(x, y, z)] = dense[x, y, z] < 0f;
                    }
                }
            }

            return grid;
        }
    }
}
