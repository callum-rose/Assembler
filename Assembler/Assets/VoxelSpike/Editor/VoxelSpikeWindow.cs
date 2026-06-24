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
    /// Spike: reconstruct a voxel model from a turnaround sheet via an AI-in-the-loop pipeline.
    ///
    /// Stage 1 — the sheet holds a front (left) and a right (right) view sharing one background.
    /// We separate the two objects, carve a high-res visual hull from front ∧ right (they share the
    /// height axis), colour it by back-projection, and export a top-down "best guess" PNG.
    ///
    /// Stage 2 — that top guess is refined by an AI (more shape + colour) and loaded back in. We
    /// re-carve with the edited top as the third silhouette (subtractive, down the Y axis), recolour
    /// the top-visible voxels, then down-res the hull to an editable resolution and export Goxel text.
    ///
    /// Standalone throwaway rig (no dependency on the project's Voxels assemblies). Conventions:
    ///   world X = right, Y = up, Z = depth(front->back).
    ///   Front view sees the XY plane, Right view sees the ZY plane, Top view sees the XZ plane.
    ///   Output is remapped to Goxel's Z-up: Goxel(x, y, z) = world(X, Z, Y).
    /// </summary>
    public class VoxelSpikeWindow : EditorWindow
    {
        enum PaletteMode { Curated, Variety, Modal }
        enum CuratedPalette { Endesga32, DawnBringer32, DawnBringer16, Pico8 }

        // --- inputs ---
        Texture2D _turnaround;   // front (left) + right (right) in one sheet
        Texture2D _topEdited;    // AI-refined top view (optional; only used by stage 2)

        // --- resolution ---
        int _carveHeight = 64;         // voxel height for the high-res hull (width/length from aspect)
        int _outputHeight = 24;        // voxel height after down-res (editable resolution)
        int _supersample = 3;          // samples per voxel per axis (S => S^3 samples)
        float _solidFraction = 0.5f;   // fraction of sub-samples that must pass to keep a voxel
        float _downresFill = 0.5f;     // a down-res block is solid if this fraction of it is filled

        // --- segmentation ---
        float _bgThreshold = 0.2f;     // foreground if RGB distance from perimeter-average background exceeds this
        int _dilate = 0;               // silhouette dilation in source pixels (forgiveness for view disagreement)
        bool _swapViews;               // sheet has front on the right instead of the left

        // --- per-view "opposite side is a mirror" (lets a view colour its far shell too) ---
        bool _frontMirror, _rightMirror, _topMirror;

        // --- palette quantisation: snap voxels to N representative colours ---
        bool _quantise = true;
        int _paletteSize = 16;
        PaletteMode _paletteMode = PaletteMode.Curated;
        CuratedPalette _curatedPalette = CuratedPalette.Endesga32;

        // --- reference sheet ---
        int _featureGuides = 2;        // interior feature projectors per view (0 = silhouette box only)
        bool _depthArcs = true;        // transfer side->top depth via quarter arcs (else miter L-paths)
        bool _landingTicks = true;     // mark where each projector lands on the top view

        // --- orientation flips (handedness ambiguity per view) ---
        bool _frontFlipU, _frontFlipV;
        bool _rightFlipU, _rightFlipV;
        bool _topFlipU, _topFlipV;

        // --- output paths ---
        string _topGuessPath = "Assets/VoxelSpike/top_guess.png";       // pristine refine target
        string _refSheetPath = "Assets/VoxelSpike/reference_sheet.png"; // front|right|top context for the AI
        string _hullPath = "Assets/VoxelSpike/hull.txt";                // stage-1 carved hull, as-is (high res)
        string _outputPath = "Assets/VoxelSpike/output.txt";

        // --- last-run state / preview ---
        string _status = "";
        Texture2D _frontMask, _rightMask, _topMask, _topGuessPreview;
        Vector2 _scroll;

        [MenuItem("Assembler/Voxel Spike (Turnaround)")]
        static void Open()
        {
            GetWindow<VoxelSpikeWindow>("Voxel Spike");
        }

        void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Turnaround sheet: front and right views side by side, sharing one " +
                "background, separated by a gap. AI top: the refined top-down image for stage 2 (optional).",
                MessageType.None);
            _turnaround = (Texture2D)EditorGUILayout.ObjectField("Turnaround (F+R)", _turnaround, typeof(Texture2D), false);
            _swapViews = EditorGUILayout.Toggle("Front is on the right", _swapViews);
            _topEdited = (Texture2D)EditorGUILayout.ObjectField("AI top (XZ)", _topEdited, typeof(Texture2D), false);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Resolution", EditorStyles.boldLabel);
            _carveHeight = EditorGUILayout.IntSlider("Carve height (voxels)", _carveHeight, 8, 160);
            _outputHeight = EditorGUILayout.IntSlider("Output height (voxels)", _outputHeight, 4, 96);
            _supersample = EditorGUILayout.IntSlider("Supersample / axis", _supersample, 1, 6);
            _solidFraction = EditorGUILayout.Slider("Solid vote fraction", _solidFraction, 0.05f, 1f);
            _downresFill = EditorGUILayout.Slider("Down-res fill fraction", _downresFill, 0.05f, 1f);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Segmentation", EditorStyles.boldLabel);
            _bgThreshold = EditorGUILayout.Slider("BG colour tolerance", _bgThreshold, 0f, 1f);
            _dilate = EditorGUILayout.IntSlider("Silhouette dilate (px)", _dilate, 0, 8);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Colour", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Opposite side is a mirror (borrow this view's colour):");
            EditorGUILayout.BeginHorizontal();
            _frontMirror = GUILayout.Toggle(_frontMirror, "front↔back");
            _rightMirror = GUILayout.Toggle(_rightMirror, "right↔left");
            _topMirror = GUILayout.Toggle(_topMirror, "top↔bottom");
            EditorGUILayout.EndHorizontal();
            _quantise = EditorGUILayout.Toggle("Quantise palette", _quantise);
            using (new EditorGUI.DisabledScope(!_quantise))
            {
                _paletteMode = (PaletteMode)EditorGUILayout.EnumPopup("Palette selection", _paletteMode);
                if (_paletteMode == PaletteMode.Curated)
                    _curatedPalette = (CuratedPalette)EditorGUILayout.EnumPopup("Curated palette", _curatedPalette);
                _paletteSize = EditorGUILayout.IntSlider("Max colours (N)", _paletteSize, 1, 64);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Orientation flips (fix handedness)", EditorStyles.boldLabel);
            DrawFlips("Front", ref _frontFlipU, ref _frontFlipV);
            DrawFlips("Right", ref _rightFlipU, ref _rightFlipV);
            DrawFlips("Top", ref _topFlipU, ref _topFlipV);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reference sheet", EditorStyles.boldLabel);
            _featureGuides = EditorGUILayout.IntSlider("Feature guides / view", _featureGuides, 0, 6);
            _depthArcs = EditorGUILayout.Toggle("Depth via arcs", _depthArcs);
            _landingTicks = EditorGUILayout.Toggle("Landing ticks", _landingTicks);

            EditorGUILayout.Space();
            _topGuessPath = EditorGUILayout.TextField("Top guess (.png)", _topGuessPath);
            _refSheetPath = EditorGUILayout.TextField("Reference sheet (.png)", _refSheetPath);
            _hullPath = EditorGUILayout.TextField("Hull (.txt)", _hullPath);
            _outputPath = EditorGUILayout.TextField("Output (.txt)", _outputPath);

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_turnaround == null))
            {
                if (GUILayout.Button("1 · Carve F+R → top guess", GUILayout.Height(30)))
                    RunStage1();
                if (GUILayout.Button("2 · Carve → down-res → Goxel", GUILayout.Height(30)))
                    RunStage2();
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }

            if (_topGuessPreview != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Top guess (front edge at bottom)", EditorStyles.boldLabel);
                Rect tr = GUILayoutUtility.GetRect(160, 160, GUILayout.Width(160), GUILayout.Height(160));
                GUI.DrawTexture(tr, _topGuessPreview, ScaleMode.ScaleToFit);
            }

            if (_frontMask != null || _rightMask != null || _topMask != null)
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

        static void DrawFlips(string label, ref bool flipU, ref bool flipV)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(48));
            flipU = GUILayout.Toggle(flipU, "flip U");
            flipV = GUILayout.Toggle(flipV, "flip V");
            EditorGUILayout.EndHorizontal();
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

        // ---------------------------------------------------------------- stages

        // Stage 1: split the sheet, carve the front+right hull, export the top-down colour guess.
        void RunStage1()
        {
            View[] fr = BuildFrontRight(out string err);
            if (fr == null) { _status = err; return; }

            Hull h = Carve(_carveHeight, fr[0], fr[1], null);

            Texture2D topClean = RenderTop(h, TopScale(h.W, h.L)); // pristine refine target (re-imported in stage 2)

            string topPath = ResolvePath(_topGuessPath);
            Directory.CreateDirectory(Path.GetDirectoryName(topPath));
            File.WriteAllBytes(topPath, topClean.EncodeToPNG());

            Texture2D sheet = BuildProjectionSheet(h, fr[0], fr[1], _featureGuides, _depthArcs, _landingTicks);
            string sheetPath = ResolvePath(_refSheetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(sheetPath));
            File.WriteAllBytes(sheetPath, sheet.EncodeToPNG());

            string hullPath = ResolvePath(_hullPath);
            Directory.CreateDirectory(Path.GetDirectoryName(hullPath));
            File.WriteAllText(hullPath, BuildGoxel(h.gx, h.gy, h.gz, h.cols));
            AssetDatabase.Refresh();

            _topGuessPreview = sheet;
            _frontMask = fr[0].BuildMaskPreview();
            _rightMask = fr[1].BuildMaskPreview();
            _topMask = null;
            _status = $"Stage 1: carved {h.cols.Count} F+R voxels at {h.W} x {h.H} x {h.L}.\n" +
                      $"Hull -> {hullPath}\nTop guess -> {topPath}\nProjection sheet -> {sheetPath}\n" +
                      "Sheet is a third-angle projection (front lower-left, side right, rough top above " +
                      "front; equal gutters, 45° miter for depth reflection). Ask the AI to redraw the top " +
                      "view using the projectors, then crop it out and load as 'AI top' for stage 2.";
            Debug.Log("[VoxelSpike] " + _status.Replace("\n", " "));
        }

        // Stage 2: re-carve (front ∧ right ∧ edited-top), down-res, write Goxel.
        void RunStage2()
        {
            View[] fr = BuildFrontRight(out string err);
            if (fr == null) { _status = err; return; }

            View top = null;
            if (_topEdited != null)
            {
                // Largest component only, so a stray mark/annotation the AI leaves doesn't skew registration.
                top = View.BuildLargest(_topEdited, _bgThreshold, _dilate, _topFlipU, _topFlipV);
                if (top == null) { _status = "AI top image has no foreground. Adjust BG tolerance."; return; }
            }

            Hull h = Carve(_carveHeight, fr[0], fr[1], top);
            Downres(h, _outputHeight, _downresFill, out var gx, out var gy, out var gz, out var cols);

            string path = ResolvePath(_outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, BuildGoxel(gx, gy, gz, cols));
            AssetDatabase.Refresh();

            _frontMask = fr[0].BuildMaskPreview();
            _rightMask = fr[1].BuildMaskPreview();
            _topMask = top != null ? top.BuildMaskPreview() : null;

            string note = top == null ? "  (no AI top loaded — front+right only)" : "";
            _status = $"Stage 2: {h.cols.Count} carved voxels -> {cols.Count} after down-res.{note}\nWrote {path}";
            Debug.Log("[VoxelSpike] " + _status.Replace("\n", " "));
        }

        View[] BuildFrontRight(out string err)
        {
            err = "";
            if (_turnaround == null) { err = "Load a turnaround sheet (front + right)."; return null; }
            View[] v = View.SplitTwo(_turnaround, _bgThreshold, _dilate, _swapViews,
                _frontFlipU, _frontFlipV, _rightFlipU, _rightFlipV);
            if (v == null)
                err = "Couldn't find two separate objects in the sheet. Ensure the front and right views " +
                      "are side by side with a clear gap, the object doesn't touch the frame edges, and the " +
                      "BG tolerance is set so both are picked up.";
            return v;
        }

        // ---------------------------------------------------------------- carve

        /// <summary>A coloured voxel hull in world space plus its grid->voxel index and Goxel coords.</summary>
        class Hull
        {
            public int W, H, L;
            public bool[] solid;        // [i + W*(j + H*k)]
            public int[] gridToVoxel;   // grid cell -> index into the lists below (-1 if empty)
            public List<int> gx, gy, gz; // Goxel coords: (X=i, Y=k depth, Z=j height)
            public List<Color> cols;
        }

        // Supersampled silhouette intersection + occlusion-correct colour back-projection. When `top`
        // is null only front ∧ right constrain occupancy (and top contributes no colour).
        Hull Carve(int height, View front, View right, View top)
        {
            int H = Mathf.Max(1, height);
            int W = Mathf.Max(1, Mathf.RoundToInt(H * (front.W / (float)front.H)));
            int L = Mathf.Max(1, Mathf.RoundToInt(H * (right.W / (float)right.H)));

            // --- Pass 1: occupancy (supersampled silhouette AND + majority vote) ---
            int S = Mathf.Max(1, _supersample);
            float voteNeeded = S * S * S * _solidFraction;

            bool[] solid = new bool[W * H * L];
            for (int j = 0; j < H; j++)
            for (int k = 0; k < L; k++)
            for (int i = 0; i < W; i++)
            {
                int hits = 0;
                for (int sy = 0; sy < S; sy++)
                for (int sz = 0; sz < S; sz++)
                for (int sx = 0; sx < S; sx++)
                {
                    float x01 = (i + (sx + 0.5f) / S) / W;
                    float y01 = (j + (sy + 0.5f) / S) / H;
                    float z01 = (k + (sz + 0.5f) / S) / L;
                    if (front.Foreground(x01, y01) && right.Foreground(z01, y01) &&
                        (top == null || top.Foreground(x01, z01)))
                        hits++;
                }
                if (hits >= voteNeeded)
                    solid[i + W * (j + H * k)] = true;
            }

            // --- Pass 2: per-column first-hit extents (occlusion-correct visibility) ---
            int[] frontMinZ = Filled(W * H, int.MaxValue);   // column (x,y): i + W*j
            int[] backMaxZ = Filled(W * H, -1);
            int[] rightMaxX = Filled(H * L, -1);             // column (y,z): j + H*k
            int[] leftMinX = Filled(H * L, int.MaxValue);
            int[] topMaxY = Filled(W * L, -1);               // column (x,z): i + W*k
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
            var gx = new List<int>();
            var gy = new List<int>();
            var gz = new List<int>();
            var cols = new List<Color>();
            var seen = new List<bool>();
            int[] gridToVoxel = Filled(W * H * L, -1);

            for (int j = 0; j < H; j++)
            for (int k = 0; k < L; k++)
            for (int i = 0; i < W; i++)
            {
                int cell = i + W * (j + H * k);
                if (!solid[cell]) continue;

                float x01 = (i + 0.5f) / W;
                float y01 = (j + 0.5f) / H;
                float z01 = (k + 0.5f) / L;

                int zc = i + W * j, xc = j + H * k, yc = i + W * k;
                bool seeFront = k == frontMinZ[zc];
                bool seeBack = _frontMirror && k == backMaxZ[zc];
                bool seeRight = i == rightMaxX[xc];
                bool seeLeft = _rightMirror && i == leftMinX[xc];
                bool seeTop = top != null && j == topMaxY[yc];
                bool seeBottom = top != null && _topMirror && j == botMinY[yc];

                Color sum = Color.clear;
                int n = 0;
                if (seeFront || seeBack) { sum += front.SampleColour(x01, y01); n++; }
                if (seeRight || seeLeft) { sum += right.SampleColour(z01, y01); n++; }
                if (seeTop || seeBottom) { sum += top.SampleColour(x01, z01); n++; }

                bool isSeen = n > 0;
                gridToVoxel[cell] = cols.Count;
                gx.Add(i); gy.Add(k); gz.Add(j);
                cols.Add(isSeen ? sum * (1f / n) : Color.clear); // unseen -> filled in pass 5
                seen.Add(isSeen);
            }

            int voxelCount = cols.Count;

            // --- Pass 4: quantise (palette built only from voxels a view actually saw) ---
            if (_quantise && voxelCount > 0)
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
                    if (seenCols.Count == 0) seenCols = cols;

                    palette = _paletteMode == PaletteMode.Variety
                        ? BuildVarietyPalette(seenCols, n)
                        : BuildModalPalette(seenCols, n);
                }
                SnapAndLimit(cols, seen, palette, n, out _);
            }

            // --- Pass 5: unseen voxels inherit their nearest seen neighbour's colour (BFS) ---
            FillUnseen(cols, seen, gridToVoxel, gx, gy, gz, W, H, L);

            return new Hull
            {
                W = W, H = H, L = L, solid = solid, gridToVoxel = gridToVoxel,
                gx = gx, gy = gy, gz = gz, cols = cols
            };
        }

        // ---------------------------------------------------------------- top render

        static readonly Color32 Neutral = new Color32(225, 228, 232, 255); // flat background
        static readonly Color32 Guide = new Color32(168, 178, 196, 255);   // datum / projection lines
        static readonly Color32 Miter = new Color32(206, 120, 80, 255);    // 45° reflection diagonal

        // Integer upscale so the larger XZ side lands near 256 px (crisp nearest-neighbour pixels).
        static int TopScale(int w, int l) => Mathf.Max(1, Mathf.RoundToInt(256f / Mathf.Max(w, l)));

        // Orthographic top-down render on a flat neutral background: each (x, z) column shows its
        // topmost voxel's colour. Row 0 (bottom) = z = 0 = the front edge, so re-import re-registers.
        static Texture2D RenderTop(Hull h, int scale)
        {
            int W = h.W, H = h.H, L = h.L;
            var col = new Color[W * L];
            var occ = new bool[W * L];
            for (int k = 0; k < L; k++)
            for (int i = 0; i < W; i++)
            {
                for (int j = H - 1; j >= 0; j--)
                {
                    int vox = h.gridToVoxel[i + W * (j + H * k)];
                    if (vox >= 0) { col[k * W + i] = h.cols[vox]; occ[k * W + i] = true; break; }
                }
            }

            int m = Mathf.Max(6, scale * 2);
            int sw = W * scale, sl = L * scale;
            int tw = sw + 2 * m, tht = sl + 2 * m;
            var img = new Color32[tw * tht];
            for (int p = 0; p < img.Length; p++) img[p] = Neutral;

            for (int y = 0; y < sl; y++)
            for (int x = 0; x < sw; x++)
            {
                int i = x / scale, k = y / scale;
                if (!occ[k * W + i]) continue;
                img[(y + m) * tw + (x + m)] = col[k * W + i];
            }

            var t = new Texture2D(tw, tht, TextureFormat.RGBA32, false);
            t.SetPixels32(img);
            t.Apply();
            return t;
        }

        // Engineering-style projection sheet for the AI: the FRONT view (lower-left), the SIDE view to
        // its right, and the rough TOP guess directly above it. The vertical gutter (front->top) equals
        // the horizontal gutter (front->side); all three views meet at the front view's top-right corner,
        // through which a 45° miter line is drawn so depth reflects from the side view into the top.
        static Texture2D BuildProjectionSheet(Hull h, View front, View side, int featureGuides,
            bool depthArcs, bool landingTicks)
        {
            int W = h.W, H = h.H, L = h.L;
            int s = Mathf.Max(8, Mathf.RoundToInt(880f / Mathf.Max(Mathf.Max(W, H), L))); // pixels / voxel (4x res)
            int Wp = W * s, Hp = H * s, Lp = L * s;
            int g = Mathf.Max(12, s * 6);   // gutter, equal in both directions
            int m = Mathf.Max(10, s * 3);   // outer margin

            int cw = 2 * m + Wp + g + Lp;
            int ch = 2 * m + Hp + g + Lp;
            var img = new Color32[cw * ch];
            for (int p = 0; p < img.Length; p++) img[p] = Neutral;

            // panels, all at the same pixels-per-voxel scale so widths/heights/depths line up
            Color32[] frontPx = ScaleNearest(front.BuildColourPreview(), Wp, Hp).GetPixels32();
            Color32[] sidePx = ScaleNearest(side.BuildColourPreview(), Lp, Hp).GetPixels32();
            Color32[] topPx = RenderTopRaw(h, s).GetPixels32();

            Blit(img, cw, frontPx, Wp, Hp, m, m);                 // front: lower-left
            Blit(img, cw, sidePx, Lp, Hp, m + Wp + g, m);         // side: right of front (shares height)
            Blit(img, cw, topPx, Wp, Lp, m, m + Hp + g);          // top: above front (shares width)

            // shared corner = front view's top-right; datum lines along its right/top edges + 45° miter
            int cornerX = m + Wp, cornerY = m + Hp;
            int thick = Mathf.Max(1, s / 6); // halved vs the resolution bump → finer datum/miter lines
            DrawVLine(img, cw, ch, cornerX, m, m + Hp + g + Lp, Guide, thick);
            DrawHLine(img, cw, ch, cornerY, m, m + Wp + g + Lp, Guide, thick);
            DrawDiag(img, cw, ch, cornerX, cornerY, g + Lp, Miter, thick);

            int tick = Mathf.Max(3, s / 2);

            // WIDTH landmarks (silhouette extents + strongest front-view boundaries) rise straight up
            // from the front view into the top.
            foreach (int idx in FeatureLandmarks(front, featureGuides))
            {
                int wx = Mathf.RoundToInt(idx / (float)Mathf.Max(1, front.W - 1) * Wp);
                DrawVLine(img, cw, ch, m + wx, m, m + Hp + g + Lp, Guide, 1);
                if (landingTicks) DrawTick(img, cw, ch, m + wx, m + Hp + g, tick, Miter);
            }

            // DEPTH landmarks transfer from the side view to the top: either a quarter arc swung about
            // the shared corner, or the classic reflect-off-the-45°-miter L-path.
            foreach (int idx in FeatureLandmarks(side, featureGuides))
            {
                int dz = Mathf.RoundToInt(idx / (float)Mathf.Max(1, side.W - 1) * Lp);
                int featX = m + Wp + g + dz; // this depth's column in the side view
                int landY = m + Hp + g + dz; // this depth's row in the top view
                if (depthArcs)
                {
                    DrawVLine(img, cw, ch, featX, m, cornerY, Guide, 1);       // mark the feature in the side view
                    DrawArc(img, cw, ch, cornerX, cornerY, g + dz, Guide, 1);  // swing the depth up to the datum
                    DrawHLine(img, cw, ch, landY, m, cornerX, Guide, 1);       // into the top view
                }
                else
                {
                    DrawVLine(img, cw, ch, featX, m, landY, Guide, 1);         // side feature up to the miter
                    DrawHLine(img, cw, ch, landY, m, featX, Guide, 1);         // miter across into the top
                }
                if (landingTicks) DrawTick(img, cw, ch, cornerX, landY, tick, Miter);
            }

            var t = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
            t.SetPixels32(img);
            t.Apply();
            return t;
        }

        // Raw top-down render (no margin) at `s` pixels per voxel: topmost voxel colour, front (z=0) at
        // the bottom row, on the flat background.
        static Texture2D RenderTopRaw(Hull h, int s)
        {
            int W = h.W, H = h.H, L = h.L;
            int sw = W * s, sl = L * s;
            var px = new Color32[sw * sl];
            for (int p = 0; p < px.Length; p++) px[p] = Neutral;
            for (int k = 0; k < L; k++)
            for (int i = 0; i < W; i++)
            {
                Color c = Neutral;
                bool occ = false;
                for (int j = H - 1; j >= 0; j--)
                {
                    int vox = h.gridToVoxel[i + W * (j + H * k)];
                    if (vox >= 0) { c = h.cols[vox]; occ = true; break; }
                }
                if (!occ) continue;
                for (int yy = 0; yy < s; yy++)
                for (int xx = 0; xx < s; xx++)
                    px[(k * s + yy) * sw + (i * s + xx)] = c;
            }
            var t = new Texture2D(sw, sl, TextureFormat.RGBA32, false);
            t.SetPixels32(px);
            t.Apply();
            return t;
        }

        static void Blit(Color32[] dst, int dstW, Color32[] src, int sw, int sh, int ox, int oy)
        {
            for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
                dst[(oy + y) * dstW + (ox + x)] = src[y * sw + x];
        }

        static void DrawVLine(Color32[] img, int w, int h, int x, int y0, int y1, Color32 c, int thick)
        {
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(h - 1, y1); y++)
            for (int t = -thick; t <= thick; t++)
            {
                int xx = x + t;
                if (xx >= 0 && xx < w) img[y * w + xx] = c;
            }
        }

        static void DrawHLine(Color32[] img, int w, int h, int y, int x0, int x1, Color32 c, int thick)
        {
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(w - 1, x1); x++)
            for (int t = -thick; t <= thick; t++)
            {
                int yy = y + t;
                if (yy >= 0 && yy < h) img[yy * w + x] = c;
            }
        }

        static void DrawDiag(Color32[] img, int w, int h, int x0, int y0, int len, Color32 c, int thick)
        {
            for (int d = 0; d <= len; d++)
            {
                int x = x0 + d, y = y0 + d;
                for (int t = -thick; t <= thick; t++)
                {
                    int xx = x + t;
                    if (xx >= 0 && xx < w && y >= 0 && y < h) img[y * w + xx] = c;
                }
            }
        }

        // Column indices to project: always the silhouette extents (0 and W-1, i.e. overall size),
        // plus up to maxPeaks strongest interior colour/feature boundaries.
        static List<int> FeatureLandmarks(View v, int maxPeaks)
        {
            var marks = PickPeaks(v.VerticalEdgeStrength(), maxPeaks, Mathf.Max(2, v.W / 8), 0.30f);
            if (!marks.Contains(0)) marks.Add(0);
            if (!marks.Contains(v.W - 1)) marks.Add(v.W - 1);
            marks.Sort();
            return marks;
        }

        // Local maxima of `e` above relThresh*max, taken strongest-first with a minimum spacing.
        static List<int> PickPeaks(float[] e, int maxCount, int minSpacing, float relThresh)
        {
            var chosen = new List<int>();
            int n = e.Length;
            if (n < 3) return chosen;

            float mx = 0f;
            for (int i = 0; i < n; i++) if (e[i] > mx) mx = e[i];
            if (mx <= 0f) return chosen;
            float th = relThresh * mx;

            var cands = new List<int>();
            for (int i = 1; i < n - 1; i++)
                if (e[i] >= th && e[i] >= e[i - 1] && e[i] >= e[i + 1]) cands.Add(i);
            cands.Sort((a, b) => e[b].CompareTo(e[a]));

            foreach (int c in cands)
            {
                if (chosen.Count >= maxCount) break;
                bool ok = true;
                foreach (int ch in chosen) if (Mathf.Abs(ch - c) < minSpacing) { ok = false; break; }
                if (ok) chosen.Add(c);
            }
            return chosen;
        }

        // Quarter arc (upper-right quadrant) centred at (x0, y0), radius r: sweeps from the horizontal
        // (east) to the vertical (north) datum, rotating a depth dimension 90° from the side to the top.
        static void DrawArc(Color32[] img, int w, int h, int x0, int y0, int r, Color32 c, int thick)
        {
            if (r <= 0) return;
            int steps = Mathf.Max(16, r * 2);
            for (int i = 0; i <= steps; i++)
            {
                float a = (Mathf.PI / 2f) * i / steps;
                int x = x0 + Mathf.RoundToInt(r * Mathf.Cos(a));
                int y = y0 + Mathf.RoundToInt(r * Mathf.Sin(a));
                for (int dy = -thick; dy <= thick; dy++)
                for (int dx = -thick; dx <= thick; dx++)
                {
                    int xx = x + dx, yy = y + dy;
                    if (xx >= 0 && xx < w && yy >= 0 && yy < h) img[yy * w + xx] = c;
                }
            }
        }

        // Small filled square marking where a projector lands.
        static void DrawTick(Color32[] img, int w, int h, int x, int y, int size, Color32 c)
        {
            int half = Mathf.Max(1, size / 2);
            for (int dy = -half; dy <= half; dy++)
            for (int dx = -half; dx <= half; dx++)
            {
                int xx = x + dx, yy = y + dy;
                if (xx >= 0 && xx < w && yy >= 0 && yy < h) img[yy * w + xx] = c;
            }
        }

        static Texture2D ScaleNearest(Texture2D src, int dw, int dh)
        {
            var sp = src.GetPixels32();
            var dp = new Color32[dw * dh];
            for (int y = 0; y < dh; y++)
            for (int x = 0; x < dw; x++)
            {
                int sx = Mathf.Min(src.width - 1, x * src.width / dw);
                int sy = Mathf.Min(src.height - 1, y * src.height / dh);
                dp[y * dw + x] = sp[sy * src.width + sx];
            }
            var t = new Texture2D(dw, dh, TextureFormat.RGBA32, false);
            t.SetPixels32(dp);
            t.Apply();
            return t;
        }

        // ---------------------------------------------------------------- down-res

        // Collapse the high-res hull to an editable resolution. A block becomes solid when at least
        // `fill` of its source cells are solid; its colour is the modal (most common) source colour.
        static void Downres(Hull h, int outHeight, float fill,
            out List<int> gx, out List<int> gy, out List<int> gz, out List<Color> cols)
        {
            int W = h.W, H = h.H, L = h.L;
            int Ho = Mathf.Clamp(outHeight, 1, H);
            int Wo = Mathf.Clamp(Mathf.RoundToInt(W * (float)Ho / H), 1, W);
            int Lo = Mathf.Clamp(Mathf.RoundToInt(L * (float)Ho / H), 1, L);

            gx = new List<int>(); gy = new List<int>(); gz = new List<int>(); cols = new List<Color>();
            var count = new Dictionary<int, int>();
            var rep = new Dictionary<int, Color>();

            for (int jo = 0; jo < Ho; jo++)
            for (int ko = 0; ko < Lo; ko++)
            for (int io = 0; io < Wo; io++)
            {
                int i0 = io * W / Wo, i1 = (io + 1) * W / Wo;
                int j0 = jo * H / Ho, j1 = (jo + 1) * H / Ho;
                int k0 = ko * L / Lo, k1 = (ko + 1) * L / Lo;

                count.Clear(); rep.Clear();
                int total = 0, solidN = 0;
                for (int j = j0; j < j1; j++)
                for (int k = k0; k < k1; k++)
                for (int i = i0; i < i1; i++)
                {
                    total++;
                    int vox = h.gridToVoxel[i + W * (j + H * k)];
                    if (vox < 0) continue;
                    solidN++;
                    int key = ColourKey(h.cols[vox]);
                    count.TryGetValue(key, out int prev);
                    count[key] = prev + 1;
                    rep[key] = h.cols[vox];
                }

                if (total == 0 || solidN < fill * total) continue;

                int bestKey = 0, bestCount = -1;
                foreach (var kv in count)
                    if (kv.Value > bestCount) { bestCount = kv.Value; bestKey = kv.Key; }

                gx.Add(io); gy.Add(ko); gz.Add(jo);
                cols.Add(rep[bestKey]);
            }
        }

        static int ColourKey(Color c)
        {
            int r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
            int g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
            int b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
            return (r << 16) | (g << 8) | b;
        }

        static string BuildGoxel(List<int> gx, List<int> gy, List<int> gz, List<Color> cols)
        {
            var sb = new StringBuilder();
            sb.Append("# Goxel 0.10.0\n");
            sb.Append("# One line per voxel\n");
            sb.Append("# X Y Z RRGGBB\n");
            for (int v = 0; v < cols.Count; v++)
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
            return sb.ToString();
        }

        static int[] Filled(int n, int value)
        {
            var a = new int[n];
            for (int i = 0; i < n; i++) a[i] = value;
            return a;
        }

        // Flood unseen voxels with their nearest seen voxel's colour. Multi-source BFS over the
        // solid grid (6-connected) seeded from every seen voxel.
        static void FillUnseen(List<Color> cols, List<bool> seen, int[] gridToVoxel,
                               List<int> gx, List<int> gy, List<int> gz, int W, int H, int L)
        {
            int count = cols.Count;
            var filled = new bool[count];
            var queue = new Queue<int>();
            for (int v = 0; v < count; v++)
                if (seen[v]) { filled[v] = true; queue.Enqueue(v); }

            if (queue.Count == 0 || queue.Count == count) return;

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

        // ---------------------------------------------------------------- palette helpers

        // Coarse-bin the voxel colours into an RGB histogram (4 bits/channel) and return one
        // candidate per occupied bin: its mean colour and population.
        static void BuildHistogram(List<Color> colours, out List<Color> means, out List<int> counts)
        {
            const int levels = 16;
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

        // Popularity ("modal"): the N most-populated bins.
        static Color[] BuildModalPalette(List<Color> colours, int n)
        {
            BuildHistogram(colours, out var means, out var counts);
            return Enumerable.Range(0, means.Count)
                .OrderByDescending(i => counts[i])
                .Take(n)
                .Select(i => means[i])
                .ToArray();
        }

        // Variety (farthest-point / max-min): captures the model's colour diversity so chromatically
        // distinct outliers (a single-voxel eye) survive even when they cover very few voxels.
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
                if (minDist[far] <= 0f) break;

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

        // Snap every seen voxel to its nearest palette colour in CIELAB (perceptual), then cap the
        // object to maxColours by keeping a variety-spread subset and remapping the rest.
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

        static readonly string[] Endesga32 =
        {
            "be4a2f", "d77643", "ead4aa", "e4a672", "b86f50", "733e39", "3e2731", "a22633",
            "e43b44", "f77622", "feae34", "fee761", "63c74d", "3e8948", "265c42", "193c3e",
            "124e89", "0099db", "2ce8f5", "ffffff", "c0cbdc", "8b9bb4", "5a6988", "3a4466",
            "262b44", "181425", "ff0044", "68386c", "b55088", "f6757a", "e8b796", "c28569",
        };

        static readonly string[] DawnBringer32 =
        {
            "000000", "222034", "45283c", "663931", "8f563b", "df7126", "d9a066", "eec39a",
            "fbf236", "99e550", "6abe30", "37946e", "4b692f", "524b24", "323c39", "3f3f74",
            "306082", "5b6ee1", "639bff", "5fcde4", "cbdbfc", "ffffff", "9badb7", "847e87",
            "696a6a", "595652", "76428a", "ac3232", "d95763", "d77bba", "8f974a", "8a6f30",
        };

        static readonly string[] DawnBringer16 =
        {
            "140c1c", "442434", "30346d", "4e4a4e", "854c30", "346524", "d04648", "757161",
            "597dce", "d27d2c", "8595a1", "6daa2c", "d2aa99", "6dc2ca", "dad45e", "deeed6",
        };

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

        // ---------------------------------------------------------------- view

        /// <summary>One cropped, segmented orthographic view. Pixel (0,0) is bottom-left (v=0 at bottom).</summary>
        class View
        {
            public int W;
            public int H;
            Color32[] _col;
            bool[] _fg;
            bool _flipU;
            bool _flipV;

            // Single-object image: segment against the perimeter-average background and crop to its bbox.
            public static View Build(Texture2D tex, float threshold, int dilate, bool flipU, bool flipV)
            {
                Color32[] px = ReadPixels(tex, out int tw, out int th);
                ComputeBackground(px, tw, th, out float bgR, out float bgG, out float bgB);

                bool[] fgFull = new bool[tw * th];
                int minX = tw, minY = th, maxX = -1, maxY = -1;
                for (int y = 0; y < th; y++)
                for (int x = 0; x < tw; x++)
                {
                    if (!IsForeground(px[y * tw + x], bgR, bgG, bgB, threshold)) continue;
                    fgFull[y * tw + x] = true;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
                if (maxX < 0) return null;

                return Crop(px, tw, minX, minY, maxX, maxY,
                    (sx, sy) => fgFull[sy * tw + sx], dilate, flipU, flipV);
            }

            // Turnaround sheet: segment, find the two largest connected components, crop each on a
            // shared vertical extent (so front & right stay registered on the height axis), and order
            // them left->right (left = front unless `swap`). Returns { front, right } or null.
            public static View[] SplitTwo(Texture2D tex, float threshold, int dilate, bool swap,
                bool aFlipU, bool aFlipV, bool bFlipU, bool bFlipV)
            {
                Color32[] px = ReadPixels(tex, out int tw, out int th);
                ComputeBackground(px, tw, th, out float bgR, out float bgG, out float bgB);

                bool[] fg = new bool[tw * th];
                for (int i = 0; i < fg.Length; i++)
                    fg[i] = IsForeground(px[i], bgR, bgG, bgB, threshold);

                List<Comp> comps = ConnectedComponents(fg, tw, th, out int[] label);
                if (comps.Count < 2) return null;

                comps.Sort((u, v) => v.Count.CompareTo(u.Count));
                Comp a = comps[0], b = comps[1];
                if (a.CentroidX > b.CentroidX) { (a, b) = (b, a); } // a = left, b = right

                int sharedMinY = Mathf.Min(a.MinY, b.MinY);
                int sharedMaxY = Mathf.Max(a.MaxY, b.MaxY);

                View left = Crop(px, tw, a.MinX, sharedMinY, a.MaxX, sharedMaxY,
                    (sx, sy) => label[sy * tw + sx] == a.Id, dilate, aFlipU, aFlipV);
                View right = Crop(px, tw, b.MinX, sharedMinY, b.MaxX, sharedMaxY,
                    (sx, sy) => label[sy * tw + sx] == b.Id, dilate, bFlipU, bFlipV);

                return swap ? new[] { right, left } : new[] { left, right };
            }

            // Single-object image, keeping only the largest connected component (drops speckle and any
            // stray annotation the AI left). Used to re-import the refined top in stage 2.
            public static View BuildLargest(Texture2D tex, float threshold, int dilate, bool flipU, bool flipV)
            {
                Color32[] px = ReadPixels(tex, out int tw, out int th);
                ComputeBackground(px, tw, th, out float bgR, out float bgG, out float bgB);

                bool[] fg = new bool[tw * th];
                for (int i = 0; i < fg.Length; i++)
                    fg[i] = IsForeground(px[i], bgR, bgG, bgB, threshold);

                List<Comp> comps = ConnectedComponents(fg, tw, th, out int[] label);
                if (comps.Count == 0) return null;
                Comp big = comps[0];
                foreach (var c in comps) if (c.Count > big.Count) big = c;

                return Crop(px, tw, big.MinX, big.MinY, big.MaxX, big.MaxY,
                    (sx, sy) => label[sy * tw + sx] == big.Id, dilate, flipU, flipV);
            }

            static View Crop(Color32[] src, int srcW, int minX, int minY, int maxX, int maxY,
                System.Func<int, int, bool> isFg, int dilate, bool flipU, bool flipV)
            {
                int cw = maxX - minX + 1;
                int ch = maxY - minY + 1;
                var col = new Color32[cw * ch];
                var fg = new bool[cw * ch];
                for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    int sx = minX + x, sy = minY + y;
                    col[y * cw + x] = src[sy * srcW + sx];
                    fg[y * cw + x] = isFg(sx, sy);
                }
                if (dilate > 0) fg = Dilate(fg, cw, ch, dilate);
                return new View { W = cw, H = ch, _col = col, _fg = fg, _flipU = flipU, _flipV = flipV };
            }

            class Comp
            {
                public int Id, Count, MinX = int.MaxValue, MaxX = -1, MinY = int.MaxValue, MaxY = -1;
                public long SumX;
                public float CentroidX => Count > 0 ? SumX / (float)Count : 0f;
            }

            // 8-connected labelling of the foreground; `label` returns the per-pixel component id.
            static List<Comp> ConnectedComponents(bool[] fg, int w, int h, out int[] label)
            {
                label = Filled(w * h, -1);
                var comps = new List<Comp>();
                var queue = new Queue<int>();

                for (int s = 0; s < fg.Length; s++)
                {
                    if (!fg[s] || label[s] >= 0) continue;
                    int id = comps.Count;
                    var c = new Comp { Id = id };
                    label[s] = id;
                    queue.Enqueue(s);
                    while (queue.Count > 0)
                    {
                        int p = queue.Dequeue();
                        int x = p % w, y = p / w;
                        c.Count++; c.SumX += x;
                        if (x < c.MinX) c.MinX = x;
                        if (x > c.MaxX) c.MaxX = x;
                        if (y < c.MinY) c.MinY = y;
                        if (y > c.MaxY) c.MaxY = y;
                        for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || nx >= w || ny < 0 || ny >= h) continue;
                            int np = ny * w + nx;
                            if (!fg[np] || label[np] >= 0) continue;
                            label[np] = id;
                            queue.Enqueue(np);
                        }
                    }
                    comps.Add(c);
                }
                return comps;
            }

            // Background colour = average of the perimeter (border-ring) pixels, so segmentation works
            // on any flat background, not just white.
            static void ComputeBackground(Color32[] px, int tw, int th, out float bgR, out float bgG, out float bgB)
            {
                float r = 0f, g = 0f, b = 0f;
                int n = 0;
                for (int x = 0; x < tw; x++)
                {
                    Color32 b0 = px[x];                       // bottom row
                    Color32 b1 = px[(th - 1) * tw + x];       // top row
                    r += b0.r + b1.r; g += b0.g + b1.g; b += b0.b + b1.b; n += 2;
                }
                for (int y = 1; y < th - 1; y++)
                {
                    Color32 l = px[y * tw];                   // left column
                    Color32 rr = px[y * tw + tw - 1];         // right column
                    r += l.r + rr.r; g += l.g + rr.g; b += l.b + rr.b; n += 2;
                }
                float inv = n > 0 ? 1f / (n * 255f) : 0f;
                bgR = r * inv; bgG = g * inv; bgB = b * inv;
            }

            // Foreground when far enough from the background; `threshold` is normalized RGB distance.
            static bool IsForeground(Color32 c, float bgR, float bgG, float bgB, float threshold)
            {
                if (c.a <= 127) return false;
                float dr = c.r / 255f - bgR, dg = c.g / 255f - bgG, db = c.b / 255f - bgB;
                return Mathf.Sqrt(dr * dr + dg * dg + db * db) > threshold;
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

            // Per-column "vertical edge" strength: how much this column differs from the one to its
            // left, summed over rows. Silhouette boundaries (fg/bg flips) and colour boundaries (Lab
            // distance) both count, so peaks mark key feature/colour transitions along the width axis.
            public float[] VerticalEdgeStrength()
            {
                var e = new float[W];
                for (int x = 1; x < W; x++)
                {
                    float sum = 0f;
                    for (int y = 0; y < H; y++)
                    {
                        bool a = _fg[y * W + x], b = _fg[y * W + x - 1];
                        if (!a && !b) continue;
                        if (a != b) { sum += 25f; continue; } // silhouette edge ~ a moderate Lab jump
                        sum += (RgbToLab(_col[y * W + x]) - RgbToLab(_col[y * W + x - 1])).magnitude;
                    }
                    e[x] = sum / Mathf.Max(1, H);
                }
                return e;
            }

            // The cropped view colours with the background knocked out flat — a reference-sheet panel.
            public Texture2D BuildColourPreview()
            {
                var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
                var px = new Color32[W * H];
                for (int i = 0; i < px.Length; i++)
                    px[i] = _fg[i] ? _col[i] : Neutral;
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
