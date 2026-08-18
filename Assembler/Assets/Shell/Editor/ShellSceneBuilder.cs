using System;
using System.Linq;
using Assembler.Shell.Composition;
using Assembler.Shell.Layout;
using Assembler.Shell.Theming;
using EasyDI.Unity;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Assembler.Shell.Editor
{
	/// <summary>
	/// Grows the shell into <c>Bootstrap.unity</c>: the root canvas and its three layered hosts, the authored
	/// EventSystem, and the shell lifetime scope with its installer — plus the application scope prefab and the
	/// EasyDI settings asset that the scope chain hangs from.
	/// </summary>
	/// <remarks>
	/// Additive and re-runnable. Objects are found by name and configured in place; nothing is destroyed, so
	/// running it again after the shell has grown content leaves that content alone.
	/// </remarks>
	public static class ShellSceneBuilder
	{
		public const string BootstrapScenePath = "Assets/Scenes/Bootstrap.unity";
		public const string ApplicationScopePrefabPath = "Assets/Shell/ApplicationScope.prefab";
		public const string EasyDiSettingsPath = "Assets/Shell/EasyDISettings.asset";

		private const string ShellRootName = "ShellRoot";
		private const string EventSystemName = "EventSystem";
		private const string ShellScopeName = "ShellScope";
		private const string SafeAreaName = "SafeArea";

		[MenuItem("Assembler/Shell/Build Shell Root")]
		public static void BuildShell()
		{
			var font = NewsreaderFontAssetBuilder.EnsureBaked();
			ShellAssetBuilder.EnsureTheme(font);
			ShellAssetBuilder.EnsureConfig();

			var applicationScope = EnsureApplicationScopePrefab();
			EnsureEasyDiSettings(applicationScope);
			AssetDatabase.SaveAssets();

			var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);

			// Loaded after the scene, not before: opening a scene unloads assets nothing is holding, which
			// fake-nulls any reference taken earlier — and a fake-null serialises as an empty field rather than
			// failing loudly.
			var theme = AssetDatabase.LoadAssetAtPath<ShellTheme>(ShellAssetBuilder.ThemeAssetPath);
			var config = AssetDatabase.LoadAssetAtPath<ShellConfig>(ShellAssetBuilder.ConfigAssetPath);

			var shellRoot = BuildShellRoot(scene);
			BuildEventSystem(scene);
			BuildShellScope(scene, theme, config, shellRoot);

			EditorSceneManager.MarkSceneDirty(scene);
			EditorSceneManager.SaveScene(scene);
			AssetDatabase.SaveAssets();

			Debug.Log($"{nameof(ShellSceneBuilder)}: shell built into {BootstrapScenePath}.");
		}

		private static ShellRoot BuildShellRoot(Scene scene)
		{
			var rootObject = FindOrCreateRoot(scene, ShellRootName);
			EnsureComponent<RectTransform>(rootObject);

			var canvas = EnsureComponent<Canvas>(rootObject);
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;

			var scaler = EnsureComponent<CanvasScaler>(rootObject);
			EnsureComponent<GraphicRaycaster>(rootObject);

			// The shim re-points these on rotation; they are written here too so the scene reads correctly in the
			// editor without entering play mode.
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
			scaler.referenceResolution = new Vector2(390f, 844f);
			scaler.matchWidthOrHeight = 0f;

			EnsureComponent<ShortAxisCanvasScaler>(rootObject);

			var screenHost = BuildHost(rootObject.transform, "ScreenHost", siblingIndex: 0);
			var gameStrip = BuildHost(rootObject.transform, "GameStrip", siblingIndex: 1);
			var overlayHost = BuildHost(rootObject.transform, "OverlayHost", siblingIndex: 2);

			var shellRoot = EnsureComponent<ShellRoot>(rootObject);
			SetField(shellRoot, "rootCanvas", canvas);
			SetField(shellRoot, "screenHost", screenHost);
			SetField(shellRoot, "gameStrip", gameStrip);
			SetField(shellRoot, "overlayHost", overlayHost);

			return shellRoot;
		}

		// Each host is a full-bleed rect with its own nested canvas — the rebuild isolation of UIPLAN 2.1 — and a
		// safe-area child for content that has to clear the notch.
		private static ShellHost BuildHost(Transform parent, string name, int siblingIndex)
		{
			var hostObject = FindOrCreateChild(parent, name);
			hostObject.transform.SetSiblingIndex(siblingIndex);

			var rect = StretchFull(hostObject);
			var canvas = EnsureComponent<Canvas>(hostObject);
			EnsureComponent<GraphicRaycaster>(hostObject);

			var safeAreaObject = FindOrCreateChild(hostObject.transform, SafeAreaName);
			StretchFull(safeAreaObject);
			var safeArea = EnsureComponent<SafeAreaPanel>(safeAreaObject);

			var host = EnsureComponent<ShellHost>(hostObject);
			SetField(host, "canvas", canvas);
			SetField(host, "rect", rect);
			SetField(host, "safeArea", safeArea);

			return host;
		}

		// Replaces the EventSystem the Builder used to stand up at runtime: uGUI needs exactly one, and the
		// project is Input System-only, so it is authored here with the Input System module.
		private static void BuildEventSystem(Scene scene)
		{
			var eventSystemObject = FindOrCreateRoot(scene, EventSystemName);

			EnsureComponent<EventSystem>(eventSystemObject);
			EnsureComponent<InputSystemUIInputModule>(eventSystemObject);
		}

		private static void BuildShellScope(Scene scene, ShellTheme theme, ShellConfig config, ShellRoot shellRoot)
		{
			var scopeObject = FindOrCreateRoot(scene, ShellScopeName);

			var scope = EnsureComponent<ShellLifetimeScope>(scopeObject);
			var installer = EnsureComponent<ShellInstaller>(scopeObject);

			SetField(installer, "theme", theme);
			SetField(installer, "config", config);
			SetField(installer, "shellRoot", shellRoot);

			SetField(scope, "installer", installer);
		}

		private static ApplicationLifetimeScope EnsureApplicationScopePrefab()
		{
			var existing = AssetDatabase.LoadAssetAtPath<GameObject>(ApplicationScopePrefabPath);

			if (existing != null)
			{
				var component = existing.GetComponent<ApplicationLifetimeScope>();

				if (component != null)
				{
					return component;
				}
			}

			var temporary = new GameObject("ApplicationScope");

			try
			{
				temporary.AddComponent<ApplicationLifetimeScope>();
				var prefab = PrefabUtility.SaveAsPrefabAsset(temporary, ApplicationScopePrefabPath);

				return prefab.GetComponent<ApplicationLifetimeScope>();
			}
			finally
			{
				Object.DestroyImmediate(temporary);
			}
		}

		// EasyDI instantiates the root scope prefab named here before the first scene loads, and throws at every
		// play if no settings asset exists — so installing the package obliges us to author one.
		private static void EnsureEasyDiSettings(ApplicationLifetimeScope applicationScope)
		{
			string[] existing = AssetDatabase.FindAssets($"t:{nameof(EasyDISettings)}");

			if (existing.Length > 1)
			{
				Debug.LogError(
					$"{nameof(ShellSceneBuilder)}: {existing.Length} {nameof(EasyDISettings)} assets exist; EasyDI " +
					"requires exactly one. Delete the extras: " +
					string.Join(", ", existing.Select(AssetDatabase.GUIDToAssetPath)));
				return;
			}

			var settings = existing.Length == 1
				? AssetDatabase.LoadAssetAtPath<EasyDISettings>(AssetDatabase.GUIDToAssetPath(existing[0]))
				: CreateEasyDiSettings();

			SetField(settings, "rootLifetimeScope", applicationScope);
		}

		private static EasyDISettings CreateEasyDiSettings()
		{
			var settings = ScriptableObject.CreateInstance<EasyDISettings>();
			AssetDatabase.CreateAsset(settings, EasyDiSettingsPath);

			return settings;
		}

		private static GameObject FindOrCreateRoot(Scene scene, string name)
		{
			var existing = scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);

			if (existing != null)
			{
				return existing;
			}

			var created = new GameObject(name);
			SceneManager.MoveGameObjectToScene(created, scene);

			return created;
		}

		private static GameObject FindOrCreateChild(Transform parent, string name)
		{
			var existing = parent.Find(name);

			if (existing != null)
			{
				return existing.gameObject;
			}

			var created = new GameObject(name, typeof(RectTransform));
			created.transform.SetParent(parent, worldPositionStays: false);

			return created;
		}

		private static RectTransform StretchFull(GameObject target)
		{
			var rect = EnsureComponent<RectTransform>(target);

			// Vector2 is forced here by the RectTransform anchor API.
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			rect.localScale = Vector3.one;
			rect.localRotation = Quaternion.identity;

			return rect;
		}

		private static T EnsureComponent<T>(GameObject target) where T : Component
		{
			var existing = target.GetComponent<T>();

			return existing != null ? existing : target.AddComponent<T>();
		}

		private static void SetField(Object target, string field, Object value)
		{
			var serialized = new SerializedObject(target);
			var property = serialized.FindProperty(field);

			if (property == null)
			{
				throw new InvalidOperationException(
					$"{nameof(ShellSceneBuilder)}: no serialized field '{field}' on {target.GetType().Name}.");
			}

			// A destroyed or unloaded asset assigns as an empty field instead of failing, so the wiring silently
			// comes out half-done. Catch it here rather than in a null reference at play time.
			if (value == null)
			{
				throw new InvalidOperationException(
					$"{nameof(ShellSceneBuilder)}: nothing to assign to '{field}' on {target.GetType().Name}.");
			}

			property.objectReferenceValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(target);
		}
	}
}
