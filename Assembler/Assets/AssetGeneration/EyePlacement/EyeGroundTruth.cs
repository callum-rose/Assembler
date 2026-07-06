using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// One human-authored acceptable eye region, in the model's own <c>.vox</c> grid space (the same
    /// integer space as <see cref="Assembler.Voxels.VoxelModel.Voxels"/>, Z-up). <see cref="Center"/>
    /// is a voxel index at the centre of the region and <see cref="RadiusVoxels"/> the acceptance
    /// radius around it (0 defers to the scorer's default tolerance). A resolved
    /// <see cref="EyeAnchor"/> counts as reaching this eye when it lands inside that ball on a real
    /// surface voxel facing outward — see <see cref="EyePlacementScorer"/>.
    /// </summary>
    public sealed record GroundTruthEye(Vector3 Center, float RadiusVoxels = 0f);

    /// <summary>
    /// 3D ground truth for one corpus model: the set of acceptable eye regions a correct placement
    /// must reach, authored by a human inspecting the model's orbit renders (issue #479). This is the
    /// only thing eye placement is judged against — never a 2D pick-in-region hit-rate, which read
    /// 87% when true 3D placement was ~0%. Stored as a small JSON sidecar (<c>&lt;name&gt;.eyes.json</c>)
    /// next to the (untracked) <c>.vox</c>:
    /// <code>
    /// {
    ///   "name": "spotted_cow",
    ///   "note": "authored 2026-07-06 from the 8-yaw ring",
    ///   "eyes": [
    ///     { "center": { "x": 12, "y": 5,  "z": 18 }, "radiusVoxels": 2 },
    ///     { "center": { "x": 12, "y": 15, "z": 18 }, "radiusVoxels": 2 }
    ///   ]
    /// }
    /// </code>
    /// </summary>
    public sealed record EyeGroundTruth(string Name, IReadOnlyList<GroundTruthEye> Eyes, string Note = "")
    {
        /// <summary>Parses the JSON sidecar. Throws <see cref="ArgumentException"/> on malformed or empty input.</summary>
        public static EyeGroundTruth FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Ground-truth JSON is empty.");
            }

            Dto dto = JsonUtility.FromJson<Dto>(json)
                ?? throw new ArgumentException("Ground-truth JSON could not be parsed.");
            if (dto.eyes is not { Length: > 0 })
            {
                throw new ArgumentException($"Ground truth '{dto.name}' declares no eyes.");
            }

            var eyes = dto.eyes
                .Select(e => new GroundTruthEye(e.center, Mathf.Max(0f, e.radiusVoxels)))
                .ToList();
            return new EyeGroundTruth(dto.name ?? string.Empty, eyes, dto.note ?? string.Empty);
        }

        public string ToJson() => JsonUtility.ToJson(
            new Dto
            {
                name = Name,
                note = Note,
                eyes = Eyes.Select(e => new EyeDto { center = e.Center, radiusVoxels = e.RadiusVoxels }).ToArray(),
            },
            prettyPrint: true);

        [Serializable]
        private sealed class Dto
        {
            public string? name;
            public string? note;
            public EyeDto[]? eyes;
        }

        [Serializable]
        private sealed class EyeDto
        {
            public Vector3 center;
            public float radiusVoxels;
        }
    }
}
