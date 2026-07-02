using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// A dense boolean occupancy grid derived from the signed distance field (cell inside ⇔
    /// <c>sign &lt; 0</c>), tagged with the world-space placement it was sampled at. Lives in the
    /// mesh's own (g3, right-handed) coordinate frame: node <c>(x,y,z)</c> sits at world
    /// <c>Origin + (x,y,z)·CellSize</c>, matching where the SDF grid nodes — and therefore the
    /// marching-cubes isosurface — live, so the blocky voxel model and the smooth remesh overlay
    /// in one frame.
    /// </summary>
    public sealed class VoxelGrid
    {
        public int NX { get; }
        public int NY { get; }
        public int NZ { get; }

        /// <summary>Occupancy, indexed by <see cref="Index"/>. <c>true</c> = inside the SDF.</summary>
        public bool[] Occupied { get; }

        /// <summary>World position of grid node <c>(0,0,0)</c> (the SDF grid origin).</summary>
        public g3.Vector3d Origin { get; init; }

        /// <summary>Edge length of one voxel in world units (the SDF cell size).</summary>
        public double CellSize { get; init; }

        public VoxelGrid(int nx, int ny, int nz)
        {
            NX = Mathf.Max(0, nx);
            NY = Mathf.Max(0, ny);
            NZ = Mathf.Max(0, nz);
            Occupied = new bool[NX * NY * NZ];
        }

        /// <summary>Flat array index for a grid coordinate. No bounds checking — gate with <see cref="InBounds"/>.</summary>
        public int Index(int x, int y, int z) => x + NX * (y + NY * z);

        public bool InBounds(int x, int y, int z) =>
            x >= 0 && x < NX && y >= 0 && y < NY && z >= 0 && z < NZ;

        public bool IsOccupied(int x, int y, int z) =>
            InBounds(x, y, z) && Occupied[Index(x, y, z)];

        /// <summary>World-space centre of the voxel at grid coordinate <c>(x,y,z)</c>.</summary>
        public g3.Vector3d Center(int x, int y, int z) =>
            Origin + new g3.Vector3d(x, y, z) * CellSize;

        public int OccupiedCount
        {
            get
            {
                int n = 0;
                foreach (bool b in Occupied)
                {
                    if (b)
                    {
                        n++;
                    }
                }
                return n;
            }
        }
    }
}
