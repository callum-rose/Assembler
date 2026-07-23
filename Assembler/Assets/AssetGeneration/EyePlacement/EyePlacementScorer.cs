using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>Thresholds for judging a resolved anchor against a ground-truth eye.</summary>
    public sealed record EyeScoreOptions
    {
        /// <summary>Acceptance radius (voxels) used for a ground-truth eye that specifies none of its own.</summary>
        public float DefaultToleranceVoxels { get; init; } = 2.5f;

        /// <summary>
        /// An anchor fails the "not up" check when its normal points up more than this — i.e.
        /// <c>dot(normal, +Z) &gt; UpNormalDotThreshold</c>. Eyes essentially never face up, so
        /// top/upward-surface snaps (the dominant failure mode) are rejected here.
        /// </summary>
        public float UpNormalDotThreshold { get; init; } = 0.6f;

        /// <summary>
        /// Radius (voxels) searched around an anchor's position for the occupied surface voxel it is
        /// meant to sit on. An anchor is pushed proud of the surface by <c>SurfaceOffset</c>, so a
        /// small search is needed to find the voxel it belongs to (and reject off-model anchors).
        /// </summary>
        public float SurfaceSearchRadius { get; init; } = 1.5f;
    }

    /// <summary>How one ground-truth eye fared: the anchor matched to it (if any) and the three checks.</summary>
    public sealed record EyeScore(
        Vector3 Target,
        EyeAnchor? Anchor,
        float DistanceVoxels,
        bool WithinTolerance,
        bool OnSurface,
        bool NormalNotUp,
        string Reason)
    {
        public bool Pass => Anchor is not null && WithinTolerance && OnSurface && NormalNotUp;
    }

    /// <summary>Per-model verdict: PASS only when every ground-truth eye was reached by a valid anchor.</summary>
    public sealed record ModelScore(
        string Name,
        bool Pass,
        IReadOnlyList<EyeScore> Eyes,
        int ExtraAnchors,
        string Summary);

    /// <summary>
    /// Judges a placement run's 3D anchors against a model's <see cref="EyeGroundTruth"/> — the harness
    /// that gates all eye-placement accuracy work (issue #479). Each ground-truth eye is matched to the
    /// nearest resolved anchor (globally, cheapest pairs first), and that anchor passes only if it is
    /// <b>within tolerance</b> of the eye, <b>on a real surface voxel</b>, and its <b>normal is not
    /// pointing up</b>. A model passes only when every ground-truth eye passes. Pure and offline: no
    /// scene, no network, no GPU — the <see cref="VoxelModel"/> is Z-up.
    /// </summary>
    public static class EyePlacementScorer
    {
        private static readonly Vector3 Up = new(0f, 0f, 1f);

        private static readonly Vector3Int[] FaceOffsets =
        {
            new(1, 0, 0), new(-1, 0, 0),
            new(0, 1, 0), new(0, -1, 0),
            new(0, 0, 1), new(0, 0, -1),
        };

        public static ModelScore Score(
            VoxelModel model, EyeGroundTruth truth, IReadOnlyList<EyeAnchor> anchors, EyeScoreOptions options)
        {
            IReadOnlyDictionary<int, int> matches = MatchNearest(truth.Eyes, anchors);

            var eyeScores = new List<EyeScore>(truth.Eyes.Count);
            for (int e = 0; e < truth.Eyes.Count; e++)
            {
                GroundTruthEye eye = truth.Eyes[e];
                Vector3 target = eye.Center + Vector3.one * 0.5f; // ground-truth voxel centre

                if (!matches.TryGetValue(e, out int a))
                {
                    eyeScores.Add(new EyeScore(target, null, float.PositiveInfinity, false, false, false,
                        "no anchor placed for this eye"));
                    continue;
                }

                EyeAnchor anchor = anchors[a];
                float distance = Vector3.Distance(anchor.Position, target);
                float tolerance = eye.RadiusVoxels > 0f ? eye.RadiusVoxels : options.DefaultToleranceVoxels;
                bool within = distance <= tolerance;
                bool onSurface = IsOnSurface(model, anchor.Position, options.SurfaceSearchRadius);
                bool notUp = IsNormalNotUp(anchor.Normal, options.UpNormalDotThreshold);

                eyeScores.Add(new EyeScore(target, anchor, distance, within, onSurface, notUp,
                    DescribeFailures(distance, tolerance, within, onSurface, notUp, anchor.Normal)));
            }

            bool pass = eyeScores.All(s => s.Pass);
            int extra = Mathf.Max(0, anchors.Count - matches.Count);
            return new ModelScore(truth.Name, pass, eyeScores, extra, Summarise(truth.Name, pass, eyeScores, extra));
        }

        /// <summary>
        /// Assigns each ground-truth eye at most one anchor by taking the globally cheapest
        /// (eye, anchor) pairs first, so a single anchor can't be double-counted for both eyes.
        /// Returns eye-index → anchor-index.
        /// </summary>
        private static IReadOnlyDictionary<int, int> MatchNearest(
            IReadOnlyList<GroundTruthEye> eyes, IReadOnlyList<EyeAnchor> anchors)
        {
            var pairs = new List<(int Eye, int Anchor, float Dist)>();
            for (int e = 0; e < eyes.Count; e++)
            {
                Vector3 target = eyes[e].Center + Vector3.one * 0.5f;
                for (int a = 0; a < anchors.Count; a++)
                {
                    pairs.Add((e, a, Vector3.Distance(anchors[a].Position, target)));
                }
            }

            var result = new Dictionary<int, int>();
            var usedAnchors = new HashSet<int>();
            foreach ((int eye, int anchor, float _) in pairs.OrderBy(p => p.Dist))
            {
                if (result.ContainsKey(eye) || usedAnchors.Contains(anchor))
                {
                    continue;
                }

                result[eye] = anchor;
                usedAnchors.Add(anchor);
            }

            return result;
        }

        /// <summary>
        /// True when a surface voxel of the model sits within <paramref name="searchRadius"/> of
        /// <paramref name="position"/> — i.e. the anchor is genuinely seated on the model, not floating
        /// off it (the shark's <c>(-9.5, 23, 7)</c> off-model anchor fails here).
        /// </summary>
        private static bool IsOnSurface(VoxelModel model, Vector3 position, float searchRadius)
        {
            var origin = Vector3Int.FloorToInt(position);
            int r = Mathf.Max(0, Mathf.CeilToInt(searchRadius));
            for (int dz = -r; dz <= r; dz++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var cell = new Vector3Int(origin.x + dx, origin.y + dy, origin.z + dz);
                        if (!model.Voxels.ContainsKey(cell))
                        {
                            continue;
                        }

                        Vector3 centre = (Vector3)cell + Vector3.one * 0.5f;
                        if ((centre - position).magnitude <= searchRadius && IsSurfaceVoxel(model, cell))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsSurfaceVoxel(VoxelModel model, Vector3Int cell) =>
            FaceOffsets.Any(offset => !model.Voxels.ContainsKey(cell + offset));

        private static bool IsNormalNotUp(Vector3 normal, float upDotThreshold) =>
            normal.sqrMagnitude > 1e-6f && Vector3.Dot(normal.normalized, Up) <= upDotThreshold;

        private static string DescribeFailures(
            float distance, float tolerance, bool within, bool onSurface, bool notUp, Vector3 normal)
        {
            if (within && onSurface && notUp)
            {
                return $"ok (d={distance:0.0}v ≤ {tolerance:0.0}v)";
            }

            var reasons = new List<string>();
            if (!within)
            {
                reasons.Add($"off target ({distance:0.0}v > {tolerance:0.0}v)");
            }
            if (!onSurface)
            {
                reasons.Add("not on a surface voxel");
            }
            if (!notUp)
            {
                reasons.Add(normal.sqrMagnitude <= 1e-6f ? "no normal" : "normal points up (+Z)");
            }

            return string.Join("; ", reasons);
        }

        private static string Summarise(string name, bool pass, IReadOnlyList<EyeScore> eyes, int extra)
        {
            int passed = eyes.Count(s => s.Pass);
            string tail = extra > 0 ? $", {extra} extra anchor(s)" : string.Empty;
            return $"{(pass ? "PASS" : "FAIL")} {name}: {passed}/{eyes.Count} eyes correct{tail}";
        }
    }
}
