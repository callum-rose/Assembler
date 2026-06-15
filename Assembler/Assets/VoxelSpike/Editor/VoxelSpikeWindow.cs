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
        enum PaletteMode { Curated, Variety, Modal }
        enum CuratedPalette { Endesga32, DawnBringer32, DawnBringer16, Pico8 }
        enum OutputMode { Colour, Parts }

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
        PaletteMode _paletteMode = PaletteMode.Curated;
        CuratedPalette _curatedPalette = CuratedPalette.Endesga32;

        // --- part decomposition: back-project flat label maps into per-voxel part ids ---
        OutputMode _outputMode = OutputMode.Colour;
        Texture2D _frontLabel, _rightLabel, _topLabel;

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
                _paletteMode = (PaletteMode)EditorGUILayout.EnumPopup("Palette selection", _paletteMode);
                if (_paletteMode == PaletteMode.Curated)
                    _curatedPalette = (CuratedPalette)EditorGUILayout.EnumPopup("Curated palette", _curatedPalette);
                _paletteSize = EditorGUILayout.IntSlider("Max colours (N)", _paletteSize, 1, 64);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Part decomposition", EditorStyles.boldLabel);
            _outputMode = (OutputMode)EditorGUILayout.EnumPopup("Output", _outputMode);
            if (_outputMode == OutputMode.Parts)
            {
                EditorGUILayout.HelpBox("Flat colour-coded part maps (one colour per part) matching each " +
                    "view's silhouette. Shape still comes from the source images; each voxel is coloured by " +
                    "its back-projected part. Per-view flips below apply to these too.", MessageType.None);
                _frontLabel = (Texture2D)EditorGUILayout.ObjectField("Front labels", _frontLabel, typeof(Texture2D), false);
                _rightLabel = (Texture2D)EditorGUILayout.ObjectField("Right labels", _rightLabel, typeof(Texture2D), false);
                _topLabel = (Texture2D)EditorGUILayout.ObjectField("Top labels", _topLabel, typeof(Texture2D), false);
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

            // --- Optional part labels: build label views and the discrete part palette ---
            bool partsMode = _outputMode == OutputMode.Parts;
            View frontLab = null, rightLab = null, topLab = null;
            Color[] labelColours = null;
            Vector3[] labelLab = null;
            if (partsMode)
            {
                if (!(_frontLabel && _rightLabel && _topLabel))
                {
                    _status = "Parts mode needs all three label maps (front/right/top).";
                    return;
                }
                frontLab = View.Build(_frontLabel, _bgThreshold, _dilate, _frontFlipU, _frontFlipV);
                rightLab = View.Build(_rightLabel, _bgThreshold, _dilate, _rightFlipU, _rightFlipV);
                topLab = View.Build(_topLabel, _bgThreshold, _dilate, _topFlipU, _topFlipV);
                if (frontLab == null || rightLab == null || topLab == null)
                {
                    _status = "Label maps have no foreground. Check the BG threshold.";
                    return;
                }
                labelColours = BuildLabelPalette(frontLab, rightLab, topLab);
                labelLab = new Vector3[labelColours.Length];
                for (int i = 0; i < labelColours.Length; i++) labelLab[i] = RgbToLab(labelColours[i]);
            }

            // --- Pass 3: colour each surviving voxel from the view(s) that can see it ---
            // Remap to Goxel Z-up as we go: Goxel(x, y, z) = world(X, Z, Y).
            var gx = new List<int>();
            var gy = new List<int>();
            var gz = new List<int>();
            var cols = new List<Color>();
            var seen = new List<bool>();                 // did any view actually colour this voxel?
            int[] gridToVoxel = Filled(W * H * L, -1);    // grid cell -> index into the lists above

            for (int j = 0; j < H; j++)
            for (int k = 0; k < L; k++)
            for (int i = 0; i < W; i++)
            {
                int cell = i + W * (j + H * k);
                if (!solid[cell]) continue;

                float x01 = (i + 0.5f) / W;
                float y01 = (j + 0.5f) / H;
                float z01 = (k + 0.5f) / L;

                Color col;
                bool isSeen;
                if (partsMode)
                {
                    int zc = i + W * j, xc = j + H * k, yc = i + W * k;
                    bool seeFront = k == frontMinZ[zc] || (_frontMirror && k == backMaxZ[zc]);
                    bool seeRight = i == rightMaxX[xc] || (_rightMirror && i == leftMinX[xc]);
                    bool seeTop = j == topMaxY[yc] || (_topMirror && j == botMinY[yc]);

                    // Each view that sees this voxel votes a part id (nearest label colour in Lab);
                    // skip a view whose label sample is background. Majority wins (front > right > top).
                    int idF = seeFront && frontLab.Foreground(x01, y01)
                        ? NearestLabIndex(labelLab, RgbToLab(frontLab.SampleNearest(x01, y01))) : -1;
                    int idR = seeRight && rightLab.Foreground(z01, y01)
                        ? NearestLabIndex(labelLab, RgbToLab(rightLab.SampleNearest(z01, y01))) : -1;
                    int idT = seeTop && topLab.Foreground(x01, z01)
                        ? NearestLabIndex(labelLab, RgbToLab(topLab.SampleNearest(x01, z01))) : -1;

                    int part = Mode3(idF, idR, idT);
                    isSeen = part >= 0;
                    col = isSeen ? labelColours[part] : Color.clear; // unseen -> filled in pass 5
                }
                else if (_colourMode == ColourMode.FlatMean)
                {
                    col = FlatMean(front, right, top, x01, y01, z01);
                    isSeen = true;
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

                    isSeen = n > 0;
                    col = isSeen ? sum * (1f / n) : Color.clear; // unseen -> filled in pass 5
                }

                gridToVoxel[cell] = cols.Count;
                gx.Add(i); gy.Add(k); gz.Add(j);
                cols.Add(col);
                seen.Add(isSeen);
            }

            int voxelCount = cols.Count;

            // --- Pass 4: quantise (palette built only from voxels a view actually saw) ---
            int paletteUsed = 0;
            if (_quantise && voxelCount > 0 && !partsMode)
            {
                int n = Mathf.Max(1, _paletteSize);
                Color[] palette;
                if (_paletteMode == PaletteMode.Curated)
                {
                    palette = CuratedColours(_curatedPalette);
                }
                else
                {
                    var seenCols = new List<Color>();
                    for (int v = 0; v < voxelCount; v++)
                        if (seen[v]) seenCols.Add(cols[v]);
                    if (seenCols.Count == 0) seenCols = cols; // degenerate: nothing seen

                    palette = _paletteMode == PaletteMode.Variety
                        ? BuildVarietyPalette(seenCols, n)
                        : BuildModalPalette(seenCols, n);
                }
                // Snap seen voxels to the palette in Lab, then cap the object to N colours.
                SnapAndLimit(cols, seen, palette, n, out paletteUsed);
            }

            // --- Pass 5: voxels no view saw inherit their nearest seen neighbour's (already
            // quantised) colour, via a multi-source BFS over the solid grid (6-connected). ---
            FillUnseen(cols, seen, gridToVoxel, gx, gy, gz, W, H, L);

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

            string detail = partsMode
                ? $", {labelColours.Length} parts"
                : _quantise ? $", {paletteUsed}-colour palette" : "";
            _status = $"Grid {W} x {H} x {L} (X*Y*Z). Wrote {voxelCount} voxels{detail} to:\n{fullPath}";
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

        // The discrete set of part colours across the three label maps: histogram the foreground
        // label pixels (binning merges antialiasing), then keep bins with enough population to be a
        // real region rather than an edge blend. Each kept colour is one part.
        static Color[] BuildLabelPalette(View a, View b, View c)
        {
            var all = new List<Color>();
            a.AccumulateForeground(all);
            b.AccumulateForeground(all);
            c.AccumulateForeground(all);

            BuildHistogram(all, out var means, out var counts);
            if (means.Count == 0) return new[] { Color.magenta };

            int totalPixels = 0;
            foreach (int cnt in counts) totalPixels += cnt;
            float threshold = Mathf.Max(4f, totalPixels * 0.004f); // drop boundary-blend bins

            var parts = new List<Color>();
            for (int i = 0; i < means.Count; i++)
                if (counts[i] >= threshold) parts.Add(means[i]);

            return parts.Count > 0 ? parts.ToArray() : new[] { means[0] };
        }

        // Most frequent non-negative id among three votes, ties broken by argument order (front first).
        static int Mode3(int a, int b, int c)
        {
            int best = -1, bestCount = 0;
            best = Vote(a, a, b, c, best, ref bestCount);
            best = Vote(b, a, b, c, best, ref bestCount);
            best = Vote(c, a, b, c, best, ref bestCount);
            return best;
        }

        static int Vote(int x, int a, int b, int c, int best, ref int bestCount)
        {
            if (x < 0) return best;
            int cnt = (a == x ? 1 : 0) + (b == x ? 1 : 0) + (c == x ? 1 : 0);
            if (cnt > bestCount) { bestCount = cnt; return x; }
            return best;
        }

        // Flood unseen voxels with their nearest seen voxel's colour. Multi-source BFS over the
        // solid grid (6-connected) seeded from every seen voxel, so each unseen voxel takes the
        // colour of the closest seen voxel by geodesic (within-object) distance.
        static void FillUnseen(List<Color> cols, List<bool> seen, int[] gridToVoxel,
                               List<int> gx, List<int> gy, List<int> gz, int W, int H, int L)
        {
            int count = cols.Count;
            var filled = new bool[count];
            var queue = new Queue<int>();
            for (int v = 0; v < count; v++)
                if (seen[v]) { filled[v] = true; queue.Enqueue(v); }

            if (queue.Count == 0 || queue.Count == count) return; // nothing seen, or nothing to fill

            int[] di = { 1, -1, 0, 0, 0, 0 };
            int[] dj = { 0, 0, 1, -1, 0, 0 };
            int[] dk = { 0, 0, 0, 0, 1, -1 };

            while (queue.Count > 0)
            {
                int v = queue.Dequeue();
                int i = gx[v], k = gy[v], j = gz[v]; // gx/gy/gz hold Goxel (X, Z, Y) = world (i, k, j)
                for (int d = 0; d < 6; d++)
                {
                    int ni = i + di[d], nj = j + dj[d], nk = k + dk[d];
                    if (ni < 0 || ni >= W || nj < 0 || nj >= H || nk < 0 || nk >= L) continue;
                    int nv = gridToVoxel[ni + W * (nj + H * nk)];
                    if (nv < 0 || filled[nv]) continue;
                    cols[nv] = cols[v];
                    filled[nv] = true;
                    queue.Enqueue(nv);
                }
            }
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

        // Snap every seen voxel to its nearest palette colour in CIELAB (perceptual), then cap
        // the object to maxColours: if more than that many palette entries end up used, keep a
        // variety-spread subset (farthest-point in Lab, seeded by the most populous used entry)
        // and remap the rest to their nearest kept colour. usedCount = distinct colours used.
        static void SnapAndLimit(List<Color> cols, List<bool> seen, Color[] palette, int maxColours, out int usedCount)
        {
            int p = palette.Length;
            var pLab = new Vector3[p];
            for (int i = 0; i < p; i++) pLab[i] = RgbToLab(palette[i]);

            int count = cols.Count;
            var idx = new int[count];
            var pop = new int[p];
            for (int v = 0; v < count; v++)
            {
                if (!seen[v]) { idx[v] = -1; continue; }
                int best = NearestLabIndex(pLab, RgbToLab(cols[v]));
                idx[v] = best;
                pop[best]++;
            }

            var used = new List<int>();
            for (int i = 0; i < p; i++)
                if (pop[i] > 0) used.Add(i);

            if (used.Count > maxColours)
            {
                HashSet<int> keep = SelectByVariety(used, pLab, pop, maxColours);
                var keptList = keep.ToList();
                var remap = new int[p];
                foreach (int u in used)
                {
                    if (keep.Contains(u)) { remap[u] = u; continue; }
                    int bestKept = keptList[0];
                    float bestD = float.MaxValue;
                    foreach (int kpt in keptList)
                    {
                        float d = (pLab[u] - pLab[kpt]).sqrMagnitude;
                        if (d < bestD) { bestD = d; bestKept = kpt; }
                    }
                    remap[u] = bestKept;
                }
                for (int v = 0; v < count; v++)
                    if (idx[v] >= 0) idx[v] = remap[idx[v]];
            }

            var finalUsed = new HashSet<int>();
            for (int v = 0; v < count; v++)
            {
                if (idx[v] < 0) continue;
                cols[v] = palette[idx[v]];
                finalUsed.Add(idx[v]);
            }
            usedCount = finalUsed.Count;
        }

        // Farthest-point (max-min) selection of k entries from candidates, in Lab, seeded by the
        // most populous candidate. Keeps chromatically distinct colours when capping the count.
        static HashSet<int> SelectByVariety(List<int> candidates, Vector3[] lab, int[] pop, int k)
        {
            int seed = candidates[0];
            foreach (int c in candidates)
                if (pop[c] > pop[seed]) seed = c;

            var chosen = new HashSet<int> { seed };
            var minD = new Dictionary<int, float>();
            foreach (int c in candidates) minD[c] = (lab[c] - lab[seed]).sqrMagnitude;

            while (chosen.Count < k)
            {
                int far = -1;
                float farD = -1f;
                foreach (int c in candidates)
                    if (!chosen.Contains(c) && minD[c] > farD) { farD = minD[c]; far = c; }
                if (far < 0 || farD <= 0f) break;

                chosen.Add(far);
                foreach (int c in candidates)
                {
                    float d = (lab[c] - lab[far]).sqrMagnitude;
                    if (d < minD[c]) minD[c] = d;
                }
            }
            return chosen;
        }

        static int NearestLabIndex(Vector3[] paletteLab, Vector3 lab)
        {
            int best = 0;
            float bestD = float.MaxValue;
            for (int i = 0; i < paletteLab.Length; i++)
            {
                float d = (paletteLab[i] - lab).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // sRGB Color -> CIELAB (D65). Linearises sRGB, converts to XYZ, then to Lab.
        static Vector3 RgbToLab(Color c)
        {
            float r = SrgbToLinear(c.r), g = SrgbToLinear(c.g), b = SrgbToLinear(c.b);
            float x = (r * 0.4124564f + g * 0.3575761f + b * 0.1804375f) / 0.95047f;
            float y = r * 0.2126729f + g * 0.7151522f + b * 0.0721750f;
            float z = (r * 0.0193339f + g * 0.1191920f + b * 0.9503041f) / 1.08883f;
            float fx = LabF(x), fy = LabF(y), fz = LabF(z);
            return new Vector3(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
        }

        static float SrgbToLinear(float u) =>
            u <= 0.04045f ? u / 12.92f : Mathf.Pow((u + 0.055f) / 1.055f, 2.4f);

        static float LabF(float t) =>
            t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;

        static Color[] CuratedColours(CuratedPalette p)
        {
            string[] hex = p switch
            {
                CuratedPalette.Pico8 => Pico8,
                CuratedPalette.DawnBringer16 => DawnBringer16,
                CuratedPalette.DawnBringer32 => DawnBringer32,
                _ => Endesga32,
            };
            var c = new Color[hex.Length];
            for (int i = 0; i < hex.Length; i++) c[i] = Hex(hex[i]);
            return c;
        }

        static Color Hex(string s)
        {
            int r = int.Parse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int g = int.Parse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            int b = int.Parse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            return new Color(r / 255f, g / 255f, b / 255f, 1f);
        }

        // Endesga 32 (EDG32) by Endesga — versatile modern game-art palette.
        static readonly string[] Endesga32 =
        {
            "be4a2f", "d77643", "ead4aa", "e4a672", "b86f50", "733e39", "3e2731", "a22633",
            "e43b44", "f77622", "feae34", "fee761", "63c74d", "3e8948", "265c42", "193c3e",
            "124e89", "0099db", "2ce8f5", "ffffff", "c0cbdc", "8b9bb4", "5a6988", "3a4466",
            "262b44", "181425", "ff0044", "68386c", "b55088", "f6757a", "e8b796", "c28569",
        };

        // DawnBringer 32 (DB32) — classic balanced 32-colour palette.
        static readonly string[] DawnBringer32 =
        {
            "000000", "222034", "45283c", "663931", "8f563b", "df7126", "d9a066", "eec39a",
            "fbf236", "99e550", "6abe30", "37946e", "4b692f", "524b24", "323c39", "3f3f74",
            "306082", "5b6ee1", "639bff", "5fcde4", "cbdbfc", "ffffff", "9badb7", "847e87",
            "696a6a", "595652", "76428a", "ac3232", "d95763", "d77bba", "8f974a", "8a6f30",
        };

        // DawnBringer 16 (DB16) — the original tight 16-colour palette.
        static readonly string[] DawnBringer16 =
        {
            "140c1c", "442434", "30346d", "4e4a4e", "854c30", "346524", "d04648", "757161",
            "597dce", "d27d2c", "8595a1", "6daa2c", "d2aa99", "6dc2ca", "dad45e", "deeed6",
        };

        // PICO-8 fantasy-console palette (16 colours).
        static readonly string[] Pico8 =
        {
            "000000", "1d2b53", "7e2553", "008751", "ab5236", "5f574f", "c2c3c7", "fff1e8",
            "ff004d", "ffa300", "ffec27", "00e436", "29adff", "83769c", "ff77a8", "ffccaa",
        };

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

            // Nearest-pixel colour — used for label maps so region boundaries stay hard (no blending).
            public Color SampleNearest(float u, float v)
            {
                if (_flipU) u = 1f - u;
                if (_flipV) v = 1f - v;
                int x = Mathf.Clamp(Mathf.RoundToInt(u * (W - 1)), 0, W - 1);
                int y = Mathf.Clamp(Mathf.RoundToInt(v * (H - 1)), 0, H - 1);
                return _col[y * W + x];
            }

            public void AccumulateForeground(List<Color> sink)
            {
                for (int i = 0; i < _fg.Length; i++)
                    if (_fg[i]) sink.Add(_col[i]);
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
