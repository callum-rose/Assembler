using System.Collections.Generic;
using Assembler.AssetGeneration.MeshToVoxels;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// The scored grid-placement search — rule 2 ("boundary repulsion") realised without deforming
    /// the mesh. A Candidate is a coarse voxel lattice laid over the fine grid: per-axis phase
    /// offsets (fine cells) × per-axis voxel-count scale flex (snap the model's extent toward whole
    /// voxel counts, stretch clamped ±10%). Every candidate is re-voted cheaply against the fine
    /// grid via the analysis' integral volumes and scored on face economy, occupancy IoU, and — at
    /// double weight — air-gap preservation; the winner is materialised as the coarse occupancy
    /// grid. Search off = the identity candidate (today's block-collapse behaviour); either way
    /// there is exactly one voting implementation. Deterministic throughout: no RNG, stable
    /// tie-breaks (highest score → smallest phase → scale nearest 1).
    ///
    /// Phase-2 fallback if features fight over phase (e.g. legs and neck wanting different
    /// alignments): warp the SDF sample positions themselves — future work, deliberately not built.
    /// </summary>
    public static class GridPlacementSearch
    {
        /// <summary>Scale flex never stretches the model by more than ±10% per axis.</summary>
        private const float MaxScaleFlex = 0.1f;

        /// <summary>One coarse-lattice placement: block <c>i</c> spans fine <c>[Start + i·Width, Start + (i+1)·Width)</c> per axis.</summary>
        public readonly struct Candidate
        {
            /// <summary>Per-axis lattice offset in fine cells, ∈ [0, factor).</summary>
            public Vector3Int Phase { get; init; }

            /// <summary>Coarse voxel count per axis.</summary>
            public Vector3Int Counts { get; init; }

            /// <summary>Block width in fine cells per axis (factor when unstretched).</summary>
            public Vector3 Width { get; init; }

            /// <summary>Lattice start in fine-cell space per axis (may be negative).</summary>
            public Vector3 Start { get; init; }

            /// <summary>Per-axis stretch (Width / factor); 1 = no stretch.</summary>
            public Vector3 Scale { get; init; }
        }

        /// <summary>All score terms ∈ [0,1], higher = better. <see cref="Col"/> is −1 when not evaluated.</summary>
        public readonly struct ScoreBreakdown
        {
            public float Total { get; init; }
            public float Face { get; init; }
            public float Iou { get; init; }
            public float Gap { get; init; }
            public float Col { get; init; }
        }

        /// <summary>A materialised (voted) candidate plus everything the cleanup and metrics stages need.</summary>
        public sealed class Placement
        {
            public Candidate Candidate { get; init; }
            public ScoreBreakdown Score { get; init; }
            public VoxelGrid Grid { get; init; } = null!;

            /// <summary>Coarse cells filled by the connectivity-gated thin-feature keep (morphology protection source).</summary>
            public bool[] ThinKept { get; init; } = null!;

            /// <summary>Fine air-gap fraction per coarse cell (the close guard).</summary>
            public float[] GapFraction { get; init; } = null!;

            /// <summary>Coarse cells whose fine support intersects the fine main component (floater-removal keep set).</summary>
            public bool[] TouchesMain { get; init; } = null!;

            public int CandidatesEvaluated { get; init; }
        }

        public readonly struct Options
        {
            /// <summary>Block occupied-fraction needed to fill a coarse voxel (unless thin-keep forces it).</summary>
            public float Coverage { get; init; }

            /// <summary>Force-keep sub-Nyquist blocks connected to the main component.</summary>
            public bool ThinFeatureKeep { get; init; }

            /// <summary>Enumerate floor/ceil voxel-count scale candidates, not just phases.</summary>
            public bool ScaleFlex { get; init; }

            public float FaceWeight { get; init; }
            public float IouWeight { get; init; }
            public float GapWeight { get; init; }
            public float ColWeight { get; init; }
        }

        /// <summary>A strong Oklab edge between adjacent fine surface cells, at fine boundary coordinate <see cref="Boundary"/> along <see cref="Axis"/>.</summary>
        public readonly struct ColourEdge
        {
            public int Axis { get; init; }
            public int Boundary { get; init; }
        }

        /// <summary>Today's behaviour: blocks of <c>factor</c> fine cells tiled from fine index 0.</summary>
        public static Candidate IdentityCandidate(FineGridAnalysis analysis)
        {
            int f = analysis.Factor;
            VoxelGrid fine = analysis.Fine;
            return new Candidate
            {
                Phase = Vector3Int.zero,
                Counts = new Vector3Int(CeilDiv(fine.NX, f), CeilDiv(fine.NY, f), CeilDiv(fine.NZ, f)),
                Width = new Vector3(f, f, f),
                Start = Vector3.zero,
                Scale = Vector3.one,
            };
        }

        /// <summary>
        /// Enumerate the candidate lattices: per-axis phases ∈ [0, factor) × per-axis floor/ceil
        /// voxel-count scales over the occupied bounding box (deduped, stretch clamped ±10%).
        /// ≤ (factor·2)³ candidates — ~512 at factor 4.
        /// </summary>
        public static List<Candidate> Enumerate(FineGridAnalysis analysis, bool scaleFlex)
        {
            var candidates = new List<Candidate>();
            if (analysis.IsEmpty)
            {
                candidates.Add(IdentityCandidate(analysis));
                return candidates;
            }

            int f = analysis.Factor;
            var extents = new int[3];
            for (int a = 0; a < 3; a++)
            {
                extents[a] = analysis.OccupiedMax[a] - analysis.OccupiedMin[a] + 1;
            }

            var axisWidths = new float[3][];
            for (int a = 0; a < 3; a++)
            {
                axisWidths[a] = WidthOptions(extents[a], f, scaleFlex);
            }

            foreach (float wx in axisWidths[0])
            {
                foreach (float wy in axisWidths[1])
                {
                    foreach (float wz in axisWidths[2])
                    {
                        for (int px = 0; px < f; px++)
                        {
                            for (int py = 0; py < f; py++)
                            {
                                for (int pz = 0; pz < f; pz++)
                                {
                                    candidates.Add(MakeCandidate(
                                        analysis, new Vector3Int(px, py, pz), new Vector3(wx, wy, wz)));
                                }
                            }
                        }
                    }
                }
            }
            return candidates;
        }

        /// <summary>Run the search: enumerate, vote, score; return the materialised winner.</summary>
        public static Placement Run(
            FineGridAnalysis analysis, Options options, IReadOnlyList<ColourEdge>? colourEdges = null)
        {
            List<Candidate> candidates = Enumerate(analysis, options.ScaleFlex);

            Placement? best = null;
            foreach (Candidate candidate in candidates)
            {
                Placement placement = Materialise(analysis, candidate, options, colourEdges);
                if (best is null || Beats(placement, best))
                {
                    best = placement;
                }
            }

            return new Placement
            {
                Candidate = best!.Candidate,
                Score = best.Score,
                Grid = best.Grid,
                ThinKept = best.ThinKept,
                GapFraction = best.GapFraction,
                TouchesMain = best.TouchesMain,
                CandidatesEvaluated = candidates.Count,
            };
        }

        /// <summary>
        /// Vote one candidate against the fine grid (the single voting implementation): per block,
        /// <c>thinKeep = occ &gt; 0 ∧ thick = 0 ∧ main &gt; 0</c> (sub-Nyquist AND connected — the
        /// connectivity gate that lets floaters die), <c>filled = thinKeep ∨ occ/vol ≥ coverage</c>.
        /// Block boundaries are rounded to whole fine cells (error ≤ ½ fine cell under scale flex).
        /// </summary>
        public static Placement Materialise(
            FineGridAnalysis analysis, Candidate candidate, Options options,
            IReadOnlyList<ColourEdge>? colourEdges = null)
        {
            VoxelGrid fine = analysis.Fine;
            int nx = candidate.Counts.x, ny = candidate.Counts.y, nz = candidate.Counts.z;

            var grid = new VoxelGrid(nx, ny, nz)
            {
                Origin = fine.Origin + new g3.Vector3d(candidate.Start.x, candidate.Start.y, candidate.Start.z) * fine.CellSize,
                CellSize = fine.CellSize * analysis.Factor,
            };
            var thinKept = new bool[grid.Occupied.Length];
            var gapFraction = new float[grid.Occupied.Length];
            var touchesMain = new bool[grid.Occupied.Length];

            int[] xB = Boundaries(candidate.Start.x, candidate.Width.x, nx);
            int[] yB = Boundaries(candidate.Start.y, candidate.Width.y, ny);
            int[] zB = Boundaries(candidate.Start.z, candidate.Width.z, nz);

            float coverage = Mathf.Clamp01(options.Coverage);
            long intersection = 0, fillOnly = 0, coveredGap = 0;

            for (int oz = 0; oz < nz; oz++)
            {
                for (int oy = 0; oy < ny; oy++)
                {
                    for (int ox = 0; ox < nx; ox++)
                    {
                        int x0 = xB[ox], x1 = xB[ox + 1];
                        int y0 = yB[oy], y1 = yB[oy + 1];
                        int z0 = zB[oz], z1 = zB[oz + 1];

                        int vol = ClippedVolume(x0, x1, fine.NX) * ClippedVolume(y0, y1, fine.NY) * ClippedVolume(z0, z1, fine.NZ);
                        if (vol <= 0)
                        {
                            continue;
                        }

                        int occ = analysis.OccupancyIntegral.BoxCount(x0, x1, y0, y1, z0, z1);
                        int mainC = analysis.MainIntegral.BoxCount(x0, x1, y0, y1, z0, z1);
                        int gapC = analysis.GapIntegral.BoxCount(x0, x1, y0, y1, z0, z1);

                        int i = grid.Index(ox, oy, oz);
                        gapFraction[i] = (float)gapC / vol;
                        touchesMain[i] = mainC > 0;

                        bool thinKeep = false;
                        if (options.ThinFeatureKeep && occ > 0 && mainC > 0)
                        {
                            int thick = analysis.ThickIntegral.BoxCount(x0, x1, y0, y1, z0, z1);
                            thinKeep = thick == 0;
                        }

                        bool filled = thinKeep || (occ > 0 && (float)occ / vol >= coverage);
                        if (!filled)
                        {
                            continue;
                        }

                        grid.Occupied[i] = true;
                        thinKept[i] = thinKeep;
                        intersection += occ;
                        fillOnly += vol - occ;
                        coveredGap += gapC;
                    }
                }
            }

            ScoreBreakdown score = Score(analysis, candidate, grid, intersection, fillOnly, coveredGap, options, colourEdges);
            return new Placement
            {
                Candidate = candidate,
                Score = score,
                Grid = grid,
                ThinKept = thinKept,
                GapFraction = gapFraction,
                TouchesMain = touchesMain,
                CandidatesEvaluated = 1,
            };
        }

        /// <summary>
        /// Strong Oklab edges between adjacent fine surface cells — the S_col plumbing. Costly
        /// (needs per-fine-voxel colours), so callers only build this when the colour weight is
        /// non-zero.
        /// </summary>
        public static List<ColourEdge> ExtractStrongColourEdges(
            VoxelGrid fine, Color32[] fineColours, float oklabThreshold)
        {
            var edges = new List<ColourEdge>();
            for (int z = 0; z < fine.NZ; z++)
            {
                for (int y = 0; y < fine.NY; y++)
                {
                    for (int x = 0; x < fine.NX; x++)
                    {
                        if (!IsSurface(fine, x, y, z))
                        {
                            continue;
                        }
                        OklabColor here = OklabColor.FromColor32(fineColours[fine.Index(x, y, z)]);

                        // +axis neighbours only, so each adjacent pair is visited once.
                        AddEdgeIfStrong(fine, fineColours, edges, here, x + 1, y, z, axis: 0, boundary: x + 1, oklabThreshold);
                        AddEdgeIfStrong(fine, fineColours, edges, here, x, y + 1, z, axis: 1, boundary: y + 1, oklabThreshold);
                        AddEdgeIfStrong(fine, fineColours, edges, here, x, y, z + 1, axis: 2, boundary: z + 1, oklabThreshold);
                    }
                }
            }
            return edges;
        }

        // ---- Internals -------------------------------------------------------

        private static Candidate MakeCandidate(FineGridAnalysis analysis, Vector3Int phase, Vector3 width)
        {
            int f = analysis.Factor;
            var start = new Vector3(
                analysis.OccupiedMin.x - phase.x,
                analysis.OccupiedMin.y - phase.y,
                analysis.OccupiedMin.z - phase.z);

            var counts = new Vector3Int(
                CountFor(analysis.OccupiedMax.x, start.x, width.x),
                CountFor(analysis.OccupiedMax.y, start.y, width.y),
                CountFor(analysis.OccupiedMax.z, start.z, width.z));

            return new Candidate
            {
                Phase = phase,
                Counts = counts,
                Width = width,
                Start = start,
                Scale = new Vector3(width.x / f, width.y / f, width.z / f),
            };
        }

        // Enough blocks that the last one covers the occupied max cell (fine cell max spans [max, max+1)).
        private static int CountFor(int occupiedMax, float start, float width) =>
            Mathf.Max(1, Mathf.CeilToInt((occupiedMax + 1 - start) / width));

        // Per-axis block widths: the floor/ceil voxel-count snaps over the occupied extent, deduped,
        // stretch clamped to ±10% of the unstretched factor. Deliberately NOT factor ∪ {floor, ceil}:
        // a third width option cubes the candidate count (1728 at factor 4) and the floor/ceil pair
        // brackets the unstretched width anyway (equalling it exactly when the extent divides evenly).
        private static float[] WidthOptions(int extent, int factor, bool scaleFlex)
        {
            if (!scaleFlex)
            {
                return new float[] { factor };
            }

            var widths = new List<float>();
            int nFloor = Mathf.Max(1, Mathf.FloorToInt((float)extent / factor));
            int nCeil = Mathf.Max(1, Mathf.CeilToInt((float)extent / factor));
            foreach (int n in nFloor == nCeil ? new[] { nFloor } : new[] { nFloor, nCeil })
            {
                float w = Mathf.Clamp((float)extent / n, factor * (1f - MaxScaleFlex), factor * (1f + MaxScaleFlex));
                if (!ContainsApprox(widths, w))
                {
                    widths.Add(w);
                }
            }
            return widths.ToArray();
        }

        private static bool ContainsApprox(List<float> values, float v)
        {
            foreach (float existing in values)
            {
                if (Mathf.Abs(existing - v) < 1e-4f)
                {
                    return true;
                }
            }
            return false;
        }

        // Block boundaries rounded to whole fine cells (round-half-up for determinism — Unity's
        // RoundToInt banker's-rounds and would tile ½-offsets unevenly).
        private static int[] Boundaries(float start, float width, int count)
        {
            var bounds = new int[count + 1];
            for (int i = 0; i <= count; i++)
            {
                bounds[i] = Mathf.FloorToInt(start + i * width + 0.5f);
            }
            return bounds;
        }

        private static int ClippedVolume(int lo, int hi, int max)
        {
            int a = Mathf.Max(0, lo);
            int b = Mathf.Min(max, hi);
            return Mathf.Max(0, b - a);
        }

        private static ScoreBreakdown Score(
            FineGridAnalysis analysis, Candidate candidate, VoxelGrid grid,
            long intersection, long fillOnly, long coveredGap,
            Options options, IReadOnlyList<ColourEdge>? colourEdges)
        {
            (int filled, int exposedFaces) = CountFilledAndFaces(grid);

            float sFace = filled == 0
                ? 0f
                : Mathf.Min(1f, 6f * Mathf.Pow(filled, 2f / 3f) / exposedFaces);

            long union = analysis.OccupancyIntegral.Total + fillOnly;
            float sIou = union > 0 ? (float)intersection / union : 0f;

            int totalGap = analysis.GapIntegral.Total;
            float sGap = totalGap > 0 ? 1f - (float)coveredGap / totalGap : 1f;

            bool colEvaluated = options.ColWeight > 0f && colourEdges is { Count: > 0 };
            float sCol = colEvaluated ? ColourBoundaryAlignment(candidate, colourEdges!) : -1f;

            float total = options.FaceWeight * sFace + options.IouWeight * sIou + options.GapWeight * sGap
                + (colEvaluated ? options.ColWeight * sCol : 0f);

            return new ScoreBreakdown { Total = total, Face = sFace, Iou = sIou, Gap = sGap, Col = sCol };
        }

        private static (int filled, int exposedFaces) CountFilledAndFaces(VoxelGrid grid)
        {
            int filled = 0, faces = 0;
            for (int z = 0; z < grid.NZ; z++)
            {
                for (int y = 0; y < grid.NY; y++)
                {
                    for (int x = 0; x < grid.NX; x++)
                    {
                        if (!grid.Occupied[grid.Index(x, y, z)])
                        {
                            continue;
                        }
                        filled++;
                        faces += ExposedFaces(grid, x, y, z);
                    }
                }
            }
            return (filled, faces);
        }

        private static int ExposedFaces(VoxelGrid grid, int x, int y, int z)
        {
            int n = 0;
            if (!grid.IsOccupied(x + 1, y, z)) { n++; }
            if (!grid.IsOccupied(x - 1, y, z)) { n++; }
            if (!grid.IsOccupied(x, y + 1, z)) { n++; }
            if (!grid.IsOccupied(x, y - 1, z)) { n++; }
            if (!grid.IsOccupied(x, y, z + 1)) { n++; }
            if (!grid.IsOccupied(x, y, z - 1)) { n++; }
            return n;
        }

        // Fraction of strong fine-surface colour edges landing on (within ½ fine cell of, i.e.
        // exactly on after rounding) a block boundary of this lattice.
        private static float ColourBoundaryAlignment(Candidate candidate, IReadOnlyList<ColourEdge> edges)
        {
            int aligned = 0;
            foreach (ColourEdge edge in edges)
            {
                float start = candidate.Start[edge.Axis];
                float width = candidate.Width[edge.Axis];
                int count = candidate.Counts[edge.Axis];

                int nearest = Mathf.Clamp(Mathf.RoundToInt((edge.Boundary - start) / width), 0, count);
                int boundary = Mathf.FloorToInt(start + nearest * width + 0.5f);
                if (boundary == edge.Boundary)
                {
                    aligned++;
                }
            }
            return (float)aligned / edges.Count;
        }

        // Ties: highest score → smallest |phase| → scale nearest 1 → first enumerated.
        private static bool Beats(Placement a, Placement b)
        {
            const float epsilon = 1e-6f;
            if (a.Score.Total > b.Score.Total + epsilon)
            {
                return true;
            }
            if (a.Score.Total < b.Score.Total - epsilon)
            {
                return false;
            }

            int phaseA = a.Candidate.Phase.x + a.Candidate.Phase.y + a.Candidate.Phase.z;
            int phaseB = b.Candidate.Phase.x + b.Candidate.Phase.y + b.Candidate.Phase.z;
            if (phaseA != phaseB)
            {
                return phaseA < phaseB;
            }

            float devA = ScaleDeviation(a.Candidate.Scale);
            float devB = ScaleDeviation(b.Candidate.Scale);
            return devA < devB - epsilon;
        }

        private static float ScaleDeviation(Vector3 scale) =>
            Mathf.Abs(scale.x - 1f) + Mathf.Abs(scale.y - 1f) + Mathf.Abs(scale.z - 1f);

        private static bool IsSurface(VoxelGrid grid, int x, int y, int z) =>
            grid.IsOccupied(x, y, z)
            && (!grid.IsOccupied(x + 1, y, z) || !grid.IsOccupied(x - 1, y, z)
                || !grid.IsOccupied(x, y + 1, z) || !grid.IsOccupied(x, y - 1, z)
                || !grid.IsOccupied(x, y, z + 1) || !grid.IsOccupied(x, y, z - 1));

        private static void AddEdgeIfStrong(
            VoxelGrid fine, Color32[] colours, List<ColourEdge> edges, OklabColor here,
            int nx, int ny, int nz, int axis, int boundary, float threshold)
        {
            if (!IsSurface(fine, nx, ny, nz))
            {
                return;
            }
            OklabColor there = OklabColor.FromColor32(colours[fine.Index(nx, ny, nz)]);
            if (here.DistanceTo(there) > threshold)
            {
                edges.Add(new ColourEdge { Axis = axis, Boundary = boundary });
            }
        }

        private static int CeilDiv(int a, int b) => (a + b - 1) / b;
    }
}
