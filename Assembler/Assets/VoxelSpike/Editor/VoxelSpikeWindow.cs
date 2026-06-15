using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
        enum ColourMode { VisibleViews, FlatMean }
        enum PaletteMode { Variety, Modal }

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
        ColourMode _colourMode = ColourMode.VisibleViews;

        // --- per-view "opposite side is a mirror" (lets a view colour its far shell too) ---
        bool _frontMirror, _rightMirror, _topMirror;

        // --- palette quantisation: snap voxels to N representative colours ---
        bool _quantise = true;
        int _paletteSize = 16;
        PaletteMode _paletteMode = PaletteMode.Variety;

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
            using (new EditorGUI.DisabledScope(_colourMode != ColourMode.VisibleViews))
            {
                EditorGUILayout.LabelField("Opposite side is a mirror (borrow this view's colour):");
                EditorGUILayout.BeginHorizontal();
                _frontMirror = GUILayout.Toggle(_frontMirror, "front↔back");
                _rightMirror = GUILayout.Toggle(_rightMirror, "right↔left");
                _topMirror = GUILayout.Toggle(_topMirror, "top↔bottom");
                EditorGUILayout.EndHorizontal();
            }
            _quantise = EditorGUILayout.Toggle("Quantise palette", _quantise);
            using (new EditorGUI.DisabledScope(!_quantise))
            {
                _paletteSize = EditorGUILayout.IntSlider("Palette colours (N)", _paletteSize, 1, 64);
                _paletteMode = (PaletteMode)EditorGUILayout.EnumPopup("Palette selection", _paletteMode);
            }

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

            // --- Pass 1: occupancy (supersampled silhouette AND + majority vote) ---
            int S = Mathf.Max(1, _supersample);
            int total = S * S * S;
            float voteNeeded = total * _solidFraction;

            bool[] solid = new bool[W * H * L];
            for (int j = 0; j < H; j++)        // Y / up
            for (int k = 0; k < L; k++)        // Z / depth
            for (int i = 0; i < W; i++)        // X / right
            {
                int hits = 0;
                for (int sy = 0; sy < S; sy++)
                for (int sz = 0; sz < S; sz++)
                for (int sx = 0; sx < S; sx++)
                {
                    float x01 = (i + (sx + 0.5f) / S + _offset.x) / W;
                    float y01 = (j + (sy + 0.5f) / S + _offset.y) / H;
                    float z01 = (k + (sz + 0.5f) / S + _offset.z) / L;
                    if (x01 < 0f || x01 > 1f || y01 < 0f || y01 > 1f || z01 < 0f || z01 > 1f)
                        continue;
                    if (front.Foreground(x01, y01) && right.Foreground(z01, y01) && top.Foreground(x01, z01))
                        hits++;
                }
                if (hits >= voteNeeded)
                    solid[i + W * (j + H * k)] = true;
            }

            // --- Pass 2: per-column first-hit extents (occlusion-correct visibility) ---
            // Each ortho camera sees only the first solid voxel along its axis in a given column:
            //   front camera (low Z) -> smallest-Z solid per (x,y); back -> largest-Z;
            //   right camera (high X) -> largest-X per (y,z); left -> smallest-X;
            //   top camera (high Y) -> largest-Y per (x,z); bottom -> smallest-Y.
            int[] frontMinZ = Filled(W * H, int.MaxValue);   // column (x,y): index i + W*j
            int[] backMaxZ = Filled(W * H, -1);
            int[] rightMaxX = Filled(H * L, -1);             // column (y,z): index j + H*k
            int[] leftMinX = Filled(H * L, int.MaxValue);
            int[] topMaxY = Filled(W * L, -1);               // column (x,z): index i + W*k
            int[] botMinY = Filled(W * L, int.MaxValue);

            for (int j = 0; j < H; j++)
            for (int k = 0; k < L; k++)
            for (int i = 0; i < W; i++)
            {
                if (!solid[i + W * (j + H * k)]) continue;
                int zc = i + W * j;
                if (k < frontMinZ[zc]) frontMinZ[zc] = k;
                if (k > backMaxZ[zc]) backMaxZ[zc] = k;
                int xc = j + H * k;
                if (i > rightMaxX[xc]) rightMaxX[xc] = i;
                if (i < leftMinX[xc]) leftMinX[xc] = i;
                int yc = i + W * k;
                if (j > topMaxY[yc]) topMaxY[yc] = j;
                if (j < botMinY[yc]) botMinY[yc] = j;
            }

            // --- Pass 3: colour each surviving voxel from the view(s) that can see it ---
            // Remap to Goxel Z-up as we go: Goxel(x, y, z) = world(X, Z, Y).
            var gx = new List<int>();
            var gy = new List<int>();
            var gz = new List<int>();
            var cols = new List<Color>();

            for (int j = 0; j < H; j++)
            for (int k = 0; k < L; k++)
            for (int i = 0; i < W; i++)
            {
                if (!solid[i + W * (j + H * k)]) continue;

                float x01 = (i + 0.5f) / W;
                float y01 = (j + 0.5f) / H;
                float z01 = (k + 0.5f) / L;

                Color col;
                if (_colourMode == ColourMode.FlatMean)
                {
                    col = FlatMean(front, right, top, x01, y01, z01);
                }
                else
                {
                    int zc = i + W * j, xc = j + H * k, yc = i + W * k;
                    bool seeFront = k == frontMinZ[zc];
                    bool seeBack = _frontMirror && k == backMaxZ[zc];
                    bool seeRight = i == rightMaxX[xc];
                    bool seeLeft = _rightMirror && i == leftMinX[xc];
                    bool seeTop = j == topMaxY[yc];
                    bool seeBottom = _topMirror && j == botMinY[yc];

                    // One colour per contributing view; average when several see this voxel.
                    Color sum = Color.clear;
                    int n = 0;
                    if (seeFront || seeBack) { sum += front.SampleColour(x01, y01); n++; }
                    if (seeRight || seeLeft) { sum += right.SampleColour(z01, y01); n++; }
                    if (seeTop || seeBottom) { sum += top.SampleColour(x01, z01); n++; }

                    // No view sees it (interior, or an unseen far/concave face with mirror off):
                    // fall back to the flat mean so there are no colour holes.
                    col = n > 0 ? sum * (1f / n) : FlatMean(front, right, top, x01, y01, z01);
                }

                gx.Add(i); gy.Add(k); gz.Add(j);
                cols.Add(col);
            }

            int voxelCount = cols.Count;

            // --- Pass 4: quantise to the N most popular (modal) colours and snap voxels ---
            int paletteUsed = 0;
            if (_quantise && voxelCount > 0)
            {
                int n = Mathf.Max(1, _paletteSize);
                Color[] palette = _paletteMode == PaletteMode.Variety
                    ? BuildVarietyPalette(cols, n)
                    : BuildModalPalette(cols, n);
                paletteUsed = palette.Length;
                for (int v = 0; v < cols.Count; v++)
                    cols[v] = Nearest(palette, cols[v]);
            }

            // --- Write Goxel text ---
            var sb = new StringBuilder();
            sb.Append("# Goxel 0.10.0\n");
            sb.Append("# One line per voxel\n");
            sb.Append("# X Y Z RRGGBB\n");
            for (int v = 0; v < voxelCount; v++)
            {
                Color c = cols[v];
                int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
                int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
                int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
                sb.Append(gx[v]).Append(' ').Append(gy[v]).Append(' ').Append(gz[v]).Append(' ')
                  .Append(r.ToString("X2", CultureInfo.InvariantCulture))
                  .Append(g.ToString("X2", CultureInfo.InvariantCulture))
                  .Append(b.ToString("X2", CultureInfo.InvariantCulture))
                  .Append('\n');
            }

            string fullPath = ResolvePath(_outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, sb.ToString());
            AssetDatabase.Refresh();

            _frontMask = front.BuildMaskPreview();
            _rightMask = right.BuildMaskPreview();
            _topMask = top.BuildMaskPreview();

            string quant = _quantise ? $", {paletteUsed}-colour palette" : "";
            _status = $"Grid {W} x {H} x {L} (X*Y*Z). Wrote {voxelCount} voxels{quant} to:\n{fullPath}";
            Debug.Log("[VoxelSpike] " + _status.Replace("\n", " "));
        }

        // Whole-column mean of the views whose silhouette covers this point. Used as the
        // FlatMean colour mode and as the fallback for voxels no view can see directly.
        static Color FlatMean(View front, View right, View top, float x01, float y01, float z01)
        {
            Color sum = Color.clear;
            int n = 0;
            if (front.Foreground(x01, y01)) { sum += front.SampleColour(x01, y01); n++; }
            if (right.Foreground(z01, y01)) { sum += right.SampleColour(z01, y01); n++; }
            if (top.Foreground(x01, z01)) { sum += top.SampleColour(x01, z01); n++; }
            return n > 0 ? sum * (1f / n) : Color.magenta;
        }

        static int[] Filled(int n, int value)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = value;
            return a;
        }

        // Coarse-bin the voxel colours into an RGB histogram (4 bits/channel) and return one
        // candidate per occupied bin: its mean colour and population. Binning merges near-
        // identical colours so both palette strategies work on distinct colours, not raw noise.
        static void BuildHistogram(List<Color> colours, out List<Color> means, out List<int> counts)
        {
            const int levels = 16; // 4 bits per channel -> 16^3 = 4096 bins
            var count = new Dictionary<int, int>();
            var sumR = new Dictionary<int, float>();
            var sumG = new Dictionary<int, float>();
            var sumB = new Dictionary<int, float>();

            foreach (Color c in colours)
            {
                int r = Mathf.Clamp((int)(c.r * (levels - 1) + 0.5f), 0, levels - 1);
                int g = Mathf.Clamp((int)(c.g * (levels - 1) + 0.5f), 0, levels - 1);
                int b = Mathf.Clamp((int)(c.b * (levels - 1) + 0.5f), 0, levels - 1);
                int key = (r * levels + g) * levels + b;
                count.TryGetValue(key, out int prev);
                count[key] = prev + 1;
                sumR.TryGetValue(key, out float sr);
                sumG.TryGetValue(key, out float sg);
                sumB.TryGetValue(key, out float sb);
                sumR[key] = sr + c.r;
                sumG[key] = sg + c.g;
                sumB[key] = sb + c.b;
            }

            means = new List<Color>(count.Count);
            counts = new List<int>(count.Count);
            foreach (var kv in count)
            {
                float inv = 1f / kv.Value;
                means.Add(new Color(sumR[kv.Key] * inv, sumG[kv.Key] * inv, sumB[kv.Key] * inv, 1f));
                counts.Add(kv.Value);
            }
        }

        // Popularity ("modal"): the N most-populated bins. Biased toward large flat regions —
        // small distinct features (a single-voxel eye) get dropped.
        static Color[] BuildModalPalette(List<Color> colours, int n)
        {
            BuildHistogram(colours, out var means, out var counts);
            return Enumerable.Range(0, means.Count)
                .OrderByDescending(i => counts[i])
                .Take(n)
                .Select(i => means[i])
                .ToArray();
        }

        // Variety (farthest-point / max-min): seed with the most populous bin, then repeatedly
        // add the candidate colour furthest from everything already chosen. Captures the colour
        // diversity of the model, so chromatically distinct outliers (the eye) survive even when
        // they cover very few voxels. Near-duplicates are already merged by the histogram.
        static Color[] BuildVarietyPalette(List<Color> colours, int n)
        {
            BuildHistogram(colours, out var means, out var counts);
            int m = means.Count;
            if (m <= n) return means.ToArray();

            int seed = 0;
            for (int i = 1; i < m; i++)
                if (counts[i] > counts[seed]) seed = i;

            var chosen = new List<int>(n) { seed };
            var minDist = new float[m];
            for (int i = 0; i < m; i++) minDist[i] = SqrDist(means[i], means[seed]);

            while (chosen.Count < n)
            {
                int far = 0;
                for (int i = 1; i < m; i++)
                    if (minDist[i] > minDist[far]) far = i;
                if (minDist[far] <= 0f) break; // remaining candidates duplicate the chosen set

                chosen.Add(far);
                for (int i = 0; i < m; i++)
                {
                    float d = SqrDist(means[i], means[far]);
                    if (d < minDist[i]) minDist[i] = d;
                }
            }

            return chosen.Select(i => means[i]).ToArray();
        }

        static float SqrDist(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return dr * dr + dg * dg + db * db;
        }

        static Color Nearest(Color[] palette, Color c)
        {
            Color best = c;
            float bestD = float.MaxValue;
            for (int i = 0; i < palette.Length; i++)
            {
                float dr = palette[i].r - c.r, dg = palette[i].g - c.g, db = palette[i].b - c.b;
                float d = dr * dr + dg * dg + db * db;
                if (d < bestD) { bestD = d; best = palette[i]; }
            }
            return best;
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
