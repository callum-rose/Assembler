using System;
using System.Collections.Generic;
using Assembler.Shell.Controls;
using Assembler.Shell.Navigation;
using Assembler.Shell.Overlays;
using Assembler.Shell.Screens;
using Assembler.Shell.Theming;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Assembler.Shell.Editor.ShellBuildUtility;

namespace Assembler.Shell.Editor
{
	/// <summary>
	/// Authors the four screen prefabs, the notice overlay, and the two catalogs that turn a
	/// <see cref="ScreenId"/> or an <see cref="OverlayId"/> into one of them.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The screens are deliberately bare (UIPLAN phase 3): a title, a rule and the buttons it takes to get
	/// everywhere from everywhere. What they prove is the shell, not the paper — that a screen is built on first
	/// visit and kept, that a pushed argument arrives, that the back control names the page underneath, that a
	/// presenter is constructed with its view. The editorial content lands on top of them in phase 5.
	/// </para>
	/// <para>
	/// Re-runnable like every other shell builder, and it runs <see cref="ShellPrefabBuilder"/> first — the
	/// screens are assembled out of the primitives, so those have to exist and be current.
	/// </para>
	/// </remarks>
	public static class ShellScreenBuilder
	{
		public const string FeedScreenPath = ShellPrefabBuilder.PrefabsFolder + "/FeedScreen.prefab";
		public const string DetailScreenPath = ShellPrefabBuilder.PrefabsFolder + "/DetailScreen.prefab";
		public const string ArchiveScreenPath = ShellPrefabBuilder.PrefabsFolder + "/ArchiveScreen.prefab";
		public const string SettingsScreenPath = ShellPrefabBuilder.PrefabsFolder + "/SettingsScreen.prefab";
		public const string NoticeOverlayPath = ShellPrefabBuilder.PrefabsFolder + "/NoticeOverlay.prefab";

		public const string ScreenCatalogPath = "Assets/Shell/ScreenCatalog.asset";
		public const string OverlayCatalogPath = "Assets/Shell/OverlayCatalog.asset";

		// The prototype's page rhythm, from the top of the safe area down. Anchors rather than a layout group:
		// these are screen fixtures, and layout groups are for flowing content inside a scroll (UIPLAN 6.1).
		private const float BackRow = 8f;
		private const float BackWidth = 150f;
		private const float BackHeight = 44f;
		private const float TitleRow = 60f;
		private const float TitleHeight = 18f;
		private const float RuleRow = 86f;
		private const float ButtonWidth = 350f;
		private const float ButtonHeight = 50f;
		private const float ButtonPitch = 64f;

		private static readonly (ScreenId Id, string Path, string Title)[] Screens =
		{
			(ScreenId.Feed, FeedScreenPath, "Front Page"),
			(ScreenId.Detail, DetailScreenPath, "The Edition"),
			(ScreenId.Archive, ArchiveScreenPath, "The Archive"),
			(ScreenId.Settings, SettingsScreenPath, "Settings")
		};

		private static readonly (OverlayId Id, string Path)[] Overlays =
		{
			(OverlayId.Notice, NoticeOverlayPath)
		};

		[MenuItem("Assembler/Shell/Build Shell Screens")]
		public static void BuildScreens()
		{
			ShellPrefabBuilder.BuildPrefabs();

			var workshop = EditorSceneManager.NewPreviewScene();

			try
			{
				BuildFeed(workshop);
				BuildDetail(workshop);
				BuildArchive(workshop);
				BuildSettings(workshop);
				BuildNotice(workshop);
			}
			finally
			{
				EditorSceneManager.ClosePreviewScene(workshop);
			}

			BuildScreenCatalog();
			BuildOverlayCatalog();

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log(
				$"{nameof(ShellScreenBuilder)}: {Screens.Length} screens and {Overlays.Length} overlay(s) built " +
				$"under {ShellPrefabBuilder.PrefabsFolder}, catalogued at {ScreenCatalogPath} and " +
				$"{OverlayCatalogPath}.");
		}

		private static void BuildFeed(Scene workshop)
		{
			using var prefab = PrefabScope.Open(FeedScreenPath, "FeedScreen", workshop);

			var group = OpenScreen(prefab.Root);
			var (title, _) = Chrome(prefab.Root, back: false);

			float gutter = Theme.Current.Layout.PageGutter;

			var headline = Band(prefab.Root.transform, "Headline", RuleRow + 14f, 90f, gutter);
			Write(headline, "Headline", "The lead story goes here", TextAlignmentOptions.TopLeft);

			var play = NestButton(prefab.Root.transform, "PlayButton", ShellPrefabBuilder.ButtonAccentPath, 200f, "PLAY TODAY'S");
			NestSectionHeader(prefab.Root.transform, "MoreEditions", 274f, "MORE EDITIONS");
			var archive = NestButton(prefab.Root.transform, "ArchiveButton", ShellPrefabBuilder.ButtonQuietPath, 336f, "OPEN THE ARCHIVE");
			var settings = NestButton(prefab.Root.transform, "SettingsButton", ShellPrefabBuilder.ButtonQuietPath, 336f + ButtonPitch, "SETTINGS");

			var view = Ensure<FeedView>(prefab.Root);
			SetField(view, "canvasGroup", group);
			SetField(view, "title", title);
			SetField(view, "headline", headline.GetComponent<TMP_Text>());
			SetField(view, "playButton", play);
			SetField(view, "archiveButton", archive);
			SetField(view, "settingsButton", settings);
		}

		private static void BuildDetail(Scene workshop)
		{
			using var prefab = PrefabScope.Open(DetailScreenPath, "DetailScreen", workshop);

			var group = OpenScreen(prefab.Root);
			var (title, back) = Chrome(prefab.Root, back: true);

			float gutter = Theme.Current.Layout.PageGutter;

			var kicker = Band(prefab.Root.transform, "Kicker", RuleRow + 12f, 14f, gutter);
			Write(kicker, "Kicker", "EDITION", TextAlignmentOptions.TopLeft);

			var headline = Band(prefab.Root.transform, "Headline", RuleRow + 34f, 90f, gutter);
			Write(headline, "Headline", "The edition's headline", TextAlignmentOptions.TopLeft);

			var play = NestButton(prefab.Root.transform, "PlayButton", ShellPrefabBuilder.ButtonAccentPath, 230f, "PLAY");
			var next = NestButton(prefab.Root.transform, "NextButton", ShellPrefabBuilder.ButtonQuietPath, 230f + ButtonPitch, "NEXT EDITION");

			var view = Ensure<DetailView>(prefab.Root);
			SetField(view, "canvasGroup", group);
			SetField(view, "title", title);
			SetField(view, "backButton", back);
			SetField(view, "headline", headline.GetComponent<TMP_Text>());
			SetField(view, "kicker", kicker.GetComponent<TMP_Text>());
			SetField(view, "playButton", play);
			SetField(view, "nextButton", next);
		}

		private static void BuildArchive(Scene workshop)
		{
			using var prefab = PrefabScope.Open(ArchiveScreenPath, "ArchiveScreen", workshop);

			var group = OpenScreen(prefab.Root);
			var (title, back) = Chrome(prefab.Root, back: true);

			var rows = new LetterpressButton[4];

			for (var i = 0; i < rows.Length; i++)
			{
				rows[i] = NestButton(
					prefab.Root.transform,
					$"Row{i}",
					ShellPrefabBuilder.ButtonQuietPath,
					RuleRow + 14f + (i * ButtonPitch),
					$"EDITION {rows.Length - i}");
			}

			var view = Ensure<ArchiveView>(prefab.Root);
			SetField(view, "canvasGroup", group);
			SetField(view, "title", title);
			SetField(view, "backButton", back);
			SetArray(view, "rows", rows);
		}

		private static void BuildSettings(Scene workshop)
		{
			using var prefab = PrefabScope.Open(SettingsScreenPath, "SettingsScreen", workshop);

			var group = OpenScreen(prefab.Root);
			var (title, back) = Chrome(prefab.Root, back: true);

			var about = NestButton(
				prefab.Root.transform,
				"AboutButton",
				ShellPrefabBuilder.ButtonQuietPath,
				RuleRow + 14f,
				"ABOUT");

			var view = Ensure<SettingsView>(prefab.Root);
			SetField(view, "canvasGroup", group);
			SetField(view, "title", title);
			SetField(view, "backButton", back);
			SetField(view, "aboutButton", about);
		}

		private static void BuildNotice(Scene workshop)
		{
			using var prefab = PrefabScope.Open(NoticeOverlayPath, "NoticeOverlay", workshop);

			Stretch(prefab.Root);
			Ensure<Canvas>(prefab.Root);
			Ensure<GraphicRaycaster>(prefab.Root);

			var frame = NestPrefab(prefab.Root.transform, "SheetFrame", ShellPrefabBuilder.SheetFramePath);
			Stretch(frame);

			var content = Find(frame, "Sheet/Content").transform;

			var title = Band(content, "Title", 0f, 20f, 0f);
			Write(title, "ScreenTitle", "ABOUT", TextAlignmentOptions.TopLeft);

			var body = Band(content, "Body", 30f, 120f, 0f);
			var bodyText = Write(body, "Body", "The notice's copy goes here.", TextAlignmentOptions.TopLeft);
			bodyText.textWrappingMode = TextWrappingModes.Normal;

			var close = NestButton(content, "CloseButton", ShellPrefabBuilder.ButtonAccentPath, 170f, "CLOSE");

			var view = Ensure<NoticeOverlay>(prefab.Root);
			SetField(view, "frame", frame.GetComponent<SheetFrame>());
			SetBool(view, "dismissOnBackgroundTap", true);
			SetField(view, "title", title.GetComponent<TMP_Text>());
			SetField(view, "body", bodyText);
			SetField(view, "closeButton", close);
		}

		// Every screen root is the same three components plus a stretched rect: its own canvas and raycaster for
		// the rebuild isolation of UIPLAN 2.1, and the canvas group the transition drives.
		private static CanvasGroup OpenScreen(GameObject root)
		{
			Stretch(root);
			Ensure<Canvas>(root);
			Ensure<GraphicRaycaster>(root);

			return Ensure<CanvasGroup>(root);
		}

		// The title and the rule under it, and — on anything that can be pushed — the back control. The label on
		// it is left empty on purpose: it names the screen underneath, which is a fact about the stack, so the
		// navigator writes it on arrival (UIPLAN 3.3).
		private static (TMP_Text Title, LetterpressButton? Back) Chrome(GameObject root, bool back)
		{
			float gutter = Theme.Current.Layout.PageGutter;

			var title = Band(root.transform, "Title", TitleRow, TitleHeight, gutter);
			var titleText = Write(title, "ScreenTitle", string.Empty, TextAlignmentOptions.TopLeft);

			var rule = NestRule(root.transform, "HeaderRule", RuleWeight.Heavy, "RuleHard");
			Pin(
				rule,
				TopLeft,
				TopRight,
				TopCentre,
				new Vector2(0f, -RuleRow),
				new Vector2(gutter * -2f, Theme.Current.Layout.HeavyRule));

			if (!back)
			{
				return (titleText, null);
			}

			var button = NestPrefab(root.transform, "BackButton", ShellPrefabBuilder.ButtonQuietPath);
			Pin(
				button,
				TopLeft,
				TopLeft,
				TopLeft,
				new Vector2(gutter, -BackRow),
				new Vector2(BackWidth, BackHeight));

			return (titleText, button.GetComponent<LetterpressButton>());
		}

		private static LetterpressButton NestButton(
			Transform parent,
			string name,
			string prefabPath,
			float row,
			string label)
		{
			var instance = NestPrefab(parent, name, prefabPath);
			Pin(instance, TopCentre, TopCentre, TopCentre, new Vector2(0f, -row), new Vector2(ButtonWidth, ButtonHeight));

			// Straight onto the label rather than through LetterpressButton.Text: SetText marks the label's
			// input as a pre-parsed buffer, and the serialized string is what a prefab actually stores.
			var text = Find(instance, "Face/Fill/Label").GetComponent<TMP_Text>();
			text.text = label;
			EditorUtility.SetDirty(text);

			return instance.GetComponent<LetterpressButton>();
		}

		// The same primitive the feed's real grid will open with — here so the placeholder is built out of the
		// pieces phase 5 keeps, not out of stand-ins phase 5 throws away.
		private static GameObject NestSectionHeader(Transform parent, string name, float row, string title)
		{
			var instance = NestPrefab(parent, name, ShellPrefabBuilder.SectionHeaderPath);
			float gutter = Theme.Current.Layout.PageGutter;

			var rect = instance.GetComponent<RectTransform>();
			Pin(
				instance,
				TopLeft,
				TopRight,
				TopCentre,
				new Vector2(0f, -row),
				new Vector2(gutter * -2f, rect.rect.height));

			var text = Find(instance, "Title").GetComponent<TMP_Text>();
			text.text = title;
			EditorUtility.SetDirty(text);

			return instance;
		}

		private static GameObject NestPrefab(Transform parent, string name, string prefabPath)
		{
			var existing = parent.Find(name);

			if (existing != null)
			{
				return existing.gameObject;
			}

			var source = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

			if (source == null)
			{
				throw new InvalidOperationException(
					$"{nameof(ShellScreenBuilder)}: {prefabPath} has not been built yet.");
			}

			var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
			instance.name = name;

			return instance;
		}

		// A full-width band, inset by the page gutter, hanging from the top of the page.
		private static GameObject Band(Transform parent, string name, float row, float height, float inset)
		{
			var target = Child(parent, name);
			Pin(target, TopLeft, TopRight, TopCentre, new Vector2(0f, -row), new Vector2(inset * -2f, height));

			return target;
		}

		private static void BuildScreenCatalog()
		{
			var catalog = LoadOrCreate<ScreenCatalog>(ScreenCatalogPath);
			var serialized = new SerializedObject(catalog);
			var entries = serialized.FindProperty("entries");
			entries.arraySize = Screens.Length;

			for (var i = 0; i < Screens.Length; i++)
			{
				var (id, path, title) = Screens[i];
				var element = entries.GetArrayElementAtIndex(i);

				element.FindPropertyRelative("id").intValue = (int)id;
				element.FindPropertyRelative("view").objectReferenceValue = Require<ScreenView>(path);
				element.FindPropertyRelative("title").stringValue = title;
			}

			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(catalog);

			Report(catalog.Validate(), ScreenCatalogPath);
		}

		private static void BuildOverlayCatalog()
		{
			var catalog = LoadOrCreate<OverlayCatalog>(OverlayCatalogPath);
			var serialized = new SerializedObject(catalog);
			var entries = serialized.FindProperty("entries");
			entries.arraySize = Overlays.Length;

			for (var i = 0; i < Overlays.Length; i++)
			{
				var (id, path) = Overlays[i];
				var element = entries.GetArrayElementAtIndex(i);

				element.FindPropertyRelative("id").intValue = (int)id;
				element.FindPropertyRelative("view").objectReferenceValue = Require<OverlayView>(path);
			}

			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(catalog);

			Report(catalog.Validate(), OverlayCatalogPath);
		}

		private static void Report(IReadOnlyList<string> complaints, string path)
		{
			foreach (string complaint in complaints)
			{
				Debug.LogError($"{nameof(ShellScreenBuilder)}: {path} — {complaint}");
			}
		}

		private static T Require<T>(string prefabPath) where T : Component
		{
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
			var component = prefab == null ? null : prefab.GetComponent<T>();

			if (component == null)
			{
				throw new InvalidOperationException(
					$"{nameof(ShellScreenBuilder)}: {prefabPath} carries no {typeof(T).Name}.");
			}

			return component;
		}

		private static T LoadOrCreate<T>(string path) where T : ScriptableObject
		{
			var existing = AssetDatabase.LoadAssetAtPath<T>(path);

			if (existing != null)
			{
				return existing;
			}

			var created = ScriptableObject.CreateInstance<T>();
			AssetDatabase.CreateAsset(created, path);

			return created;
		}

		private static void SetArray<T>(UnityEngine.Object target, string field, IReadOnlyList<T> values)
			where T : Component
		{
			var serialized = new SerializedObject(target);
			var property = serialized.FindProperty(field);

			if (property == null)
			{
				throw new InvalidOperationException(
					$"{nameof(ShellScreenBuilder)}: no serialized field '{field}' on {target.GetType().Name}.");
			}

			property.arraySize = values.Count;

			for (var i = 0; i < values.Count; i++)
			{
				property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
			}

			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(target);
		}
	}
}
