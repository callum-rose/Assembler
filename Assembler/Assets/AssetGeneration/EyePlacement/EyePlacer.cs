using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// End-to-end eye placement: render the voxel model from an isometric view, ask Claude
    /// where the eyes go in that image, then reproject each 2D pick back through the same
    /// view to a concrete surface voxel and outward normal. The <see cref="VoxelModel"/> is
    /// the geometric source of truth — an image alone is only 2D, so a <c>.vox</c> (read via
    /// <see cref="VoxReader"/>) is required to produce 3D coordinates.
    ///
    /// Headless like the other cores: inputs are arguments, the result is returned, no editor
    /// state. Rendering + PNG encoding still need the Unity engine, so run it on the main thread.
    /// </summary>
    public static class EyePlacer
    {
        /// <summary>
        /// Renders <paramref name="model"/> itself, asks the model for picks, and reprojects.
        /// This is the normal path: the render and the reprojection share one projection, so
        /// picks land on the voxels they were drawn over.
        /// </summary>
        public static Task<EyePlacementResult> PlaceAsync(
            string apiKey,
            VoxelModel model,
            EyePlacementOptions options,
            CancellationToken cancellationToken = default)
        {
            var projection = new VoxelViewProjection(options.View, model);
            byte[] png = VoxelIsometricRenderer.RenderPng(model, projection, options.ImageSize);
            return PlaceInternalAsync(apiKey, model, options, projection, png, "image/png", cancellationToken);
        }

        /// <summary>
        /// Uses a caller-supplied image as the vision cue instead of an auto-render — for when
        /// you already have a picture of the model. Picks are still reprojected through
        /// <paramref name="options"/>.View against <paramref name="model"/>, so the supplied
        /// image should frame the model with that same view or the reprojection will drift.
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
            return PlaceInternalAsync(apiKey, model, options, projection, imageData, mediaType, cancellationToken);
        }

        /// <summary>Offline geometric placement — no render, no network. See <see cref="GeometricEyePlacer"/>.</summary>
        public static EyePlacementResult PlaceGeometric(VoxelModel model, EyePlacementOptions options) =>
            new(GeometricEyePlacer.Place(model, options), "(geometric — no model call)", null);

        private static async Task<EyePlacementResult> PlaceInternalAsync(
            string apiKey,
            VoxelModel model,
            EyePlacementOptions options,
            VoxelViewProjection projection,
            byte[] imageData,
            string mediaType,
            CancellationToken cancellationToken)
        {
            EyePicks picks = await ImageEyePlacer.DetermineAsync(
                apiKey, imageData, mediaType, options.EyeCount, options.Model, cancellationToken);

            var anchors = new List<EyeAnchor>(picks.Points.Count);
            foreach (Vector2 pick in picks.Points)
            {
                projection.NormalizedToRay(pick, out Vector3 origin, out Vector3 direction);
                if (VoxelRaycaster.TryRaycast(model, origin, direction, projection.RayLength,
                        out Vector3Int hit, out Vector3 normal))
                {
                    anchors.Add(new EyeAnchor((Vector3)hit + normal * options.SurfaceOffset, normal));
                }
            }

            return new EyePlacementResult(anchors, picks.RawResponse, imageData);
        }
    }
}
