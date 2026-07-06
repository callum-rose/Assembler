using System;
using UnityEngine;
using UnityEngine.Rendering;
using Assembler.Voxels;

namespace Assembler.AssetGeneration.EyePlacement
{
    /// <summary>
    /// Renders a <see cref="VoxelModel"/> to a high-resolution PNG with a real Unity camera — a
    /// proper screenshot of the shaded mesh, far better than a CPU splat for the vision model to
    /// read. It builds the mesh (<see cref="VoxelPreviewMesh"/>), drives an orthographic camera
    /// straight from the <see cref="VoxelViewProjection"/> (so a pick on the render reprojects
    /// exactly), and reads the result back. Renders through <see cref="Camera.SubmitRenderRequest"/>
    /// (the URP/SRP path) with a built-in-pipeline fallback.
    ///
    /// Returns <c>false</c> (rather than throwing) when there is no GPU or the shader is missing,
    /// so callers can fall back to the CPU renderer and keep working in headless/batch contexts.
    /// </summary>
    public static class VoxelCameraRenderer
    {
        // The project's vertex-colour shader (used by the MeshToVoxelSpike previewer); renders the
        // baked-shade vertex colours under URP without a light rig.
        private const string ShaderName = "Assembler/VertexColorUnlit";
        private static readonly Color Background = new(0.94f, 0.94f, 0.94f, 1f);

        public static bool TryRenderPng(
            VoxelModel model, VoxelViewProjection projection, int imageSize, int msaa, out byte[] png)
        {
            png = Array.Empty<byte>();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || model.Voxels.Count == 0)
            {
                return false;
            }

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                return false;
            }

            imageSize = Mathf.Clamp(imageSize, 64, 4096);
            msaa = ClampMsaa(msaa);
            // Render far from the origin so nothing else in the open scene lands in the frustum.
            var offset = new Vector3(10000f, 10000f, 10000f);

            Mesh? mesh = null;
            Material? material = null;
            RenderTexture? rt = null;
            Texture2D? tex = null;
            GameObject? root = null;
            GameObject? camGo = null;
            RenderTexture? prevActive = RenderTexture.active;
            try
            {
                mesh = VoxelPreviewMesh.Build(model);
                material = new Material(shader) { name = "EyePlacementRender" };

                root = new GameObject("__EyePlacementModel") { hideFlags = HideFlags.HideAndDontSave };
                root.transform.position = offset;
                root.AddComponent<MeshFilter>().sharedMesh = mesh;
                root.AddComponent<MeshRenderer>().sharedMaterial = material;

                camGo = new GameObject("__EyePlacementCamera") { hideFlags = HideFlags.HideAndDontSave };
                var cam = camGo.AddComponent<Camera>();
                cam.enabled = false; // render on demand only
                cam.orthographic = true;
                cam.orthographicSize = projection.OrthographicSize;
                cam.aspect = 1f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Background;

                projection.GetCamera(out Vector3 pos, out Quaternion rot, out float near, out float far);
                camGo.transform.SetPositionAndRotation(pos + offset, rot);
                cam.nearClipPlane = near;
                cam.farClipPlane = far;

                rt = new RenderTexture(imageSize, imageSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = msaa };
                rt.Create();
                Render(cam, rt);

                RenderTexture.active = rt;
                tex = new Texture2D(imageSize, imageSize, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, imageSize, imageSize), 0, 0);
                tex.Apply();
                png = tex.EncodeToPNG();
                return png.Length > 0;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"[EyePlacement] Camera render failed, falling back to CPU render: {e.Message}");
                return false;
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
                if (rt != null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); }
                if (camGo != null) UnityEngine.Object.DestroyImmediate(camGo);
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
                if (material != null) UnityEngine.Object.DestroyImmediate(material);
                if (mesh != null) UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        private static void Render(Camera cam, RenderTexture rt)
        {
            var request = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(cam, request))
            {
                cam.SubmitRenderRequest(request); // URP / any SRP
            }
            else
            {
                cam.targetTexture = rt; // built-in pipeline
                cam.Render();
                cam.targetTexture = null;
            }
        }

        private static int ClampMsaa(int msaa) =>
            msaa >= 8 ? 8 : msaa >= 4 ? 4 : msaa >= 2 ? 2 : 1;
    }
}
