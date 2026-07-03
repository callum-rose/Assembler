using UnityEngine;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// Tests whether an eye anchor is actually visible from a given view or is masked (occluded) by
    /// the model — the eye is on the far side, or something in front of it blocks the line of sight.
    /// It casts the same camera ray the render uses (through the eye's screen position) and checks
    /// whether the first surface voxel it meets <i>is</i> the eye's own voxel; if a nearer voxel is
    /// hit first, or the ray misses entirely, the eye is masked. Using the render's own projection
    /// means the answer matches what a viewer sees in that image.
    /// </summary>
    public static class EyeVisibility
    {
        public static bool IsMasked(VoxelModel model, EyeAnchor eye, VoxelViewProjection projection)
        {
            if (model.Voxels.Count == 0)
            {
                return false;
            }

            Vector2 uv = projection.WorldToNormalized(eye.Position);
            projection.NormalizedToRay(uv, out Vector3 origin, out Vector3 direction);
            if (!VoxelRaycaster.TryRaycast(model, origin, direction, projection.RayLength, out Vector3Int hit, out _))
            {
                return true; // the eye's pixel doesn't even land on the model from here
            }

            // The eye's surface voxel is just inside the anchor along -normal (the anchor is pushed
            // out by SurfaceOffset). Allow a 1-voxel slack for rounding at the pixel edge.
            Vector3Int eyeVoxel = Vector3Int.RoundToInt(eye.Position - eye.Normal * 0.5f);
            Vector3Int delta = hit - eyeVoxel;
            return Mathf.Abs(delta.x) + Mathf.Abs(delta.y) + Mathf.Abs(delta.z) > 1;
        }
    }
}
