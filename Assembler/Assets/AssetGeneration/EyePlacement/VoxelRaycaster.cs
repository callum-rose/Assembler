using UnityEngine;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// Marches a ray through a <see cref="VoxelModel"/>'s sparse occupancy and returns
    /// the first filled voxel it enters, together with the face it entered through as
    /// an outward normal. This is the geometric half of eye placement: a 2D pick on a
    /// render becomes a ray (via <see cref="VoxelViewProjection"/>) that this resolves
    /// to a concrete surface voxel and orientation — so it works for a pick on the
    /// front face or a side face alike. Pure and offline; no Unity scene involved.
    ///
    /// Uses the Amanatides–Woo grid traversal, so every voxel the ray crosses is
    /// visited in order with no gaps. Because the ray originates outside the model on
    /// the camera side, the entry face always faces the camera, which is exactly the
    /// surface a viewer clicked.
    /// </summary>
    public static class VoxelRaycaster
    {
        public static bool TryRaycast(
            VoxelModel model,
            Vector3 origin,
            Vector3 direction,
            float maxDistance,
            out Vector3Int hitVoxel,
            out Vector3 normal)
        {
            hitVoxel = Vector3Int.zero;
            normal = Vector3.zero;

            Vector3 dir = direction.normalized;
            if (dir == Vector3.zero || model.Voxels.Count == 0)
            {
                return false;
            }

            var cell = new Vector3Int(
                Mathf.FloorToInt(origin.x),
                Mathf.FloorToInt(origin.y),
                Mathf.FloorToInt(origin.z));

            var step = new Vector3Int(
                dir.x > 0f ? 1 : dir.x < 0f ? -1 : 0,
                dir.y > 0f ? 1 : dir.y < 0f ? -1 : 0,
                dir.z > 0f ? 1 : dir.z < 0f ? -1 : 0);

            Vector3 tMax = new(
                RayToNextBoundary(origin.x, dir.x, cell.x, step.x),
                RayToNextBoundary(origin.y, dir.y, cell.y, step.y),
                RayToNextBoundary(origin.z, dir.z, cell.z, step.z));

            Vector3 tDelta = new(
                dir.x != 0f ? Mathf.Abs(1f / dir.x) : float.PositiveInfinity,
                dir.y != 0f ? Mathf.Abs(1f / dir.y) : float.PositiveInfinity,
                dir.z != 0f ? Mathf.Abs(1f / dir.z) : float.PositiveInfinity);

            int lastAxis = -1;
            float travelled = 0f;

            // A generous cap on iterations so a degenerate ray can never spin forever.
            int maxSteps = Mathf.CeilToInt(maxDistance) * 3 + 8;
            for (int i = 0; i < maxSteps && travelled <= maxDistance; i++)
            {
                if (model.Voxels.ContainsKey(cell))
                {
                    hitVoxel = cell;
                    normal = lastAxis switch
                    {
                        0 => new Vector3(-step.x, 0f, 0f),
                        1 => new Vector3(0f, -step.y, 0f),
                        2 => new Vector3(0f, 0f, -step.z),
                        _ => -dir, // origin started inside a voxel: face the ray back out
                    };
                    return true;
                }

                if (tMax.x <= tMax.y && tMax.x <= tMax.z)
                {
                    cell.x += step.x;
                    travelled = tMax.x;
                    tMax.x += tDelta.x;
                    lastAxis = 0;
                }
                else if (tMax.y <= tMax.z)
                {
                    cell.y += step.y;
                    travelled = tMax.y;
                    tMax.y += tDelta.y;
                    lastAxis = 1;
                }
                else
                {
                    cell.z += step.z;
                    travelled = tMax.z;
                    tMax.z += tDelta.z;
                    lastAxis = 2;
                }
            }

            return false;
        }

        // Distance along the ray from the origin to the first cell boundary it crosses.
        private static float RayToNextBoundary(float origin, float dir, int cell, int step)
        {
            if (step == 0)
            {
                return float.PositiveInfinity;
            }

            float boundary = step > 0 ? cell + 1 : cell;
            return (boundary - origin) / dir;
        }
    }
}
