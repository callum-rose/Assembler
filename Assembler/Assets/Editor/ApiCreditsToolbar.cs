#nullable enable

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace Assembler.EditorTools
{
    /// <summary>
    /// Adds a small credit-balance readout to the main editor toolbar, immediately
    /// to the right of the play / pause / step controls.
    ///
    /// Only <b>Meshy</b> exposes an API that returns a remaining credit balance
    /// (<c>GET https://api.meshy.ai/openapi/v1/balance</c> → <c>{"balance": N}</c>),
    /// so it is shown as a live number, refreshed on load, on click, and every few
    /// minutes.
    ///
    /// <b>Anthropic</b> and <b>Google Gemini</b> have no public endpoint that returns
    /// a remaining balance — Anthropic's balance lives only in the Console billing
    /// page (the Admin API reports spend, not balance), and Gemini's prepaid balance
    /// is only shown in the AI Studio billing tab. Those two are therefore rendered
    /// as buttons that open the respective billing page rather than a live figure.
    ///
    /// The Meshy key is read from the same <see cref="EditorPrefs"/> entry the
    /// "Image to 3D" window stores, so no extra configuration is needed.
    /// </summary>
    [InitializeOnLoad]
    internal static class ApiCreditsToolbar
    {
        // Reused from Assembler.AssetGeneration.ImageToMesh.MeshyImageTo3DWindow.
        private const string MeshyApiKeyPref = "Meshy.ImageTo3D.ApiKey";

        private const string MeshyBalanceUrl = "https://api.meshy.ai/openapi/v1/balance";
        private const string AnthropicBillingUrl = "https://console.anthropic.com/settings/billing";
        private const string GeminiBillingUrl = "https://aistudio.google.com/billing";

        // How often the Meshy balance auto-refreshes while the editor is open.
        private const double AutoRefreshSeconds = 300; // 5 minutes
        // How often we retry injecting into the toolbar when it isn't attached yet.
        private const double InjectRetrySeconds = 0.5;

        private static readonly Type? ToolbarType =
            typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");

        private static VisualElement? _container;
        private static Label? _meshyLabel;
        private static double _nextInjectAttempt;
        private static bool _warned;

        // Meshy request state (all touched on the main thread only).
        private static UnityWebRequest? _meshyRequest;
        private static UnityWebRequestAsyncOperation? _meshyOp;
        private static int? _meshyBalance;
        private static string? _meshyError;
        private static bool _refreshing;
        private static double _lastRefreshTime = double.NegativeInfinity;

        static ApiCreditsToolbar()
        {
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (_container != null && _container.parent != null)
            {
                MaybeAutoRefresh();
                return;
            }

            if (EditorApplication.timeSinceStartup < _nextInjectAttempt)
                return;
            _nextInjectAttempt = EditorApplication.timeSinceStartup + InjectRetrySeconds;

            TryInject();
        }

        private static void TryInject()
        {
            if (ToolbarType == null)
            {
                WarnOnce("UnityEditor.Toolbar type not found — API credits toolbar disabled.");
                return;
            }

            var toolbars = Resources.FindObjectsOfTypeAll(ToolbarType);
            if (toolbars.Length == 0)
                return; // toolbar not created yet (e.g. maximized play mode)

            var rootField = GetFieldRecursive(ToolbarType, "m_Root");
            if (rootField == null)
            {
                WarnOnce("UnityEditor.Toolbar.m_Root not found — API credits toolbar disabled.");
                return;
            }

            if (rootField.GetValue(toolbars[0]) is not VisualElement root)
                return;

            // Zone immediately to the right of the play/pause/step controls.
            var zone = root.Q("ToolbarZoneRightAlign");
            if (zone == null)
                return;

            _container = BuildUi();
            zone.Insert(0, _container);
            UpdateMeshyLabel();
            RequestMeshyRefresh();
        }

        private static VisualElement BuildUi()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginRight = 6;

            _meshyLabel = new Label("Meshy: …")
            {
                tooltip = "Meshy credit balance (live). Click to refresh."
            };
            _meshyLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            _meshyLabel.style.marginLeft = 4;
            _meshyLabel.style.marginRight = 6;
            _meshyLabel.RegisterCallback<MouseDownEvent>(_ => RequestMeshyRefresh());
            row.Add(_meshyLabel);

            // Anthropic and Gemini have no balance API — link to their billing pages.
            row.Add(MakeLinkButton(
                "Claude ↗",
                "Anthropic has no credit-balance API. Opens the Console billing page.",
                AnthropicBillingUrl));
            row.Add(MakeLinkButton(
                "Gemini ↗",
                "Gemini has no credit-balance API. Opens the AI Studio billing page.",
                GeminiBillingUrl));

            return row;
        }

        private static Button MakeLinkButton(string text, string tooltip, string url)
        {
            var button = new Button(() => Application.OpenURL(url))
            {
                text = text,
                tooltip = tooltip
            };
            button.style.marginLeft = 2;
            button.style.marginRight = 2;
            button.style.paddingLeft = 6;
            button.style.paddingRight = 6;
            return button;
        }

        private static void MaybeAutoRefresh()
        {
            if (_refreshing || EditorApplication.isCompiling)
                return;
            if (EditorApplication.timeSinceStartup - _lastRefreshTime < AutoRefreshSeconds)
                return;
            RequestMeshyRefresh();
        }

        private static void RequestMeshyRefresh()
        {
            if (_refreshing)
                return;

            _lastRefreshTime = EditorApplication.timeSinceStartup;

            var key = EditorPrefs.GetString(MeshyApiKeyPref, "");
            if (string.IsNullOrWhiteSpace(key))
            {
                _meshyBalance = null;
                _meshyError = "No Meshy API key set — configure it in the Image to 3D window.";
                UpdateMeshyLabel();
                return;
            }

            _refreshing = true;
            _meshyError = null;
            UpdateMeshyLabel();

            var request = UnityWebRequest.Get(MeshyBalanceUrl);
            request.SetRequestHeader("Authorization", "Bearer " + key.Trim());
            _meshyRequest = request;
            _meshyOp = request.SendWebRequest();
            EditorApplication.update += PumpMeshy;
        }

        private static void PumpMeshy()
        {
            if (_meshyRequest == null || _meshyOp == null)
            {
                EditorApplication.update -= PumpMeshy;
                _refreshing = false;
                return;
            }

            if (!_meshyOp.isDone)
                return;

            EditorApplication.update -= PumpMeshy;
            var request = _meshyRequest;

            try
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    _meshyBalance = null;
                    _meshyError = $"{(int)request.responseCode} {request.error}".Trim();
                }
                else
                {
                    var parsed = JsonUtility.FromJson<BalanceResponse>(request.downloadHandler.text);
                    if (parsed == null)
                    {
                        _meshyBalance = null;
                        _meshyError = "Unexpected response: " + Truncate(request.downloadHandler.text, 120);
                    }
                    else
                    {
                        _meshyBalance = parsed.balance;
                        _meshyError = null;
                    }
                }
            }
            catch (Exception e)
            {
                _meshyBalance = null;
                _meshyError = e.Message;
            }
            finally
            {
                request.Dispose();
                _meshyRequest = null;
                _meshyOp = null;
                _refreshing = false;
                UpdateMeshyLabel();
            }
        }

        private static void UpdateMeshyLabel()
        {
            if (_meshyLabel == null)
                return;

            if (_refreshing)
            {
                _meshyLabel.text = "Meshy: …";
                _meshyLabel.tooltip = "Refreshing Meshy balance…";
                SetLabelColour(null);
            }
            else if (_meshyError != null)
            {
                _meshyLabel.text = "Meshy: !";
                _meshyLabel.tooltip = "Meshy: " + _meshyError + "\n(click to retry)";
                SetLabelColour(new Color(0.90f, 0.45f, 0.40f));
            }
            else if (_meshyBalance.HasValue)
            {
                _meshyLabel.text = $"Meshy: {_meshyBalance.Value:N0}";
                _meshyLabel.tooltip =
                    $"Meshy credits (live). Updated {DateTime.Now:HH:mm:ss}. Click to refresh.";
                SetLabelColour(null);
            }
            else
            {
                _meshyLabel.text = "Meshy: —";
                _meshyLabel.tooltip = "Click to fetch the Meshy balance.";
                SetLabelColour(null);
            }
        }

        private static void SetLabelColour(Color? colour)
        {
            if (_meshyLabel == null)
                return;
            _meshyLabel.style.color = colour.HasValue
                ? new StyleColor(colour.Value)
                : new StyleColor(StyleKeyword.Null);
        }

        private static FieldInfo? GetFieldRecursive(Type type, string name)
        {
            for (var current = type; current != null; current = current.BaseType)
            {
                var field = current.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                    return field;
            }
            return null;
        }

        private static string Truncate(string value, int max) =>
            string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max) + "…";

        private static void WarnOnce(string message)
        {
            if (_warned)
                return;
            _warned = true;
            Debug.LogWarning("[ApiCreditsToolbar] " + message);
        }

        [Serializable]
        private class BalanceResponse
        {
            public int balance;
        }
    }
}
