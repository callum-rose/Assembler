using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// The objective per-run readout the locked test set is judged against: geometry counts
    /// (voxels, exposed faces, floaters removed), colour counts (distinct colours), the placement
    /// the search chose (phase, scale, candidate count) and its score terms. Computed on the FINAL
    /// grid — after cleanup — so the numbers describe what the user actually sees.
    /// </summary>
    public readonly struct SpikeMetrics
    {
        public int VoxelCount { get; init; }
        public int ExposedFaces { get; init; }
        public int FloatersRemoved { get; init; }
        public int DistinctColours { get; init; }
        public Vector3Int GridDims { get; init; }
        public Vector3Int FineDims { get; init; }
        public Vector3Int Phase { get; init; }
        public Vector3 Scale { get; init; }
        public int CandidatesEvaluated { get; init; }
        public float Score { get; init; }
        public float SFace { get; init; }
        public float SIou { get; init; }
        public float SGap { get; init; }

        /// <summary>−1 when the colour-alignment term wasn't evaluated (weight 0, the default).</summary>
        public float SCol { get; init; }

        public static SpikeMetrics Compute(
            VoxelGrid grid, Color32[] voxelColours, GridPlacementSearch.Placement placement,
            int floatersRemoved, Vector3Int fineDims)
        {
            int voxels = 0, faces = 0;
            var distinct = new HashSet<int>();
            for (int z = 0; z < grid.NZ; z++)
            {
                for (int y = 0; y < grid.NY; y++)
                {
                    for (int x = 0; x < grid.NX; x++)
                    {
                        int i = grid.Index(x, y, z);
                        if (!grid.Occupied[i])
                        {
                            continue;
                        }
                        voxels++;
                        Color32 c = voxelColours[i];
                        distinct.Add((c.r << 16) | (c.g << 8) | c.b);

                        if (!grid.IsOccupied(x + 1, y, z)) { faces++; }
                        if (!grid.IsOccupied(x - 1, y, z)) { faces++; }
                        if (!grid.IsOccupied(x, y + 1, z)) { faces++; }
                        if (!grid.IsOccupied(x, y - 1, z)) { faces++; }
                        if (!grid.IsOccupied(x, y, z + 1)) { faces++; }
                        if (!grid.IsOccupied(x, y, z - 1)) { faces++; }
                    }
                }
            }

            return new SpikeMetrics
            {
                VoxelCount = voxels,
                ExposedFaces = faces,
                FloatersRemoved = floatersRemoved,
                DistinctColours = distinct.Count,
                GridDims = new Vector3Int(grid.NX, grid.NY, grid.NZ),
                FineDims = fineDims,
                Phase = placement.Candidate.Phase,
                Scale = placement.Candidate.Scale,
                CandidatesEvaluated = placement.CandidatesEvaluated,
                Score = placement.Score.Total,
                SFace = placement.Score.Face,
                SIou = placement.Score.Iou,
                SGap = placement.Score.Gap,
                SCol = placement.Score.Col,
            };
        }

        public string ToLogString()
        {
            var sb = new StringBuilder();
            sb.Append($"voxels {VoxelCount:N0}, faces {ExposedFaces:N0}, floaters removed {FloatersRemoved}, ");
            sb.Append($"colours {DistinctColours}, grid {GridDims.x}×{GridDims.y}×{GridDims.z} ");
            sb.Append($"(fine {FineDims.x}×{FineDims.y}×{FineDims.z}), ");
            sb.Append($"phase ({Phase.x},{Phase.y},{Phase.z}), scale ({Scale.x:F3},{Scale.y:F3},{Scale.z:F3}), ");
            sb.Append($"score {Score:F3} (face {SFace:F3}, iou {SIou:F3}, gap {SGap:F3}");
            if (SCol >= 0f)
            {
                sb.Append($", col {SCol:F3}");
            }
            sb.Append($") over {CandidatesEvaluated} candidate(s)");
            return sb.ToString();
        }

        public const string CsvHeader =
            "name,voxels,faces,floatersRemoved,colours,gridX,gridY,gridZ,fineX,fineY,fineZ,"
            + "phaseX,phaseY,phaseZ,scaleX,scaleY,scaleZ,candidates,score,sFace,sIou,sGap,sCol";

        public string ToCsvRow(string name)
        {
            var inv = CultureInfo.InvariantCulture;
            return string.Join(",",
                Escape(name),
                VoxelCount.ToString(inv), ExposedFaces.ToString(inv), FloatersRemoved.ToString(inv),
                DistinctColours.ToString(inv),
                GridDims.x.ToString(inv), GridDims.y.ToString(inv), GridDims.z.ToString(inv),
                FineDims.x.ToString(inv), FineDims.y.ToString(inv), FineDims.z.ToString(inv),
                Phase.x.ToString(inv), Phase.y.ToString(inv), Phase.z.ToString(inv),
                Scale.x.ToString("F4", inv), Scale.y.ToString("F4", inv), Scale.z.ToString("F4", inv),
                CandidatesEvaluated.ToString(inv),
                Score.ToString("F4", inv), SFace.ToString("F4", inv), SIou.ToString("F4", inv),
                SGap.ToString("F4", inv), SCol.ToString("F4", inv));
        }

        private static string Escape(string name) =>
            name.Contains(",") ? "\"" + name.Replace("\"", "\"\"") + "\"" : name;
    }
}
