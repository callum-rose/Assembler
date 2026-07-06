using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// Turns 2D eye picks on a render into <see cref="EyeAnchor"/>s on the voxel model. This is
    /// the geometry half of eye placement and is deliberately free of anatomy heuristics — it
    /// never tries to locate the head (unreliable on arbitrary geometry). Instead it makes each
    /// pick robust and, when asked, leans on <b>bilateral symmetry</b> rather than on knowing
    /// where a head is:
    ///
    /// <list type="bullet">
    /// <item><b>Surface snap.</b> Each pick is cast not as a single ray but as a small
    /// neighbourhood of rays; the frontmost consistent, camera-facing surface voxel wins. This
    /// tolerates an imprecise pick and recovers picks that graze the silhouette (which a single
    /// ray would drop), and it rejects a stray neighbour that punched through a gap to a far
    /// surface.</item>
    /// <item><b>Mirror the pair.</b> When two symmetric eyes are wanted, the best-observed pick
    /// is placed and its mirror image across the model's symmetry plane becomes the other eye, so
    /// one good pick is enough and the pair is symmetric by construction. In the orthographic
    /// front view used for placement the two eyes share a depth, so there is nothing to
    /// triangulate — mirroring is both simpler and sufficient.</item>
    /// </list>
    ///
    /// Pure and offline: no Unity scene, no network. The <see cref="VoxelModel"/> is Z-up.
    /// </summary>
    public static class EyeReprojection
    {
        private static readonly Vector3Int[] AxisDirs =
        {
            new(1, 0, 0), new(-1, 0, 0),
            new(0, 1, 0), new(0, -1, 0),
            new(0, 0, 1), new(0, 0, -1),
        };

        /// <summary>
        /// Full reprojection of a set of 2D picks into anchors, honouring
        /// <see cref="EyePlacementOptions.AssumeSymmetry"/> and
        /// <see cref="EyePlacementOptions.SurfaceSnapRadius"/>.
        /// </summary>
        public static IReadOnlyList<EyeAnchor> BuildAnchors(
            VoxelModel model, VoxelViewProjection projection, EyePlacementOptions options, IReadOnlyList<Vector2> picks)
        {
            OrthographicView view = projection.View;

            // Snap every pick to a real, camera-facing surface voxel (picks that hit nothing drop out).
            var snapped = new List<Snap>(picks.Count);
            foreach (Vector2 pick in picks)
            {
                if (TrySnapPick(model, projection, options.ImageSize, pick, options.SurfaceSnapRadius,
                        out Vector3Int voxel, out Vector3 normal, out int support))
                {
                    snapped.Add(new Snap(voxel, normal, support));
                }
            }

            // Symmetric two-eye case: place the best-observed eye and mirror it, so a single
            // reliable pick still yields a symmetric pair.
            if (options.AssumeSymmetry && options.EyeCount == 2 && snapped.Count >= 1)
            {
                return MirrorPair(model, view, options, snapped);
            }

            return snapped
                .Select(s => new EyeAnchor((Vector3)s.Voxel + s.Normal * options.SurfaceOffset, s.Normal))
                .ToList();
        }

        /// <summary>
        /// Snaps a normalised 2D <paramref name="pick"/> to a surface voxel by casting a small disc
        /// of rays around it (radius in voxels) and choosing the frontmost, camera-facing hit that
        /// agrees with the neighbourhood's median depth, preferring the sample nearest the pick.
        /// <paramref name="support"/> is how many neighbourhood rays agreed — a rough confidence.
        /// </summary>
        public static bool TrySnapPick(
            VoxelModel model, VoxelViewProjection projection, int imageSize, Vector2 pick, float searchRadiusVoxels,
            out Vector3Int voxel, out Vector3 normal, out int support)
        {
            voxel = Vector3Int.zero;
            normal = Vector3.zero;
            support = 0;

            OrthographicView view = projection.View;
            float voxelNorm = imageSize > 0 ? projection.VoxelPixelSize(imageSize) / imageSize : 0f;
            int r = Mathf.Max(0, Mathf.CeilToInt(searchRadiusVoxels));
            float r2 = searchRadiusVoxels * searchRadiusVoxels;

            var hits = new List<(Vector3Int Voxel, Vector3 Normal, float Depth, int Offset2)>();
            for (int dy = -r; dy <= r; dy++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    int offset2 = dx * dx + dy * dy;
                    if (offset2 > 0 && offset2 > r2)
                    {
                        continue; // circular neighbourhood; the exact pick (0,0) is always included
                    }

                    Vector2 sample = pick + new Vector2(dx, dy) * voxelNorm;
                    projection.NormalizedToRay(sample, out Vector3 origin, out Vector3 direction);
                    if (!VoxelRaycaster.TryRaycast(model, origin, direction, projection.RayLength,
                            out Vector3Int hit, out Vector3 hitNormal))
                    {
                        continue;
                    }
                    if (Vector3.Dot(hitNormal, -view.Forward) <= 0f)
                    {
                        continue; // only surfaces that face the camera
                    }

                    hits.Add((hit, hitNormal, Vector3.Dot((Vector3)hit, view.Forward), offset2));
                }
            }

            if (hits.Count == 0)
            {
                return false;
            }

            // Depth consensus: discard neighbours that shot through a gap to a far surface.
            var sortedDepths = hits.Select(h => h.Depth).OrderBy(d => d).ToList();
            float medianDepth = sortedDepths[sortedDepths.Count / 2];
            var consistent = hits.Where(h => Mathf.Abs(h.Depth - medianDepth) <= 1.5f).ToList();
            if (consistent.Count == 0)
            {
                consistent = hits;
            }

            var best = consistent.OrderBy(h => h.Offset2).First();
            voxel = best.Voxel;
            normal = best.Normal;
            support = consistent.Count;
            return true;
        }

        /// <summary>
        /// The model's bilateral symmetry plane for this view, as (point on plane, unit normal).
        /// The normal is the model's left–right axis — the horizontal component of the camera's
        /// right axis, which is the eye-to-eye direction in a front view. The point is the bounding
        /// centre, which lies on the plane for any bilaterally symmetric model.
        /// </summary>
        public static (Vector3 Point, Vector3 Normal) SymmetryPlane(VoxelModel model, OrthographicView view)
        {
            var normal = new Vector3(view.Right.x, view.Right.y, 0f);
            normal = normal.sqrMagnitude < 1e-6f ? Vector3.right : normal.normalized;
            Vector3 centre = ((Vector3)model.Min + (Vector3)model.Max) * 0.5f + Vector3.one * 0.5f;
            return (centre, normal);
        }

        /// <summary>Reflects a point across the plane defined by <paramref name="planePoint"/> and unit <paramref name="planeNormal"/>.</summary>
        public static Vector3 ReflectPoint(Vector3 point, Vector3 planePoint, Vector3 planeNormal) =>
            point - 2f * Vector3.Dot(point - planePoint, planeNormal) * planeNormal;

        /// <summary>Reflects a direction across a plane through the origin with unit <paramref name="planeNormal"/>.</summary>
        public static Vector3 ReflectDirection(Vector3 direction, Vector3 planeNormal) =>
            direction - 2f * Vector3.Dot(direction, planeNormal) * planeNormal;

        /// <summary>
        /// Snaps a continuous world <paramref name="point"/> to the nearest occupied voxel within
        /// <paramref name="searchRadiusVoxels"/>, estimating an outward, camera-biased normal from
        /// its exposed faces. Used to seat a mirrored eye onto the actual surface.
        /// </summary>
        public static bool TrySnapPoint(
            VoxelModel model, OrthographicView view, Vector3 point, int searchRadiusVoxels,
            out Vector3Int voxel, out Vector3 normal)
        {
            voxel = Vector3Int.zero;
            normal = Vector3.zero;

            var centre = new Vector3Int(
                Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y), Mathf.FloorToInt(point.z));
            int r = Mathf.Max(0, searchRadiusVoxels);
            float bestSqr = float.PositiveInfinity;
            bool found = false;

            for (int dz = -r; dz <= r; dz++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        var cell = new Vector3Int(centre.x + dx, centre.y + dy, centre.z + dz);
                        if (!model.Voxels.ContainsKey(cell))
                        {
                            continue;
                        }

                        float sqr = (((Vector3)cell + Vector3.one * 0.5f) - point).sqrMagnitude;
                        if (sqr < bestSqr)
                        {
                            bestSqr = sqr;
                            voxel = cell;
                            found = true;
                        }
                    }
                }
            }

            if (!found)
            {
                return false;
            }

            normal = EstimateOutwardNormal(model, voxel, view);
            return true;
        }

        private static List<EyeAnchor> MirrorPair(
            VoxelModel model, OrthographicView view, EyePlacementOptions options, IReadOnlyList<Snap> snapped)
        {
            // Primary = best-supported pick; the other eye is its mirror image.
            Snap primary = snapped.OrderByDescending(s => s.Support).First();
            Vector3 primaryCentre = (Vector3)primary.Voxel + Vector3.one * 0.5f;

            (Vector3 planePoint, Vector3 planeNormal) = SymmetryPlane(model, view);
            Vector3 mirroredCentre = ReflectPoint(primaryCentre, planePoint, planeNormal);

            int snapRadius = Mathf.CeilToInt(options.SurfaceSnapRadius) + 2;
            Vector3 mirrorSurface;
            Vector3 mirrorNormal;
            if (TrySnapPoint(model, view, mirroredCentre, snapRadius, out Vector3Int mirrorVoxel, out Vector3 snappedNormal))
            {
                mirrorSurface = (Vector3)mirrorVoxel;
                mirrorNormal = snappedNormal;
            }
            else
            {
                // Mirror fell outside the model (asymmetric geometry) — keep the reflected pose.
                mirrorSurface = mirroredCentre - Vector3.one * 0.5f;
                mirrorNormal = ReflectDirection(primary.Normal, planeNormal);
            }

            var primaryAnchor = new EyeAnchor((Vector3)primary.Voxel + primary.Normal * options.SurfaceOffset, primary.Normal);
            var mirrorAnchor = new EyeAnchor(mirrorSurface + mirrorNormal * options.SurfaceOffset, mirrorNormal);

            // Deterministic left→right order along the eye-to-eye axis.
            return new[] { primaryAnchor, mirrorAnchor }
                .OrderBy(a => Vector3.Dot(a.Position, view.Right))
                .ToList();
        }

        private static Vector3 EstimateOutwardNormal(VoxelModel model, Vector3Int voxel, OrthographicView view)
        {
            Vector3 best = Vector3.zero;
            float bestDot = float.NegativeInfinity;
            foreach (Vector3Int dir in AxisDirs)
            {
                if (model.Voxels.ContainsKey(voxel + dir))
                {
                    continue; // buried face
                }

                float dot = Vector3.Dot((Vector3)dir, -view.Forward);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best = dir;
                }
            }

            return best == Vector3.zero ? -view.Forward : best;
        }

        private readonly record struct Snap(Vector3Int Voxel, Vector3 Normal, int Support);
    }
}
