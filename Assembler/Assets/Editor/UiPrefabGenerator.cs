using System;
using System.IO;
using Assembler.Behaviours.UI;
using Assembler.Behaviours.UI.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace Editor
{
	/// <summary>
	/// Generates the baseline reusable UI prefabs (button, label, slider) and the <see cref="UiPrefabLibrary"/>
	/// asset that the runtime UI blocks instantiate. This is the "hybrid" authoring seam: it produces working,
	/// unstyled prefabs so the UI blocks function immediately; restyle or replace the prefabs in the editor
	/// afterwards (keep the view-component references wired) without touching code.
	///
	/// Run via the menu "Assembler > UI > Generate UI Prefabs".
	/// </summary>
	public static class UiPrefabGenerator
	{
		private const string UiFolder = "Assets/Resources/UI";
		private const string ButtonPath = UiFolder + "/UiButton.prefab";
		private const string LabelPath = UiFolder + "/UiLabel.prefab";
		private const string SliderPath = UiFolder + "/UiSlider.prefab";
		private const string JoystickPath = UiFolder + "/OnScreenJoystick.prefab";
		private const string DPadPath = UiFolder + "/OnScreenDPad.prefab";
		private const string MobileButtonPath = UiFolder + "/OnScreenButton.prefab";
		private const string LibraryPath = UiFolder + "/UiPrefabLibrary.asset";

		[MenuItem("Assembler/UI/Generate UI Prefabs")]
		public static void Generate()
		{
			WarnIfTmpEssentialsMissing();
			EnsureFolder();

			var buttonPrefab = SaveAsPrefab(BuildButton(), ButtonPath);
			var labelPrefab = SaveAsPrefab(BuildLabel(), LabelPath);
			var sliderPrefab = SaveAsPrefab(BuildSlider(), SliderPath);
			var joystickPrefab = SaveAsPrefab(BuildJoystick(), JoystickPath);
			var dpadPrefab = SaveAsPrefab(BuildDPad(), DPadPath);
			var mobileButtonPrefab = SaveAsPrefab(BuildMobileButton(), MobileButtonPath);

			var library = AssetDatabase.LoadAssetAtPath<UiPrefabLibrary>(LibraryPath);
			if (library == null)
			{
				library = ScriptableObject.CreateInstance<UiPrefabLibrary>();
				AssetDatabase.CreateAsset(library, LibraryPath);
			}

			Wire(library, "buttonPrefab", buttonPrefab);
			Wire(library, "labelPrefab", labelPrefab);
			Wire(library, "sliderPrefab", sliderPrefab);
			Wire(library, "joystickPrefab", joystickPrefab);
			Wire(library, "dpadPrefab", dpadPrefab);
			Wire(library, "mobileButtonPrefab", mobileButtonPrefab);

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log($"UiPrefabGenerator: wrote prefabs and library under {UiFolder}.");
		}

		private static void EnsureFolder()
		{
			if (!AssetDatabase.IsValidFolder("Assets/Resources"))
			{
				AssetDatabase.CreateFolder("Assets", "Resources");
			}

			if (!AssetDatabase.IsValidFolder(UiFolder))
			{
				AssetDatabase.CreateFolder("Assets/Resources", "UI");
			}
		}

		private static GameObject BuildButton()
		{
			// Image (background) + Button on the root, with a stretched TextMeshPro caption child.
			var go = new GameObject("UiButton", typeof(RectTransform));
			var image = go.AddComponent<Image>();
			var button = go.AddComponent<Button>();
			button.targetGraphic = image;

			var labelGo = new GameObject("Label", typeof(RectTransform));
			var labelRect = (RectTransform)labelGo.transform;
			labelRect.SetParent(go.transform, worldPositionStays: false);
			labelRect.anchorMin = Vector2.zero;
			labelRect.anchorMax = Vector2.one;
			labelRect.offsetMin = Vector2.zero;
			labelRect.offsetMax = Vector2.zero;

			var label = labelGo.AddComponent<TextMeshProUGUI>();
			label.alignment = TextAlignmentOptions.Center;
			label.color = Color.black;
			label.text = "Button";

			var view = go.AddComponent<UiButtonView>();
			Wire(view, "button", button);
			Wire(view, "label", label);
			return go;
		}

		private static GameObject BuildLabel()
		{
			var go = new GameObject("UiLabel", typeof(RectTransform));
			var text = go.AddComponent<TextMeshProUGUI>();
			text.alignment = TextAlignmentOptions.Center;
			text.text = string.Empty;

			var view = go.AddComponent<UiLabelView>();
			Wire(view, "text", text);
			return go;
		}

		private static GameObject BuildSlider()
		{
			var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
			go.name = "UiSlider";

			var view = go.AddComponent<UiSliderView>();
			Wire(view, "slider", go.GetComponent<Slider>());
			return go;
		}

		private static GameObject BuildJoystick()
		{
			// Background ring with a draggable handle child carrying the OnScreenStick. The stick moves the
			// handle within movementRange and synthesises a virtual stick the mobile binding reads. Sizes are a
			// baseline; the builder overrides the root RectTransform size from the descriptor's Size.
			var go = new GameObject("OnScreenJoystick", typeof(RectTransform));
			var bgRect = (RectTransform)go.transform;
			bgRect.sizeDelta = new Vector2(320f, 320f);
			var background = go.AddComponent<Image>();
			background.color = new Color(1f, 1f, 1f, 0.2f);

			var handleGo = new GameObject("Handle", typeof(RectTransform));
			var handleRect = (RectTransform)handleGo.transform;
			handleRect.SetParent(go.transform, worldPositionStays: false);
			handleRect.anchorMin = new Vector2(0.5f, 0.5f);
			handleRect.anchorMax = new Vector2(0.5f, 0.5f);
			handleRect.pivot = new Vector2(0.5f, 0.5f);
			handleRect.anchoredPosition = Vector2.zero;
			handleRect.sizeDelta = new Vector2(140f, 140f);
			var handleImage = handleGo.AddComponent<Image>();
			handleImage.color = new Color(1f, 1f, 1f, 0.5f);

			// OnScreenStick lives on the handle: it moves its own transform on drag. movementRange caps the
			// handle travel (in px) that maps to a full-deflection stick value.
			var stick = handleGo.AddComponent<OnScreenStick>();
			stick.movementRange = 90f;

			var view = go.AddComponent<OnScreenJoystickView>();
			Wire(view, "stick", stick);
			Wire(view, "background", bgRect);
			Wire(view, "handle", handleRect);
			return go;
		}

		private static GameObject BuildMobileButton()
		{
			// Image + OnScreenButton on the root (the OnScreenButton equivalent of BuildButton's uGUI Button),
			// with a stretched TextMeshPro caption child the builder sets per widget.
			var go = new GameObject("OnScreenButton", typeof(RectTransform));
			((RectTransform)go.transform).sizeDelta = new Vector2(200f, 200f);
			var image = go.AddComponent<Image>();
			image.color = new Color(1f, 1f, 1f, 0.35f);
			var button = go.AddComponent<OnScreenButton>();

			var labelGo = new GameObject("Label", typeof(RectTransform));
			var labelRect = (RectTransform)labelGo.transform;
			labelRect.SetParent(go.transform, worldPositionStays: false);
			labelRect.anchorMin = Vector2.zero;
			labelRect.anchorMax = Vector2.one;
			labelRect.offsetMin = Vector2.zero;
			labelRect.offsetMax = Vector2.zero;

			var label = labelGo.AddComponent<TextMeshProUGUI>();
			label.alignment = TextAlignmentOptions.Center;
			label.color = Color.black;
			label.text = "Button";

			var view = go.AddComponent<OnScreenButtonView>();
			Wire(view, "button", button);
			Wire(view, "image", image);
			Wire(view, "label", label);
			return go;
		}

		private static GameObject BuildDPad()
		{
			// Four directional OnScreenButtons laid out in a cross; the builder points each at the matching
			// sub-control of the action's base path (<base>/up|down|left|right) so they read back as a Vector2.
			var go = new GameObject("OnScreenDPad", typeof(RectTransform));
			((RectTransform)go.transform).sizeDelta = new Vector2(300f, 300f);

			var up = BuildDPadButton("Up", go.transform, new Vector2(0.5f, 1f), new Vector2(0f, -50f));
			var down = BuildDPadButton("Down", go.transform, new Vector2(0.5f, 0f), new Vector2(0f, 50f));
			var left = BuildDPadButton("Left", go.transform, new Vector2(0f, 0.5f), new Vector2(50f, 0f));
			var right = BuildDPadButton("Right", go.transform, new Vector2(1f, 0.5f), new Vector2(-50f, 0f));

			var view = go.AddComponent<OnScreenDPadView>();
			Wire(view, "up", up);
			Wire(view, "down", down);
			Wire(view, "left", left);
			Wire(view, "right", right);
			return go;
		}

		private static OnScreenButton BuildDPadButton(string name, Transform parent, Vector2 anchor, Vector2 anchoredPosition)
		{
			var go = new GameObject(name, typeof(RectTransform));
			var rect = (RectTransform)go.transform;
			rect.SetParent(parent, worldPositionStays: false);
			rect.anchorMin = anchor;
			rect.anchorMax = anchor;
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.sizeDelta = new Vector2(90f, 90f);
			rect.anchoredPosition = anchoredPosition;

			var image = go.AddComponent<Image>();
			image.color = new Color(1f, 1f, 1f, 0.35f);

			return go.AddComponent<OnScreenButton>();
		}

		// Assigns a [SerializeField] private object-reference field by name, so the views/library can keep
		// their runtime surface read-only (private field + public getter) while still being wired here.
		private static void Wire(UnityEngine.Object target, string field, UnityEngine.Object value)
		{
			var serialized = new SerializedObject(target);
			var property = serialized.FindProperty(field);
			if (property == null)
			{
				throw new InvalidOperationException(
					$"UiPrefabGenerator: no serialized field '{field}' on {target.GetType().Name}.");
			}

			property.objectReferenceValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		private static GameObject SaveAsPrefab(GameObject instance, string path)
		{
			var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
			UnityEngine.Object.DestroyImmediate(instance);
			return prefab;
		}

		private static void WarnIfTmpEssentialsMissing()
		{
			// TextMeshPro needs its "Essentials" imported (a default font asset) for text to render. If the
			// default font asset is missing, prefabs are still generated but text will be invisible at runtime.
			try
			{
				if (TMP_Settings.defaultFontAsset == null)
				{
					Debug.LogWarning(
						"UiPrefabGenerator: TMP default font asset not found. Import it via " +
						"'Window > TextMeshPro > Import TMP Essential Resources', then re-run this generator.");
				}
			}
			catch (Exception)
			{
				Debug.LogWarning(
					"UiPrefabGenerator: TMP_Settings unavailable — import TMP Essential Resources " +
					"('Window > TextMeshPro > Import TMP Essential Resources') before generating UI prefabs.");
			}
		}
	}
}
