using System;
using System.IO;
using Assembler.Behaviours.UI;
using Assembler.Behaviours.UI.Views;
using TMPro;
using UnityEditor;
using UnityEngine;
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
		private const string LibraryPath = UiFolder + "/UiPrefabLibrary.asset";

		[MenuItem("Assembler/UI/Generate UI Prefabs")]
		public static void Generate()
		{
			WarnIfTmpEssentialsMissing();
			EnsureFolder();

			var buttonPrefab = SaveAsPrefab(BuildButton(), ButtonPath);
			var labelPrefab = SaveAsPrefab(BuildLabel(), LabelPath);
			var sliderPrefab = SaveAsPrefab(BuildSlider(), SliderPath);

			var library = AssetDatabase.LoadAssetAtPath<UiPrefabLibrary>(LibraryPath);
			if (library == null)
			{
				library = ScriptableObject.CreateInstance<UiPrefabLibrary>();
				AssetDatabase.CreateAsset(library, LibraryPath);
			}

			Wire(library, "buttonPrefab", buttonPrefab);
			Wire(library, "labelPrefab", labelPrefab);
			Wire(library, "sliderPrefab", sliderPrefab);

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
