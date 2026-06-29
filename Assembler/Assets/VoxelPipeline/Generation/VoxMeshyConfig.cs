using System;
using Assembler.MeshyImageTo3D;
using UnityEngine;

namespace Assembler.VoxelPipeline.Generation
{
    /// <summary>
    /// The image → mesh (Meshy.ai) generation parameters the AI layer chooses for the full pipeline.
    /// A <see cref="SerializableAttribute"/> class with mutable public fields (like
    /// <see cref="VoxPipelineSettings"/>) so the prompt builder can reflect over it and the parser can
    /// <see cref="UnityEngine.JsonUtility.FromJsonOverwrite"/> a partial object onto it.
    ///
    /// These mirror the stage-2 fields of the pipeline window's settings (kept here rather than
    /// referencing them so this AI layer doesn't depend on the pipeline window). The defaults match.
    /// Environment-level inputs — API keys, output dir, image provider/model — are deliberately not
    /// here: those are the caller's, not the model's, to decide.
    /// </summary>
    [Serializable]
    public sealed class VoxMeshyConfig
    {
        [Tooltip("Meshy model id: meshy-6 (best, default), meshy-5, or meshy-4.")]
        public string MeshAiModel = "meshy-6";

        [Tooltip("Model file format to generate and download.")]
        public ModelFormat MeshFormat = ModelFormat.Obj;

        [Tooltip("Generate a texture for the model.")]
        public bool GenerateTexture = true;

        [Tooltip("Also generate metallic/roughness/normal (PBR) maps. Only matters when GenerateTexture is on.")]
        public bool EnablePbr = true;

        [Tooltip("Generate a higher-resolution texture. Only matters when GenerateTexture is on.")]
        public bool HdTexture = false;

        [Tooltip("Let Meshy clean up the topology.")]
        public bool Remesh = true;

        [Tooltip("Target face topology when remeshing. Only matters when Remesh is on.")]
        public MeshyTopology Topology = MeshyTopology.Triangle;

        [Tooltip("Remesh decimation preset. 'None' lets Meshy decide (or set TargetPolycount instead). Only matters when Remesh is on.")]
        public DecimationMode Decimation = DecimationMode.None;

        [Range(100, 300000)]
        [Tooltip("Target triangle count when remeshing. Only used when Remesh is on and Decimation is None.")]
        public int TargetPolycount = 30000;

        [Tooltip("Also keep the model from before remeshing. Only matters when Remesh is on.")]
        public bool SavePreRemeshedModel = false;

        [Tooltip("Bake out baked-in lighting from the source image. Only supported on meshy-6 (ignored otherwise).")]
        public bool RemoveLighting = true;

        [Tooltip("Auto-scale the model to a realistic size.")]
        public bool AutoSize = false;

        [Tooltip("Where the model's pivot sits.")]
        public ModelOrigin OriginAt = ModelOrigin.Bottom;

        [Tooltip("Run content moderation on the input image.")]
        public bool Moderation = false;

        [Tooltip("Generate thumbnails from several angles.")]
        public bool MultiViewThumbnails = false;

        [Tooltip("Generate a thumbnail with a transparent background.")]
        public bool AlphaThumbnail = false;
    }
}
