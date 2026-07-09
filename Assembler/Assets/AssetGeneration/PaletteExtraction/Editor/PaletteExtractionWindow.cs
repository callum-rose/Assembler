using System;
using System.Globalization;
using System.IO;
using Assembler.AssetGeneration.Colour;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.PaletteExtraction.Editor
{
    /// <summary>
    /// Dev/tuning-only window: load an image, run <see cref="PaletteExtractor.Extract"/>, and show the
    /// masked object, the extracted swatch strip with per-swatch coverage, and a live slider for every
    /// <see cref="PaletteExtractionOptions"/> field. NOT part of the automated pipeline — it exists to
    /// tune <see cref="PaletteExtractionOptions.Default"/> against the tuning corpus. Mirrors
    /// <c>ImageOrientationWindow</c>: the editor decodes the image to a pixel array and the engine-free
    /// core does the rest.
    /// </summary>
    public sealed class PaletteExtractionWindow : EditorWindow
    {
        private const string ImagePathPref = "Assembler.AssetGeneration.PaletteExtraction.ImagePath";
        private const string OptionsPrefPrefix = "Assembler.AssetGeneration.PaletteExtraction.Opt.";

        private string _imagePath = string.Empty;
        private Texture2D? _source;

        // Decoded once on load and cached so slider tweaks re-extract without re-decoding.
        private Rgba32[]? _pixels;
        private int _width;
        private int _height;

        private PaletteExtractionOptions _options = PaletteExtractionOptions.Default;
        private PaletteResult? _result;
        private Texture2D? _maskedPreview;
        private string _status = string.Empty;
        private Vector2 _scroll;

        [MenuItem("Assembler/Palette Extraction/Extract Palette")]
        public static void Open()
        {
            var window = GetWindow<PaletteExtractionWindow>("Extract Palette");
            window.minSize = new Vector2(460, 640);
            window.Show();
        }

        private void OnEnable()
        {
            _imagePath = EditorPrefs.GetString(ImagePathPref, string.Empty);
            _options = LoadOptions();
            LoadImage();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            DrawImageSelector();
            EditorGUILayout.Space();
            DrawOptions();
            EditorGUILayout.Space();

            using (new EditorGUI.DisabledScope(_pixels is null))
            {
                if (GUILayout.Button("Extract palette"))
                {
                    RunExtraction();
                }
            }

            if (!string.IsNullOrEmpty(_status))
            {
                EditorGUILayout.HelpBox(_status, MessageType.Info);
            }

            if (_result is { } result)
            {
                DrawResult(result);
            }

            EditorGUILayout.EndScrollView();
        }

        // ---- Image selection --------------------------------------------------

        private void DrawImageSelector()
        {
            EditorGUILayout.LabelField("Image", EditorStyles.boldLabel);

            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                var texture = (Texture2D?)EditorGUILayout.ObjectField(
                    "In-project texture", _source, typeof(Texture2D), allowSceneObjects: false);
                if (scope.changed && texture != null)
                {
                    var path = AssetDatabase.GetAssetPath(texture);
                    if (!string.IsNullOrEmpty(path))
                    {
                        SetImagePath(path);
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Browse..."))
                {
                    var path = EditorUtility.OpenFilePanel("Select image", string.Empty, "png,jpg,jpeg");
                    if (!string.IsNullOrEmpty(path))
                    {
                        SetImagePath(path);
                    }
                }
                if (!string.IsNullOrEmpty(_imagePath) && GUILayout.Button("Clear", GUILayout.Width(60)))
                {
                    SetImagePath(string.Empty);
                }
            }

            if (!string.IsNullOrEmpty(_imagePath))
            {
                EditorGUILayout.LabelField(_imagePath, EditorStyles.miniLabel);
            }
            if (_source != null)
            {
                var rect = GUILayoutUtility.GetRect(180, 180, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(rect, _source, ScaleMode.ScaleToFit);
            }
        }

        private void SetImagePath(string path)
        {
            _imagePath = path;
            EditorPrefs.SetString(ImagePathPref, _imagePath);
            _result = null;
            _maskedPreview = null;
            LoadImage();
        }

        // Decode the image to a Rgba32[] once (respecting the imported asset's settings when in-project,
        // else decoding raw bytes) so extraction re-runs off the cached pixels.
        private void LoadImage()
        {
            _source = null;
            _pixels = null;
            if (string.IsNullOrEmpty(_imagePath) || !File.Exists(_imagePath))
            {
                return;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            if (!texture.LoadImage(File.ReadAllBytes(_imagePath)))
            {
                _status = "Failed to decode the image.";
                return;
            }

            _source = texture;
            _width = texture.width;
            _height = texture.height;

            Color32[] c = texture.GetPixels32();
            var pixels = new Rgba32[c.Length];
            for (int i = 0; i < c.Length; i++)
            {
                pixels[i] = new Rgba32(c[i].r, c[i].g, c[i].b, c[i].a);
            }
            _pixels = pixels;
            _status = $"Loaded {_width}×{_height}. Press Extract.";
        }

        // ---- Options ----------------------------------------------------------

        private void DrawOptions()
        {
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
            using var scope = new EditorGUI.ChangeCheckScope();

            float bgTol = EditorGUILayout.Slider(
                new GUIContent("Background tolerance", "Oklab flood-fill match radius."),
                _options.BackgroundTolerance, 0f, 0.4f);
            int erode = EditorGUILayout.IntSlider(
                new GUIContent("Erode pixels", "Anti-alias/JPEG halo kill."), _options.ErodePixels, 0, 4);
            float merge = EditorGUILayout.Slider(
                new GUIContent("Merge tolerance", "Oklab shading-step collapse (bias loose)."),
                _options.MergeTolerance, 0f, 0.4f);
            int maxColours = EditorGUILayout.IntSlider(
                new GUIContent("Max colours", "Hard cap (generous; over-segment is safe)."),
                _options.MaxColours, 1, 24);
            float minCoverage = EditorGUILayout.Slider(
                new GUIContent("Min coverage", "Drop clusters below this object-pixel fraction…"),
                _options.MinCoverage, 0f, 0.2f);
            float minCompactness = EditorGUILayout.Slider(
                new GUIContent("Min compactness", "…unless their pixels fill this fraction of their bbox."),
                _options.MinCompactness, 0f, 1f);

            if (scope.changed)
            {
                _options = new PaletteExtractionOptions
                {
                    BackgroundTolerance = bgTol,
                    ErodePixels = erode,
                    MergeTolerance = merge,
                    MaxColours = maxColours,
                    MinCoverage = minCoverage,
                    MinCompactness = minCompactness,
                };
                SaveOptions(_options);
                // Live re-extract off the cached pixels so tuning is immediate.
                if (_pixels is not null)
                {
                    RunExtraction();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reset to Default"))
                {
                    _options = PaletteExtractionOptions.Default;
                    SaveOptions(_options);
                    if (_pixels is not null)
                    {
                        RunExtraction();
                    }
                }
            }
        }

        private static PaletteExtractionOptions LoadOptions()
        {
            PaletteExtractionOptions d = PaletteExtractionOptions.Default;
            return new PaletteExtractionOptions
            {
                BackgroundTolerance = EditorPrefs.GetFloat(OptionsPrefPrefix + "bgTol", d.BackgroundTolerance),
                ErodePixels = EditorPrefs.GetInt(OptionsPrefPrefix + "erode", d.ErodePixels),
                MergeTolerance = EditorPrefs.GetFloat(OptionsPrefPrefix + "merge", d.MergeTolerance),
                MaxColours = EditorPrefs.GetInt(OptionsPrefPrefix + "maxColours", d.MaxColours),
                MinCoverage = EditorPrefs.GetFloat(OptionsPrefPrefix + "minCoverage", d.MinCoverage),
                MinCompactness = EditorPrefs.GetFloat(OptionsPrefPrefix + "minCompactness", d.MinCompactness),
            };
        }

        private static void SaveOptions(PaletteExtractionOptions o)
        {
            EditorPrefs.SetFloat(OptionsPrefPrefix + "bgTol", o.BackgroundTolerance);
            EditorPrefs.SetInt(OptionsPrefPrefix + "erode", o.ErodePixels);
            EditorPrefs.SetFloat(OptionsPrefPrefix + "merge", o.MergeTolerance);
            EditorPrefs.SetInt(OptionsPrefPrefix + "maxColours", o.MaxColours);
            EditorPrefs.SetFloat(OptionsPrefPrefix + "minCoverage", o.MinCoverage);
            EditorPrefs.SetFloat(OptionsPrefPrefix + "minCompactness", o.MinCompactness);
        }

        // ---- Extraction + result rendering ------------------------------------

        private void RunExtraction()
        {
            if (_pixels is null)
            {
                _status = "Load an image first.";
                return;
            }

            PaletteResult result = PaletteExtractor.Extract(_pixels, _width, _height, _options);
            _result = result;
            _maskedPreview = BuildMaskedPreview(_pixels, result.ObjectMask, _width, _height);
            _status = $"{result.Palette.Count} colours from {result.ObjectPixelCount:N0} object pixels " +
                      $"(bg {Describe(result.Background)}).";
        }

        private void DrawResult(PaletteResult result)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Masked object", EditorStyles.boldLabel);
            if (_maskedPreview != null)
            {
                var rect = GUILayoutUtility.GetRect(180, 180, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(rect, _maskedPreview, ScaleMode.ScaleToFit);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Palette — {result.Palette.Count} colours", EditorStyles.boldLabel);
            for (int i = 0; i < result.Palette.Count; i++)
            {
                DrawSwatch(result.Palette[i], result.Coverage[i], result.ObjectPixelCount);
            }
        }

        private static void DrawSwatch(Rgba32 c, int coverage, int total)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var rect = GUILayoutUtility.GetRect(28, 28, GUILayout.Width(28), GUILayout.Height(28));
                EditorGUI.DrawRect(rect, new Color32(c.r, c.g, c.b, 255));
                float pct = total > 0 ? 100f * coverage / total : 0f;
                EditorGUILayout.LabelField(
                    $"{Describe(c)}   {pct.ToString("0.0", CultureInfo.InvariantCulture)}%  ({coverage:N0} px)");
            }
        }

        // Object pixels keep their colour; background is dimmed so the mask boundary reads clearly.
        private static Texture2D BuildMaskedPreview(Rgba32[] pixels, bool[] mask, int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
            var outPixels = new Color32[pixels.Length];
            var dim = new Color32(30, 30, 34, 255);
            for (int i = 0; i < pixels.Length; i++)
            {
                outPixels[i] = mask[i]
                    ? new Color32(pixels[i].r, pixels[i].g, pixels[i].b, 255)
                    : dim;
            }
            texture.SetPixels32(outPixels);
            texture.Apply(updateMipmaps: false);
            return texture;
        }

        private static string Describe(Rgba32 c) => $"#{c.r:X2}{c.g:X2}{c.b:X2}";
    }
}
