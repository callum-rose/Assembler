#nullable enable

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.Networking;

namespace Assembler.EditorTools
{
    /// <summary>
    /// Adds a small credit-balance readout to the main editor toolbar, in the middle
    /// dock next to the play / pause / step controls, using the supported
    /// <see cref="MainToolbarElementAttribute"/> API (Unity 6.1+).
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
    internal static class ApiCreditsToolbar
    {
        // Unique identifier for the toolbar element (used by MainToolbar.Refresh).
        private const string ElementId = "Assembler/ApiCredits";

        // Reused from Assembler.AssetGeneration.ImageToMesh.MeshyImageTo3DWindow.
        private const string MeshyApiKeyPref = "Meshy.ImageTo3D.ApiKey";

        private const string MeshyBalanceUrl = "https://api.meshy.ai/openapi/v1/balance";
        private const string AnthropicBillingUrl = "https://console.anthropic.com/settings/billing";
        private const string GeminiBillingUrl = "https://aistudio.google.com/billing";

        // How often the Meshy balance auto-refreshes while the editor is open.
        private const double AutoRefreshSeconds = 300; // 5 minutes

        // Meshy request state (all touched on the main thread only).
        private static UnityWebRequest? _meshyRequest;
        private static UnityWebRequestAsyncOperation? _meshyOp;
        private static int? _meshyBalance;
        private static string? _meshyError;
        private static bool _refreshing;
        private static double _lastRefreshTime = double.NegativeInfinity;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            // Drives the periodic auto-refresh; the first tick fetches immediately
            // (_lastRefreshTime starts at -inf), which also defers the initial
            // network call until the editor has finished loading.
            EditorApplication.update += AutoRefreshTick;
        }

        [MainToolbarElement(ElementId, defaultDockPosition = MainToolbarDockPosition.Middle)]
        private static IEnumerable<MainToolbarElement> Create()
        {
            yield return new MainToolbarButton(MeshyContent(), RequestMeshyRefresh);

            // Anthropic and Gemini have no balance API — link to their billing pages.
            yield return new MainToolbarButton(
                new MainToolbarContent(
                    "Claude ↗",
                    "Anthropic has no credit-balance API. Opens the Console billing page."),
                () => Application.OpenURL(AnthropicBillingUrl));

            yield return new MainToolbarButton(
                new MainToolbarContent(
                    "Gemini ↗",
                    "Gemini has no credit-balance API. Opens the AI Studio billing page."),
                () => Application.OpenURL(GeminiBillingUrl));
        }

        private static MainToolbarContent MeshyContent()
        {
            if (_refreshing)
                return new MainToolbarContent("Meshy: …", "Refreshing Meshy balance…");
            if (_meshyError != null)
                return new MainToolbarContent("Meshy: !", "Meshy: " + _meshyError + "\n(click to retry)");
            if (_meshyBalance.HasValue)
                return new MainToolbarContent(
                    $"Meshy: {_meshyBalance.Value:N0}",
                    $"Meshy credits (live). Updated {DateTime.Now:HH:mm:ss}. Click to refresh.");
            return new MainToolbarContent("Meshy: —", "Click to fetch the Meshy balance.");
        }

        private static void AutoRefreshTick()
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
                RefreshToolbar();
                return;
            }

            _refreshing = true;
            _meshyError = null;
            RefreshToolbar();

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
                RefreshToolbar();
            }
        }

        private static void RefreshToolbar()
        {
            // Re-queries Create() so the Meshy button rebuilds with the latest state.
            // Guarded because it may run before the toolbar element exists (on load).
            try
            {
                MainToolbar.Refresh(ElementId);
            }
            catch
            {
                // Toolbar not built yet — the element reads current state when created.
            }
        }

        private static string Truncate(string value, int max) =>
            string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max) + "…";

        [Serializable]
        private class BalanceResponse
        {
            public int balance;
        }
    }
}
