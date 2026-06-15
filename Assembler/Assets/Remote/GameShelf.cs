using System;
using System.Collections;
using System.IO;
using Assembler.Building;
using Assembler.Deserialisation;
using Assembler.Deserialisation.Dtos;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Assembler.Remote
{
	/// <summary>
	/// Runtime entry point for the remote shelf: fetches the manifest, lists the available games, and on tap
	/// downloads (or reuses a cached) descriptor, runs the v1 asset-guard, and builds it through the normal
	/// <see cref="Builder.BuildFromYaml"/> pipeline. When the running game tears itself down (the <c>!gameover</c>
	/// path destroys the "Game" root), the shelf re-appears and re-fetches the manifest so freshly-published
	/// games show up. Replaces <c>GameBootstrap</c> as the boot-scene component for player builds.
	///
	/// The UI is built programmatically (no authored scene), matching how <see cref="Builder"/> builds in-game UI.
	/// The canvas and EventSystem live under this (persistent) boot GameObject, so they survive game teardown.
	/// </summary>
	public sealed class GameShelf : MonoBehaviour
	{
		[Tooltip("URL of the remote manifest.json listing available games. Must be https on iOS (ATS).")]
		[SerializeField] private string _manifestUrl =
			"https://raw.githubusercontent.com/USER/assembler-games/main/manifest.json";

		[Tooltip("Seconds before a manifest/descriptor download is abandoned.")]
		[SerializeField] private int _timeoutSeconds = 15;

		private static readonly Color Background = new(0.10f, 0.11f, 0.13f, 1f);
		private static readonly Color Card = new(0.16f, 0.17f, 0.20f, 1f);
		private static readonly Color Accent = new(0.30f, 0.55f, 0.95f, 1f);

		private RemoteGameClient _client = null!;
		private readonly RemoteGameCache _cache = new();
		private readonly GameFileParser _parser = new();

		private GameObject _canvasRoot = null!;
		private RectTransform _listContent = null!;
		private GameObject _statusPanel = null!;
		private TextMeshProUGUI _statusText = null!;
		private Button _retryButton = null!;

		private void Start()
		{
			_client = new RemoteGameClient(_timeoutSeconds);
			BuildCanvas();
			StartCoroutine(LoadManifest());
		}

		// ---- Manifest loading -------------------------------------------------------------------------

		private IEnumerator LoadManifest()
		{
			ShowShelf();
			ShowStatus("Loading games…", showRetry: false);
			ClearList();

			yield return _client.FetchText(
				_manifestUrl,
				onText: raw =>
				{
					_cache.WriteManifest(raw);
					Populate(GameManifest.Parse(raw));
				},
				onError: OnManifestError);
		}

		private void OnManifestError(RemoteError error)
		{
			// Fall back to the last manifest we cached, so a flaky/absent connection still shows a playable shelf.
			var cached = _cache.ReadCachedManifest();

			if (!string.IsNullOrEmpty(cached))
			{
				Populate(GameManifest.Parse(cached!));
				ShowToast($"Offline — showing saved games ({error.Kind}).");
				return;
			}

			ShowStatus($"Couldn't load games.\n{error}", showRetry: true);
		}

		private void Populate(GameManifest manifest)
		{
			ClearList();

			if (manifest.Games.Count == 0)
			{
				ShowStatus("No games published yet.", showRetry: true);
				return;
			}

			HideStatus();

			foreach (var entry in manifest.Games)
			{
				CreateGameCard(entry);
			}
		}

		// ---- Launch flow ------------------------------------------------------------------------------

		private void OnGamePicked(GameManifestEntry entry) => StartCoroutine(LaunchGame(entry));

		private IEnumerator LaunchGame(GameManifestEntry entry)
		{
			string? yaml = null;
			RemoteError? fetchError = null;

			if (_cache.TryGetCached(entry.Id, entry.Version, out var cachedPath))
			{
				yaml = TryReadFile(cachedPath);
			}

			if (yaml == null)
			{
				ShowStatus($"Downloading “{entry.Title}”…", showRetry: false);

				yield return _client.FetchText(
					entry.DescriptorUrl,
					onText: body => yaml = body,
					onError: err => fetchError = err);

				if (yaml == null)
				{
					ShowStatus($"Couldn't download “{entry.Title}”.\n{fetchError}", showRetry: true);
					yield break;
				}

				_cache.Write(entry.Id, entry.Version, yaml);
			}

			// Parse + guard before touching the build pipeline so failures are clean messages, not mid-build crashes.
			GameDto dto;
			try
			{
				dto = _parser.Parse(yaml);
			}
			catch (Exception e)
			{
				ShowStatus($"“{entry.Title}” is not a valid descriptor.\n{e.Message}", showRetry: true);
				yield break;
			}

			var guard = RemoteGameGuard.Validate(dto);
			if (!guard.Allowed)
			{
				ShowStatus(guard.Reason!, showRetry: true);
				yield break;
			}

			HideShelf();

			GameObject gameRoot;
			try
			{
				gameRoot = Builder.BuildFromYaml(yaml);
			}
			catch (Exception e)
			{
				UnityEngine.Debug.LogError($"GameShelf: failed to build '{entry.Id}': {e}");
				ShowShelf();
				ShowStatus($"“{entry.Title}” failed to start.\n{e.Message}", showRetry: true);
				yield break;
			}

			// Wait until the game tears itself down (the !gameover path / any destruction of the root),
			// then return to the shelf and refresh so newly-published games appear.
			yield return new WaitUntil(() => gameRoot == null);

			yield return LoadManifest();
		}

		// ---- UI construction --------------------------------------------------------------------------

		private void BuildCanvas()
		{
			EnsureEventSystem();

			_canvasRoot = new GameObject("ShelfCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
			_canvasRoot.transform.SetParent(transform, worldPositionStays: false);

			var canvas = _canvasRoot.GetComponent<Canvas>();
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;

			var scaler = _canvasRoot.GetComponent<CanvasScaler>();
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = new Vector2(1080, 1920);
			scaler.matchWidthOrHeight = 0.5f;

			var background = CreateChild(_canvasRoot.transform, "Background", typeof(Image));
			Fill((RectTransform)background.transform);
			background.GetComponent<Image>().color = Background;

			var title = CreateText(_canvasRoot.transform, "Title", 64, TextAlignmentOptions.Center);
			var titleRt = title.rectTransform;
			titleRt.anchorMin = new Vector2(0, 1);
			titleRt.anchorMax = new Vector2(1, 1);
			titleRt.pivot = new Vector2(0.5f, 1);
			titleRt.sizeDelta = new Vector2(0, 160);
			titleRt.anchoredPosition = Vector2.zero;
			title.text = "<b>Games</b>";

			BuildScrollList();
			BuildStatusPanel();
		}

		private void BuildScrollList()
		{
			var scrollView = CreateChild(_canvasRoot.transform, "ScrollView", typeof(Image), typeof(ScrollRect));
			var scrollRt = (RectTransform)scrollView.transform;
			scrollRt.anchorMin = Vector2.zero;
			scrollRt.anchorMax = Vector2.one;
			scrollRt.offsetMin = new Vector2(24, 24);
			scrollRt.offsetMax = new Vector2(-24, -180);
			scrollView.GetComponent<Image>().color = new Color(0, 0, 0, 0);

			var viewport = CreateChild(scrollView.transform, "Viewport", typeof(Image), typeof(RectMask2D));
			var viewportRt = (RectTransform)viewport.transform;
			Fill(viewportRt);
			viewport.GetComponent<Image>().color = new Color(0, 0, 0, 0);

			var content = CreateChild(viewport.transform, "Content", typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
			_listContent = (RectTransform)content.transform;
			_listContent.anchorMin = new Vector2(0, 1);
			_listContent.anchorMax = new Vector2(1, 1);
			_listContent.pivot = new Vector2(0.5f, 1);
			_listContent.anchoredPosition = Vector2.zero;

			var layout = content.GetComponent<VerticalLayoutGroup>();
			layout.spacing = 18;
			layout.childForceExpandWidth = true;
			layout.childForceExpandHeight = false;
			layout.childControlWidth = true;
			layout.childControlHeight = true;

			content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

			var scroll = scrollView.GetComponent<ScrollRect>();
			scroll.horizontal = false;
			scroll.vertical = true;
			scroll.movementType = ScrollRect.MovementType.Elastic;
			scroll.viewport = viewportRt;
			scroll.content = _listContent;
		}

		private void BuildStatusPanel()
		{
			_statusPanel = CreateChild(_canvasRoot.transform, "StatusPanel", typeof(Image));
			Fill((RectTransform)_statusPanel.transform);
			_statusPanel.GetComponent<Image>().color = new Color(0.10f, 0.11f, 0.13f, 0.92f);

			_statusText = CreateText(_statusPanel.transform, "StatusText", 48, TextAlignmentOptions.Center);
			var textRt = _statusText.rectTransform;
			textRt.anchorMin = new Vector2(0.1f, 0.45f);
			textRt.anchorMax = new Vector2(0.9f, 0.65f);
			textRt.offsetMin = Vector2.zero;
			textRt.offsetMax = Vector2.zero;

			_retryButton = CreateButton(_statusPanel.transform, "Retry", () => StartCoroutine(LoadManifest()));
			var btnRt = _retryButton.GetComponent<RectTransform>();
			btnRt.anchorMin = new Vector2(0.3f, 0.34f);
			btnRt.anchorMax = new Vector2(0.7f, 0.42f);
			btnRt.offsetMin = Vector2.zero;
			btnRt.offsetMax = Vector2.zero;
		}

		private void CreateGameCard(GameManifestEntry entry)
		{
			var card = CreateChild(_listContent, "Game_" + entry.Id, typeof(Image), typeof(Button), typeof(LayoutElement));
			card.GetComponent<Image>().color = Card;

			var layoutElement = card.GetComponent<LayoutElement>();
			layoutElement.minHeight = 150;
			layoutElement.preferredHeight = 150;

			var label = CreateText(card.transform, "Label", 44, TextAlignmentOptions.TopLeft);
			var labelRt = label.rectTransform;
			labelRt.anchorMin = Vector2.zero;
			labelRt.anchorMax = Vector2.one;
			labelRt.offsetMin = new Vector2(28, 20);
			labelRt.offsetMax = new Vector2(-28, -20);
			label.text = string.IsNullOrEmpty(entry.Description)
				? $"<b>{entry.Title}</b>"
				: $"<b>{entry.Title}</b>\n<size=70%><color=#B8BDC7>{entry.Description}</color></size>";

			var captured = entry;
			card.GetComponent<Button>().onClick.AddListener(() => OnGamePicked(captured));
		}

		private Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
		{
			var go = CreateChild(parent, "Button_" + label, typeof(Image), typeof(Button));
			go.GetComponent<Image>().color = Accent;

			var text = CreateText(go.transform, "Label", 44, TextAlignmentOptions.Center);
			Fill(text.rectTransform);
			text.text = label;

			go.GetComponent<Button>().onClick.AddListener(onClick);
			return go.GetComponent<Button>();
		}

		// ---- UI state ---------------------------------------------------------------------------------

		private void ShowShelf() => _canvasRoot.SetActive(true);

		private void HideShelf() => _canvasRoot.SetActive(false);

		private void ShowStatus(string message, bool showRetry)
		{
			_statusPanel.SetActive(true);
			_statusText.text = message;
			_retryButton.gameObject.SetActive(showRetry);
		}

		private void HideStatus() => _statusPanel.SetActive(false);

		// A transient, non-blocking message overlaid on the list (used for the offline fallback notice).
		private void ShowToast(string message)
		{
			_statusPanel.SetActive(true);
			_statusText.text = message;
			_retryButton.gameObject.SetActive(false);
			CancelInvoke(nameof(HideStatus));
			Invoke(nameof(HideStatus), 2.5f);
		}

		private void ClearList()
		{
			for (var i = _listContent.childCount - 1; i >= 0; i--)
			{
				Destroy(_listContent.GetChild(i).gameObject);
			}
		}

		// ---- Helpers ----------------------------------------------------------------------------------

		private static void EnsureEventSystem()
		{
			if (EventSystem.current != null)
			{
				return;
			}

			var eventSystem = new GameObject("ShelfEventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
			DontDestroyOnLoad(eventSystem);
		}

		// Always seed the GameObject with a RectTransform (passing it as the sole ctor type makes Unity use a
		// RectTransform instead of a plain Transform), then add the UI components — several of which
		// (Image/ScrollRect/TMP) require a RectTransform to already be present.
		private static GameObject CreateChild(Transform parent, string name, params Type[] components)
		{
			var go = new GameObject(name, typeof(RectTransform));
			go.transform.SetParent(parent, worldPositionStays: false);

			foreach (var component in components)
			{
				go.AddComponent(component);
			}

			return go;
		}

		private static TextMeshProUGUI CreateText(Transform parent, string name, float fontSize, TextAlignmentOptions align)
		{
			var go = CreateChild(parent, name, typeof(TextMeshProUGUI));
			var text = go.GetComponent<TextMeshProUGUI>();
			text.fontSize = fontSize;
			text.alignment = align;
			text.color = Color.white;
			text.richText = true;
			text.raycastTarget = false;
			return text;
		}

		private static void Fill(RectTransform rt)
		{
			rt.anchorMin = Vector2.zero;
			rt.anchorMax = Vector2.one;
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;
		}

		private static string? TryReadFile(string path)
		{
			try
			{
				return File.ReadAllText(path);
			}
			catch (IOException)
			{
				return null;
			}
		}
	}
}
