namespace Assembler.AssetGeneration.MeshToVoxel
{
    /// <summary>
    /// Boxiness helper: fill the concave corners and notches that make a blocky silhouette read
    /// ragged, colouring each fill the modal colour of its occupied face-neighbours so it stays
    /// on-palette. An empty voxel is filled when EITHER
    /// <list type="bullet">
    /// <item>three same-colour face-neighbours meet it at a shared vertex — a genuine concave
    /// corner (fill it that colour), OR</item>
    /// <item>it has ≥<paramref name="neighbourThreshold"/> occupied face-neighbours (a deep enough
    /// concavity to box out regardless of colour — fill it the modal colour).</item>
    /// </list>
    /// The <em>shared-vertex</em> constraint on the same-colour rule is what stops inappropriate
    /// fills: three same-colour neighbours count only if they lie on three <em>distinct</em> axes
    /// (one of ±X, one of ±Y, one of ±Z), so they touch a single corner of the empty cell. A
    /// straddle — e.g. +X, −X and +Y — spans only two axes and shares no vertex, so it is left
    /// alone rather than welding across a thin sheet or bridging a one-voxel gap. "Share a colour"
    /// is a perceptual match within <c>colourTolerance</c> (Oklab distance), not exact RGB equality
    /// — so near-identical shades (Raw-mode samples, near-duplicate palette entries) still count as
    /// one colour. Tolerance 0 is exact match. A corner where distinct colour regions meet has no
    /// single-colour cluster spanning all three axes, so it is left alone rather than filled with an
    /// arbitrary pick. With <c>requireMajority</c> the deep-concavity fill additionally needs the
    /// modal colour to be a strict majority of the occupied neighbours, so a pocket where two colour
    /// regions meet is left open rather than smeared with an arbitrary side. Cells flagged as real
    /// air gaps (fine gap fraction &gt; ¼) are protected from the corner fill so leg gaps and handle
    /// holes stay open — but NOT from the deep-concavity fill, since a cell walled in on that many
    /// sides is an enclosed pocket, not a see-through gap, and boxing it out is the whole point. Each
    /// pass reads a snapshot (order-independent) and passes repeat until one fills nothing, so a fill
    /// that creates a new qualifying corner is chased down (always terminates — each pass only adds
    /// cells to a finite grid). Mutates <paramref name="grid"/> and <paramref name="colours"/> in
    /// place; returns the total number filled.
    /// </summary>
    public static class CornerFill
    {
        /// <summary>Default face-neighbour count for the deep-concavity (any-colour) fill.</summary>
        public const int DefaultNeighbourThreshold = 5;

        // All three axis bits set: a colour cluster that spans ±X, ±Y and ±Z has one neighbour on
        // each axis, so those neighbours meet the empty cell at a shared vertex — a concave corner.
        private const int AllAxes = 0b111;

        // A cell whose fine support is more than a quarter air-gap is a real gap — never fill it.
        private const float GapFillLimit = 0.25f;

        // Ordered X±, Y±, Z± so neighbour index >> 1 gives the axis (0=X, 1=Y, 2=Z).
        private static readonly (int dx, int dy, int dz)[] FaceNeighbours =
        {
            (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1),
        };

        /// <summary>
        /// <paramref name="gapFraction"/> (per-cell, <see cref="VoxelGrid.Index"/> layout) may be
        /// null to skip the gap guard. <paramref name="colourTolerance"/> is the Oklab distance
        /// within which neighbour colours count as the same (0 = exact match).
        /// <paramref name="neighbourThreshold"/> is how many occupied face-neighbours trigger the
        /// any-colour deep-concavity fill. When <paramref name="requireMajority"/> is set, that fill
        /// only fires if the modal colour is a strict majority of the occupied neighbours.
        /// </summary>
        public static int Apply(
            VoxelGrid grid, Rgba32[] colours, float[]? gapFraction,
            float colourTolerance = 0f, int neighbourThreshold = DefaultNeighbourThreshold,
            bool requireMajority = true)
        {
            int total = 0;
            int filledThisPass;
            do
            {
                filledThisPass = SinglePass(grid, colours, gapFraction, colourTolerance, neighbourThreshold, requireMajority);
                total += filledThisPass;
            }
            while (filledThisPass > 0);
            return total;
        }

        private static int SinglePass(
            VoxelGrid grid, Rgba32[] colours, float[]? gapFraction, float colourTolerance,
            int neighbourThreshold, bool requireMajority)
        {
            var wasOccupied = (bool[])grid.Occupied.Clone();
            float toleranceSqr = colourTolerance * colourTolerance;

            // A voxel has ≤6 face-neighbours, so its colours group into at most 6 perceptual
            // clusters. Reuse these across cells to avoid per-cell allocation. clusterAxisMask
            // accumulates which axes (X=1, Y=2, Z=4) a cluster's neighbours occupy, so AllAxes
            // marks a cluster that forms a shared-vertex corner.
            var clusterLab = new OklabColor[6];
            var clusterColour = new Rgba32[6];
            var clusterCount = new int[6];
            var clusterAxisMask = new int[6];
            int filled = 0;

            for (int z = 0; z < grid.NZ; z++)
            {
                for (int y = 0; y < grid.NY; y++)
                {
                    for (int x = 0; x < grid.NX; x++)
                    {
                        int i = grid.Index(x, y, z);
                        if (wasOccupied[i])
                        {
                            continue;
                        }

                        int occupiedNeighbours = 0;
                        int clusters = 0;

                        for (int f = 0; f < FaceNeighbours.Length; f++)
                        {
                            (int dx, int dy, int dz) = FaceNeighbours[f];
                            int nx = x + dx, ny = y + dy, nz = z + dz;
                            if (!grid.InBounds(nx, ny, nz) || !wasOccupied[grid.Index(nx, ny, nz)])
                            {
                                continue;
                            }

                            occupiedNeighbours++;
                            int axisBit = 1 << (f >> 1); // 0,1→X ; 2,3→Y ; 4,5→Z
                            Rgba32 c = colours[grid.Index(nx, ny, nz)];
                            OklabColor lab = OklabColor.FromColor32(c);

                            // Add to the first cluster within tolerance, else seed a new one. The
                            // seed colour (first neighbour in scan order) is the cluster's fill colour.
                            int match = -1;
                            for (int k = 0; k < clusters; k++)
                            {
                                if (lab.SquaredDistanceTo(clusterLab[k]) <= toleranceSqr)
                                {
                                    match = k;
                                    break;
                                }
                            }
                            if (match >= 0)
                            {
                                clusterCount[match]++;
                                clusterAxisMask[match] |= axisBit;
                            }
                            else
                            {
                                clusterLab[clusters] = lab;
                                clusterColour[clusters] = c;
                                clusterCount[clusters] = 1;
                                clusterAxisMask[clusters] = axisBit;
                                clusters++;
                            }
                        }

                        // Modal cluster (for the deep-concavity fill) and the shared-vertex corner
                        // cluster (for the same-colour fill). A corner needs a same-colour cluster
                        // spanning all three axes; the largest such is picked, first-in-scan-order
                        // winning ties (strict >) so the result is deterministic.
                        int bestCount = 0;
                        Rgba32 modal = default;
                        int bestCornerCount = 0;
                        Rgba32 cornerColour = default;
                        bool cornerFound = false;
                        for (int k = 0; k < clusters; k++)
                        {
                            if (clusterCount[k] > bestCount)
                            {
                                bestCount = clusterCount[k];
                                modal = clusterColour[k];
                            }
                            if (clusterAxisMask[k] == AllAxes && clusterCount[k] > bestCornerCount)
                            {
                                bestCornerCount = clusterCount[k];
                                cornerColour = clusterColour[k];
                                cornerFound = true;
                            }
                        }

                        // A deep-enough pocket is walled in on all but a side or two — an enclosed
                        // pocket, not a see-through gap — so its any-colour fill (modal colour)
                        // overrides the air-gap guard. With requireMajority it only fires when the
                        // modal colour is a strict majority (2×count > occupied), so a pocket straddling
                        // two colour regions is left open rather than smeared. The shallower same-colour
                        // corner fill still respects the gap guard, since a 3-neighbour corner can sit on
                        // the edge of a real gap (leg gap, handle hole) we must keep.
                        bool gapProtected = gapFraction != null && gapFraction[i] > GapFillLimit;
                        bool deepConcavity = occupiedNeighbours >= neighbourThreshold
                            && (!requireMajority || bestCount * 2 > occupiedNeighbours);
                        if (deepConcavity)
                        {
                            grid.Occupied[i] = true;
                            colours[i] = modal;
                            filled++;
                        }
                        else if (cornerFound && !gapProtected)
                        {
                            grid.Occupied[i] = true;
                            colours[i] = cornerColour;
                            filled++;
                        }
                    }
                }
            }
            return filled;
        }
    }
}
