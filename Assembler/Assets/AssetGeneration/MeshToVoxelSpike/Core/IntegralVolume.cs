namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// 3D summed-volume table over a boolean mask: <see cref="BoxCount"/> answers "how many set
    /// cells in this axis-aligned box" with 8 array lookups, which is what lets the grid placement
    /// search re-vote hundreds of candidate lattices against the fine grid without re-scanning it.
    /// Uses the same flat index convention as <see cref="VoxelGrid.Index"/>.
    /// </summary>
    public sealed class IntegralVolume
    {
        private readonly int[] _sums; // (NX+1)·(NY+1)·(NZ+1), _sums[SumIndex(x,y,z)] = count in [0,x)×[0,y)×[0,z)
        private readonly int _nx;
        private readonly int _ny;
        private readonly int _nz;

        public int NX => _nx;
        public int NY => _ny;
        public int NZ => _nz;

        /// <summary>Total set cells over the whole mask.</summary>
        public int Total => _sums[SumIndex(_nx, _ny, _nz)];

        private IntegralVolume(int nx, int ny, int nz, int[] sums)
        {
            _nx = nx;
            _ny = ny;
            _nz = nz;
            _sums = sums;
        }

        /// <summary>Build the table over <paramref name="mask"/> with <see cref="VoxelGrid.Index"/> layout.</summary>
        public static IntegralVolume Build(bool[] mask, int nx, int ny, int nz)
        {
            var sums = new int[(nx + 1) * (ny + 1) * (nz + 1)];
            var result = new IntegralVolume(nx, ny, nz, sums);

            for (int z = 0; z < nz; z++)
            {
                for (int y = 0; y < ny; y++)
                {
                    int rowRun = 0;
                    for (int x = 0; x < nx; x++)
                    {
                        if (mask[x + nx * (y + ny * z)])
                        {
                            rowRun++;
                        }
                        // sum(x+1,y+1,z+1) = rowRun + sum(x+1,y,z+1) + sum(x+1,y+1,z) − sum(x+1,y,z)
                        sums[result.SumIndex(x + 1, y + 1, z + 1)] =
                            rowRun
                            + sums[result.SumIndex(x + 1, y, z + 1)]
                            + sums[result.SumIndex(x + 1, y + 1, z)]
                            - sums[result.SumIndex(x + 1, y, z)];
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Set-cell count in the half-open box <c>[x0,x1)×[y0,y1)×[z0,z1)</c>. Bounds are clamped to
        /// the grid, so callers can pass lattice blocks that overhang the mask.
        /// </summary>
        public int BoxCount(int x0, int x1, int y0, int y1, int z0, int z1)
        {
            x0 = Clamp(x0, _nx);
            x1 = Clamp(x1, _nx);
            y0 = Clamp(y0, _ny);
            y1 = Clamp(y1, _ny);
            z0 = Clamp(z0, _nz);
            z1 = Clamp(z1, _nz);
            if (x1 <= x0 || y1 <= y0 || z1 <= z0)
            {
                return 0;
            }

            return _sums[SumIndex(x1, y1, z1)]
                - _sums[SumIndex(x0, y1, z1)]
                - _sums[SumIndex(x1, y0, z1)]
                - _sums[SumIndex(x1, y1, z0)]
                + _sums[SumIndex(x0, y0, z1)]
                + _sums[SumIndex(x0, y1, z0)]
                + _sums[SumIndex(x1, y0, z0)]
                - _sums[SumIndex(x0, y0, z0)];
        }

        private int SumIndex(int x, int y, int z) => x + (_nx + 1) * (y + (_ny + 1) * z);

        private static int Clamp(int v, int max) => v < 0 ? 0 : v > max ? max : v;
    }
}
