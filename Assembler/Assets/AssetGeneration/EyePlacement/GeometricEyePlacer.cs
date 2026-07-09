using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// Offline, no-AI eye placement by geometry alone: assumes a bilaterally-symmetric
    /// creature standing in Z-up voxel space and places eyes high on the head, symmetric
    /// about the model's widest horizontal axis, on whichever depth face carries more
    /// surface at eye height. It reuses <see cref="VoxelRaycaster"/> to snap each guess
    /// onto the actual surface, so the anchors and normals are consistent with the vision
    /// path. It is a deterministic fallback for when there is no API key — naive on
    /// unusual shapes (multiple heads, a creature lying down), where the vision path wins.
    /// </summary>
    public static class GeometricEyePlacer
    {
        public static IReadOnlyList<EyeAnchor> Place(VoxelModel model, EyePlacementOptions options)
        {
            if (model.Voxels.Count == 0)
            {
                return System.Array.Empty<EyeAnchor>();
            }

            Vector3Int min = model.Min;
            Vector3Int size = model.Size;

            // Z is up (the .vox vertical). The two horizontal axes are X and Y; the wider
            // one is the left/right (bilateral) axis, the narrower is front/back (depth).
            int widthAxis = size.x >= size.y ? 0 : 1;
            int depthAxis = widthAxis == 0 ? 1 : 0;

            int eyeLevel = min.z + Mathf.RoundToInt(0.62f * (size.z - 1));
            int separation = Mathf.Max(1, Mathf.RoundToInt(0.22f * size[widthAxis]));

            int depthSign = FrontDepthSign(model, depthAxis, eyeLevel);
            int depthStart = depthSign > 0 ? model.Max[depthAxis] + 2 : min[depthAxis] - 2;

            var lateral = LateralPositions(min[widthAxis], model.Max[widthAxis], options.EyeCount, separation);
            var anchors = new List<EyeAnchor>(lateral.Count);
            foreach (int w in lateral)
            {
                var origin = new Vector3();
                origin[widthAxis] = w + 0.5f;
                origin[depthAxis] = depthStart + 0.5f;
                origin.z = eyeLevel + 0.5f;

                var direction = new Vector3();
                direction[depthAxis] = -depthSign;

                if (VoxelRaycaster.TryRaycast(model, origin, direction, size[depthAxis] + 4f,
                        out Vector3Int hit, out Vector3 normal))
                {
                    anchors.Add(new EyeAnchor((Vector3)hit + normal * options.SurfaceOffset, normal));
                }
            }

            return anchors;
        }

        // Pick the depth face with more filled voxels around eye height, so eyes go on the
        // fuller "face" side rather than the back. +1 means the +axis face is the front.
        private static int FrontDepthSign(VoxelModel model, int depthAxis, int eyeLevel)
        {
            int mid = (model.Min[depthAxis] + model.Max[depthAxis]) / 2;
            int positive = 0, negative = 0;
            foreach (var cell in model.Voxels.Keys)
            {
                if (Mathf.Abs(cell.z - eyeLevel) > 2)
                {
                    continue;
                }
                if (cell[depthAxis] >= mid) positive++;
                else negative++;
            }
            return positive >= negative ? 1 : -1;
        }

        // Lateral (left/right) eye columns, exactly mirrored about the axis centre so the
        // pair is symmetric even when the model has no voxel on the true half-integer centre
        // line. Even counts straddle the centre; odd counts add one on the nearest centre cell.
        private static IReadOnlyList<int> LateralPositions(int minW, int maxW, int count, int separation)
        {
            count = Mathf.Max(1, count);
            int sumW = minW + maxW;
            int centreW = sumW / 2;

            var positions = new List<int>(count);
            int pairs = count / 2;
            for (int i = 1; i <= pairs; i++)
            {
                int left = centreW - separation * i;
                positions.Add(left);
                positions.Add(sumW - left); // mirror → left/right equidistant from (minW+maxW)/2
            }
            if (count % 2 == 1)
            {
                positions.Add(centreW);
            }
            return positions.Take(count).ToList();
        }
    }
}
