using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Assembler.Voxels;
using Assembler.AssetGeneration.ImageOrientation;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>The eye-placement view chosen to face the model's front, plus a human-readable label.</summary>
    public sealed record OrientationOutcome(OrthographicView View, string Label);

    /// <summary>
    /// Works out which way a voxel model's front faces and returns an isometric view that looks at
    /// that front, so eye placement always sees the face. It renders a ring of isometric views around
    /// the up axis and asks <see cref="ImageFacingDirection.SelectViewAsync"/> which one best shows the
    /// front. Isometric candidates carry far more shape information than a single flat view, and
    /// picking from concrete rendered angles sidesteps the toward/away ambiguity a single in-plane
    /// compass read suffers — each candidate <i>is</i> a real yaw, and the winner is used directly as
    /// the eye-placement render.
    /// </summary>
    public static class ModelOrientation
    {
        public const int DetectionImageSize = 512;

        public static async Task<OrientationOutcome> DetermineAsync(
            string apiKey,
            VoxelModel model,
            float pitchDegrees,
            int viewCount,
            string? visionModel = null,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<float> yaws = CandidateYaws(viewCount);
            var views = new List<OrthographicView>(yaws.Count);
            var images = new List<byte[]>(yaws.Count);
            foreach (float yaw in yaws)
            {
                var view = OrthographicView.FromZUpAngles(yaw, pitchDegrees);
                views.Add(view);
                var projection = new VoxelViewProjection(view, model);
                images.Add(VoxelRender.ToPng(model, projection, DetectionImageSize));
            }

            OrientationResult result = await ImageFacingDirection.SelectViewAsync(
                apiKey, images, "image/png", visionModel, cancellationToken);

            int index = ResolveIndex(result.Index, yaws.Count);
            return new OrientationOutcome(views[index], $"view {index} (yaw {yaws[index]:0}°)");
        }

        /// <summary>An even ring of yaw candidates (degrees) around the up axis, starting at 0.</summary>
        public static IReadOnlyList<float> CandidateYaws(int count)
        {
            count = Mathf.Clamp(count, 1, 16);
            var yaws = new List<float>(count);
            for (int i = 0; i < count; i++)
            {
                yaws.Add(i * (360f / count));
            }

            return yaws;
        }

        /// <summary>Clamps a model-chosen index into range, falling back to 0 when it's missing or out of range.</summary>
        public static int ResolveIndex(int? index, int count) =>
            index is { } i && i >= 0 && i < count ? i : 0;
    }
}
