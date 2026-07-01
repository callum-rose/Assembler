using System.Collections.Generic;
using UnityEngine;

namespace Assembler.AssetGeneration.MeshToVoxelSpike
{
    /// <summary>
    /// Instantiates each pipeline stage as a GameObject under a single parent, laid out left-to-right
    /// along +X with a floating label, so the whole progression — original → isosurface → smoothed →
    /// (reprojected) → coloured smooth → blocky voxel model — is visible at once for A/B judgement.
    /// The prior preview is cleared on each run. Editor-time scene objects only (not saved).
    /// </summary>
    public static class SpikeStagePreviewer
    {
        private const string ParentName = "MeshToVoxelSpike Preview";
        private const float Gap = 0.5f; // world-unit gap between stages, relative to mesh size

        public static void Show(SpikeStageResult result, float rowSpacingScale)
        {
            ClearPrevious();

            var parent = new GameObject(ParentName);
            Material coloured = ColouredMaterial();
            Material grey = GreyMaterial();

            var stages = new List<(string label, Mesh mesh, bool coloured)>
            {
                ("1 · Original", result.Original, true),
                ("2 · MC Isosurface", result.Iso, false),
                ("3 · Taubin Smoothed", result.Smoothed, false),
            };
            if (result.Reprojected != null)
            {
                stages.Add(("4 · SDF Reprojected", result.Reprojected, false));
            }
            stages.Add(("5 · Smooth (reprojected colour)", result.SmoothColoured, true));
            stages.Add(("6 · Blocky Voxel Model", result.Blocky, true));

            float cursorX = 0f;
            foreach ((string label, Mesh mesh, bool isColoured) in stages)
            {
                Bounds b = mesh.bounds;
                float halfWidth = Mathf.Max(0.01f, b.extents.x);
                float spacing = Mathf.Max(0.1f, Gap) * rowSpacingScale;

                cursorX += halfWidth;
                CreateStage(parent.transform, label, mesh, isColoured ? coloured : grey, cursorX, b);
                cursorX += halfWidth + spacing;
            }

            Debug.Log(
                $"[MeshToVoxelSpike] Preview built: {result.VoxelCount:N0} voxels " +
                $"({result.GridX}×{result.GridY}×{result.GridZ}). See '{ParentName}' in the scene.");
        }

        /// <summary>Show only the primary blocky voxel model (when intermediates are hidden).</summary>
        public static void ShowBlockyOnly(SpikeStageResult result)
        {
            ClearPrevious();
            var parent = new GameObject(ParentName);
            CreateStage(parent.transform, "Blocky Voxel Model", result.Blocky, ColouredMaterial(), 0f, result.Blocky.bounds);
            Debug.Log(
                $"[MeshToVoxelSpike] Blocky model built: {result.VoxelCount:N0} voxels " +
                $"({result.GridX}×{result.GridY}×{result.GridZ}). See '{ParentName}' in the scene.");
        }

        public static void ClearPrevious()
        {
            // FindObjectsByType includes inactive; DestroyImmediate because this is editor scene work.
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go != null && go.name == ParentName && go.transform.parent == null)
                {
                    Object.DestroyImmediate(go);
                }
            }
        }

        private static void CreateStage(
            Transform parent, string label, Mesh mesh, Material material, float centreX, Bounds bounds)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            // Centre the mesh's bounds at (centreX, 0, 0) so stages sit side by side, floor-aligned.
            go.transform.localPosition = new Vector3(centreX - bounds.center.x, -bounds.center.y, -bounds.center.z);

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;

            AddLabel(parent, label, centreX, bounds.extents.y);
        }

        private static void AddLabel(Transform parent, string label, float centreX, float halfHeight)
        {
            Font? font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                return;
            }

            var go = new GameObject($"{label} (label)");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(centreX, halfHeight + 0.4f, 0f);
            go.transform.localScale = Vector3.one * 0.08f;

            var text = go.AddComponent<TextMesh>();
            text.text = label;
            text.font = font;
            text.anchor = TextAnchor.LowerCenter;
            text.alignment = TextAlignment.Center;
            text.color = Color.white;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        }

        private static Material ColouredMaterial()
        {
            Shader shader = Shader.Find("Assembler/VertexColorUnlit");
            return new Material(shader) { name = "SpikeVertexColour" };
        }

        // Grey intermediates reuse the same Cull-Off vertex-colour shader: their meshes carry a white
        // colour stream, so an _BaseColor grey tint renders them flat grey and double-sided (no
        // winding/culling surprises).
        private static Material GreyMaterial()
        {
            var mat = new Material(Shader.Find("Assembler/VertexColorUnlit")) { name = "SpikeGrey" };
            mat.SetColor("_BaseColor", new Color(0.7f, 0.7f, 0.7f, 1f));
            return mat;
        }
    }
}
