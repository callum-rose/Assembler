using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// End-to-end eye placement: optionally turn the model to face the camera, render a
    /// high-resolution screenshot from an isometric view, ask Claude where the eyes go in that
    /// image, then reproject each 2D pick back through the same view to a concrete surface voxel
    /// and outward normal. The <see cref="VoxelModel"/> is the geometric source of truth — an image
    /// alone is only 2D, so a <c>.vox</c> (read via <see cref="VoxReader"/>) is required to produce
    /// 3D coordinates.
    ///
    /// Headless like the other cores: inputs are arguments, the result is returned, no editor state.
    /// Rendering + PNG encoding need the Unity engine, so run it on the main thread.
    /// </summary>
    public static class EyePlacer
    {
        /// <summary>
        /// Renders <paramref name="model"/> itself, asks the model for picks, and reprojects. When
        /// <see cref="EyePlacementOptions.AutoOrient"/> is set, a top-down vision pass first detects
        /// the front so the eye-placement camera looks at the face. The render and the reprojection
        /// share one projection, so picks land on the voxels they were drawn over.
        /// </summary>
        public static async Task<EyePlacementResult> PlaceAsync(
            string apiKey,
            VoxelModel model,
            EyePlacementOptions options,
            CancellationToken cancellationToken = default)
        {
            OrthographicView view = options.View;
            string? frontLabel = null;
            IReadOnlyList<OrientationCandidate> candidates = System.Array.Empty<OrientationCandidate>();
            if (options.AutoOrient)
            {
                OrientationOutcome outcome = await ModelOrientation.DetermineAsync(
                    apiKey, model, options.PitchDegrees, options.OrientationViewCount, options.Model, cancellationToken);
                view = outcome.View;
                frontLabel = outcome.Label;
                candidates = outcome.Candidates;
            }

            var projection = new VoxelViewProjection(view, model);
            byte[] png = VoxelRender.ToPng(model, projection, options.ImageSize, options.Msaa);
            return await PlaceInternalAsync(
                apiKey, model, options, projection, png, "image/png", frontLabel, candidates, cancellationToken);
        }

        /// <summary>
        /// Uses a caller-supplied image as the vision cue instead of an auto-render — for when you
        /// already have a picture of the model. Auto-orientation does not apply (we can't turn a
        /// supplied image); picks are reprojected through <paramref name="options"/>.View against
        /// <paramref name="model"/>, so the image should frame the model with that same view.
        /// </summary>
        public static Task<EyePlacementResult> PlaceFromImageAsync(
            string apiKey,
            VoxelModel model,
            byte[] imageData,
            string mediaType,
            EyePlacementOptions options,
            CancellationToken cancellationToken = default)
        {
            var projection = new VoxelViewProjection(options.View, model);
            return PlaceInternalAsync(
                apiKey, model, options, projection, imageData, mediaType, null,
                System.Array.Empty<OrientationCandidate>(), cancellationToken);
        }

        /// <summary>Offline geometric placement — no render, no network. See <see cref="GeometricEyePlacer"/>.</summary>
        public static EyePlacementResult PlaceGeometric(VoxelModel model, EyePlacementOptions options) =>
            new(GeometricEyePlacer.Place(model, options), "(geometric — no model call)", null, options.View, null,
                System.Array.Empty<OrientationCandidate>());

        private static async Task<EyePlacementResult> PlaceInternalAsync(
            string apiKey,
            VoxelModel model,
            EyePlacementOptions options,
            VoxelViewProjection projection,
            byte[] imageData,
            string mediaType,
            string? frontCode,
            IReadOnlyList<OrientationCandidate> candidates,
            CancellationToken cancellationToken)
        {
            EyePicks picks = await ImageEyePlacer.DetermineAsync(
                apiKey, imageData, mediaType, options.EyeCount, options.Model, cancellationToken);

            IReadOnlyList<EyeAnchor> anchors = EyeReprojection.BuildAnchors(model, projection, options, picks.Points);

            return new EyePlacementResult(anchors, picks.RawResponse, imageData, projection.View, frontCode, candidates);
        }
    }
}
