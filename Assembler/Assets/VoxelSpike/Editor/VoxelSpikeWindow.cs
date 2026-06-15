using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VoxelSpike.Editor
{
    /// <summary>
    /// Spike: reconstruct a voxel model from three orthographic images (front / right / top)
    /// via supersampled silhouette intersection (visual hull) + multi-view colour back-projection,
    /// and export it to Goxel plain-text format.
    ///
    /// This is a throwaway disprove-the-theory rig. It is intentionally standalone (no dependency
    /// on the project's Voxels/Voxelization assemblies) so the result can't be confounded by the
    /// real pipeline. Conventions:
    ///   world X = right, Y = up, Z = depth(front->back).
    ///   Front view sees the XY plane, Right view sees the ZY plane, Top view sees the XZ plane.
    ///   Output is remapped to Goxel's Z-up so the model stands upright in Goxel.
    /// </summary>
    public class VoxelSpikeWindow : EditorWindow
    {
        enum ColourMode { MeanForeground, FrontPriority, MostSaturated }

        // --- inputs ---
        Texture2D _front;
        Texture2D _right;
        Texture2D _top;

        // --- knobs ---
        int _heightVoxels = 32;        // voxel count along Y (height). Width/Length derived from aspect.
        float _bgThreshold = 0.9f;     // luminance >= this => background (near-white)
        int _supersample = 3;          // samples per voxel per axis (S => S^3 samples)
        float _solidFraction = 0.5f;   // fraction of sub-samples that must pass to keep the voxel
        int _dilate = 0;               // silhouette dilation in source pixels (forgiveness for view disagreement)
        Vector3 _offset = Vector3.zero; // sub-voxel grid offset, in voxel units
        ColourMode _colourMode = ColourMode.MeanForeground;

        // --- orientation flips (handedness ambiguity per view) ---
        bool _frontFlipU, _frontFlipV;
        bool _rightFlipU, _rightFlipV;
        bool _topFlipU, _topFlipV;

        string _outputPath = "Assets/VoxelSpike/output.txt";

        // --- last-run state / preview ---
        string _status = "";
        Texture2D _frontMask, _rightMask, _topMask;
        Vector2 _scroll;

        [MenuItem("Assembler/Voxel Spike (3-View)")]
        static void Open()
        {
            GetWindow<VoxelSpikeWindow>("Voxel Spike");
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Source images (front / right / top)", EditorStyles.boldLabel);
            _front = (Texture2D)EditorGUILayout.ObjectField("Front (XY)", _front, typeof(Texture2D), false);
            _right = (Texture2D)EditorGUILayout.ObjectField("Right (ZY)", _right, typeof(Texture2D), false);
            _top = (Texture2D)EditorGUILayout.ObjectField("Top (XZ)", _top, typeof(Texture2D), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Grid", EditorStyles.boldLabel);
            _heightVoxels = EditorGUILayout.IntSlider("Height (voxels)", _heightVoxels, 2, 256);
            _supersample = EditorGUILayout.IntSlider("Supersample / axis", _supersample, 1, 6);
            _solidFraction = EditorGUILayout.Slider("Solid vote fraction", _solidFraction, 0.05f, 1f);
            EditorGUILayout.LabelField("Sub-voxel offset (voxel units)");
            _offset.x = EditorGUILayout.Slider("  offset X", _offset.x, -1f, 1f);
            _offset.y = EditorGUILayout.Slider("  offset Y", _offset.y, -1f, 1f);
            _offset.z = EditorGUILayout.Slider("  offset Z", _offset.z, -1f, 1f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Segmentation", EditorStyles.boldLabel);
            _bgThreshold = EditorGUILayout.Slider("BG luminance threshold", _bgThreshold, 0.5f, 1f);
            _dilate = EditorGUILayout.IntSlider("Silhouette dilate (px)", _dilate, 0, 8);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Colour", EditorStyles.boldLabel);
            _colourMode = (ColourMode)EditorGUILayout.EnumPopup("Colour mode", _colourMode);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Orientation flips (fix handedness)", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Front", GUILayout.Width(48));
            _frontFlipU = GUILayout.Toggle(_frontFlipU, "flip U");
            _frontFlipV = GUILayout.Toggle(_frontFlipV, "flip V");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Right", GUILayout.Width(48));
            _rightFlipU = GUILayout.Toggle(_rightFlipU, "flip U");
            _rightFlipV = GUILayout.Toggle(_rightFlipV, "flip V");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Top", GUILayout.Width(48));
            _topFlipU = GUILayout.Toggle(_topFlipU, "flip U");
            _topFlipV = GUILayout.Toggle(_topFlipV, "flip V");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            _outputPath = EditorGUILayout.TextField("Output (.txt)", _outputPath);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_front == null || _right == null || _top == null))
            {
                if (GUILayout.Button("Generate & Export", GUILayout.Height(32)))
                    Generate();
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }

            if (_frontMask != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Segmentation preview (white = solid)", EditorStyles.boldLabel);
                EditorGUILayout.BeginHorizontal();
                DrawMask("front", _frontMask);
                DrawMask("right", _rightMask);
                DrawMask("top", _topMask);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }

        static void DrawMask(string label, Texture2D tex)
        {
            if (tex == null) return;
            EditorGUILayout.BeginVertical(GUILayout.Width(110));
            EditorGUILayout.LabelField(label, GUILayout.Width(110));
            Rect r = GUILayoutUtility.GetRect(100, 100, GUILayout.Width(100), GUILayout.Height(100));
            GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit);
            EditorGUILayout.EndVertical();
        }

        void Generate()
        {
            View front = View.Build(_front, _bgThreshold, _dilate, _frontFlipU, _frontFlipV);
            View right = View.Build(_right, _bgThreshold, _dilate, _rightFlipU, _rightFlipV);
            View top = View.Build(_top, _bgThreshold, _dilate, _topFlipU, _topFlipV);

            if (front == null || right == null || top == null)
            {
                _status = "Segmentation found no foreground in at least one image. Raise the BG threshold.";
                return;
            }

            // Grid dimensions from the ratio theory:
            //   height H = user value; front gives width aspect; right gives length (depth) aspect.
            int H = _heightVoxels;
            int W = Mathf.Max(1, Mathf.RoundToInt(H * (front.W / (float)front.H)));
            int L = Mathf.Max(1, Mathf.RoundToInt(H * (right.W / (float)right.H)));

            int S = Mathf.Max(1, _supersample);
            int total = S * S * S;
            float voteNeeded = total * _solidFraction;

            var sb = new StringBuilder();
            sb.Append("# Goxel 0.10.0\n");
            sb.Append("# One line per voxel\n");
            sb.Append("# X Y Z RRGGBB\n");

            int voxelCount = 0;
            for (int j = 0; j < H; j++)        // Y / up
            for (int k = 0; k < L; k++)        // Z / depth
            for (int i = 0; i < W; i++)        // X / right
            {
                int solid = 0;
                float cr = 0f, cg = 0f, cb = 0f;
                int colN = 0;

                for (int sy = 0; sy < S; sy++)
                for (int sz = 0; sz < S; sz++)
                for (int sx = 0; sx < S; sx++)
                {
                    float fx = i + (sx + 0.5f) / S + _offset.x;
                    float fy = j + (sy + 0.5f) / S + _offset.y;
                    float fz = k + (sz + 0.5f) / S + _offset.z;

                    float x01 = fx / W;
                    float y01 = fy / H;
                    float z01 = fz / L;
                    if (x01 < 0f || x01 > 1f || y01 < 0f || y01 > 1f || z01 < 0f || z01 > 1f)
                        continue;

                    bool occ = front.Foreground(x01, y01)
                            && right.Foreground(z01, y01)
                            && top.Foreground(x01, z01);
                    if (!occ) continue;

                    solid++;
                    Color c = SampleColour(front, right, top, x01, y01, z01);
                    cr += c.r; cg += c.g; cb += c.b; colN++;
                }

                if (solid < voteNeeded || colN == 0) continue;

                Color32 col = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(cr / colN * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(cg / colN * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(cb / colN * 255f), 0, 255),
                    255);

                // Remap to Goxel Z-up: Goxel(x, y, z) = world(X, Z, Y).
                sb.Append(i).Append(' ').Append(k).Append(' ').Append(j).Append(' ')
                  .Append(col.r.ToString("X2", CultureInfo.InvariantCulture))
                  .Append(col.g.ToString("X2", CultureInfo.InvariantCulture))
                  .Append(col.b.ToString("X2", CultureInfo.InvariantCulture))
                  .Append('\n');
                voxelCount++;
            }

            string fullPath = ResolvePath(_outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, sb.ToString());
            AssetDatabase.Refresh();

            _frontMask = front.BuildMaskPreview();
            _rightMask = right.BuildMaskPreview();
            _topMask = top.BuildMaskPreview();

            _status = $"Grid {W} x {H} x {L} (X*Y*Z). Wrote {voxelCount} voxels to:\n{fullPath}";
            Debug.Log("[VoxelSpike] " + _status.Replace("\n", " "));
        }

        Color SampleColour(View front, View right, View top, float x01, float y01, float z01)
        {
            bool ff = front.Foreground(x01, y01);
            bool rf = right.Foreground(z01, y01);
            bool tf = top.Foreground(x01, z01);
            Color cf = ff ? front.SampleColour(x01, y01) : Color.clear;
            Color cr = rf ? right.SampleColour(z01, y01) : Color.clear;
            Color ct = tf ? top.SampleColour(x01, z01) : Color.clear;

            switch (_colourMode)
            {
                case ColourMode.FrontPriority:
                    return ff ? cf : (rf ? cr : ct);

                case ColourMode.MostSaturated:
                {
                    Color best = Color.magenta;
                    float bestS = -1f;
                    if (ff) ConsiderSaturation(cf, ref best, ref bestS);
                    if (rf) ConsiderSaturation(cr, ref best, ref bestS);
                    if (tf) ConsiderSaturation(ct, ref best, ref bestS);
                    return best;
                }

                default: // MeanForeground
                {
                    Color sum = Color.clear;
                    int n = 0;
                    if (ff) { sum += cf; n++; }
                    if (rf) { sum += cr; n++; }
                    if (tf) { sum += ct; n++; }
                    return n > 0 ? sum * (1f / n) : Color.magenta;
                }
            }
        }

        static void ConsiderSaturation(Color c, ref Color best, ref float bestS)
        {
            float h, s, v;
            Color.RGBToHSV(c, out h, out s, out v);
            if (s > bestS) { bestS = s; best = c; }
        }

        static string ResolvePath(string path)
        {
            if (Path.IsPathRooted(path)) return path;
            string projectRoot = Directory.GetParent(Application.dataPath).FullName; // parent of Assets/
            return Path.Combine(projectRoot, path);
        }

        /// <summary>One cropped, segmented orthographic view. Pixel (0,0) is bottom-left (v=0 at bottom).</summary>
        class View
        {
            public int W;
            public int H;
            Color32[] _col;
            bool[] _fg;
            bool _flipU;
            bool _flipV;

            public static View Build(Texture2D tex, float threshold, int dilate, bool flipU, bool flipV)
            {
                int tw, th;
                Color32[] px = ReadPixels(tex, out tw, out th);

                bool[] fgFull = new bool[tw * th];
                int minX = tw, minY = th, maxX = -1, maxY = -1;
                for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                {
                    Color32 c = px[y * tw + x];
                    float lum = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) / 255f;
                    bool fg = c.a > 127 && lum < threshold;
                    fgFull[y * tw + x] = fg;
                    if (fg)
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
                if (maxX < 0) return null;

                int cw = maxX - minX + 1;
                int ch = maxY - minY + 1;
                var col = new Color32[cw * ch];
                var fg2 = new bool[cw * ch];
                for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    int sx = minX + x, sy = minY + y;
                    col[y * cw + x] = px[sy * tw + sx];
                    fg2[y * cw + x] = fgFull[sy * tw + sx];
                }
                if (dilate > 0) fg2 = Dilate(fg2, cw, ch, dilate);

                return new View { W = cw, H = ch, _col = col, _fg = fg2, _flipU = flipU, _flipV = flipV };
            }

            public bool Foreground(float u, float v)
            {
                if (_flipU) u = 1f - u;
                if (_flipV) v = 1f - v;
                int x = Mathf.Clamp(Mathf.RoundToInt(u * (W - 1)), 0, W - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt(v * (H - 1)), 0, H - 1);
                return _fg[y * W + x];
            }

            public Color SampleColour(float u, float v)
            {
                if (_flipU) u = 1f - u;
                if (_flipV) v = 1f - v;
                float fx = u * (W - 1);
                float fy = v * (H - 1);
                int x0 = Mathf.Clamp((int)fx, 0, W - 1);
                int y0 = Mathf.Clamp((int)fy, 0, H - 1);
                int x1 = Mathf.Min(x0 + 1, W - 1);
                int y1 = Mathf.Min(y0 + 1, H - 1);
                float tx = fx - x0, ty = fy - y0;
                Color c00 = _col[y0 * W + x0];
                Color c10 = _col[y0 * W + x1];
                Color c01 = _col[y1 * W + x0];
                Color c11 = _col[y1 * W + x1];
                return Color.Lerp(Color.Lerp(c00, c10, tx), Color.Lerp(c01, c11, tx), ty);
            }

            public Texture2D BuildMaskPreview()
            {
                var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
                var px = new Color32[W * H];
                for (int i = 0; i < px.Length; i++)
                    px[i] = _fg[i] ? new Color32(255, 255, 255, 255) : new Color32(40, 40, 40, 255);
                t.SetPixels32(px);
                t.Apply();
                return t;
            }

            static bool[] Dilate(bool[] src, int w, int h, int radius)
            {
                bool[] cur = src;
                for (int r = 0; r < radius; r++)
                {
                    var next = new bool[w * h];
                    for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        bool on = cur[y * w + x];
                        if (!on)
                        {
                            if (x > 0 && cur[y * w + x - 1]) on = true;
                            else if (x < w - 1 && cur[y * w + x + 1]) on = true;
                            else if (y > 0 && cur[(y - 1) * w + x]) on = true;
                            else if (y < h - 1 && cur[(y + 1) * w + x]) on = true;
                        }
                        next[y * w + x] = on;
                    }
                    cur = next;
                }
                return cur;
            }

            // Returns pixels with row 0 = bottom (v increases upward). Handles non-readable textures.
            static Color32[] ReadPixels(Texture2D tex, out int w, out int h)
            {
                w = tex.width;
                h = tex.height;
                if (tex.isReadable)
                    return tex.GetPixels32();

                var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                var prev = RenderTexture.active;
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                var read = new Texture2D(w, h, TextureFormat.RGBA32, false);
                read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                read.Apply();
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
                Color32[] outPx = read.GetPixels32();
                DestroyImmediate(read);
                return outPx;
            }
        }
    }
}
