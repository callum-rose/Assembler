using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Assembler.Voxels;
using Assembler.AssetGeneration.ImageOrientation;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>The detected front (null when unread) and the eye-placement view chosen for it.</summary>
    public sealed record OrientationOutcome(FacingDirection? Front, OrthographicView View);

    /// <summary>
    /// Works out which way a voxel model's front faces and returns an isometric view that looks at
    /// that front, so eye placement always sees the face. It renders the model <b>top-down</b> and
    /// runs <see cref="ImageFacingDirection"/> on it: looking straight down the up axis removes the
    /// toward/away ambiguity a side view has, so the eight-way compass code fully determines the
    /// front's yaw on the ground plane. That yaw is turned into a three-quarter camera pointed at
    /// the front.
    /// </summary>
    public static class ModelOrientation
    {
        public const int DetectionImageSize = 512;

        public static async Task<OrientationOutcome> DetermineAsync(
            string apiKey,
            VoxelModel model,
            float pitchDegrees,
            float yawOffsetDegrees,
            string? visionModel = null,
            CancellationToken cancellationToken = default)
        {
            var topProjection = new VoxelViewProjection(OrthographicView.Top, model);
            byte[] topPng = VoxelRender.ToPng(model, topProjection, DetectionImageSize);

            OrientationResult result = await ImageFacingDirection.DetermineAsync(
                apiKey, topPng, "image/png", visionModel, cancellationToken);

            return new OrientationOutcome(result.Direction, ViewFacingFront(result.Direction, pitchDegrees, yawOffsetDegrees));
        }

        /// <summary>
        /// The world yaw (degrees, about the up axis) the front points along, from a top-down
        /// compass code. The top view is set up with image-right = +X and image-up = +Y, so
        /// <see cref="FacingDirection.Right"/> is +X (0°), <see cref="FacingDirection.Up"/> is +Y (90°).
        /// </summary>
        public static float FrontYawDegrees(FacingDirection front) => front switch
        {
            FacingDirection.Right => 0f,
            FacingDirection.RightUp => 45f,
            FacingDirection.Up => 90f,
            FacingDirection.LeftUp => 135f,
            FacingDirection.Left => 180f,
            FacingDirection.LeftDown => 225f,
            FacingDirection.Down => 270f,
            FacingDirection.RightDown => 315f,
            _ => 0f,
        };

        /// <summary>
        /// A three-quarter view that looks at the given front. The camera stands on the front side
        /// (yaw + 180°) with a yaw offset so a side is visible too. An unreadable front falls back
        /// to the default <see cref="OrthographicView.Isometric"/> (no reorientation).
        /// </summary>
        public static OrthographicView ViewFacingFront(FacingDirection? front, float pitchDegrees, float yawOffsetDegrees)
        {
            if (front is not { } f)
            {
                return OrthographicView.Isometric;
            }

            float cameraYaw = FrontYawDegrees(f) + 180f + yawOffsetDegrees;
            return OrthographicView.FromZUpAngles(cameraYaw, pitchDegrees);
        }
    }
}
