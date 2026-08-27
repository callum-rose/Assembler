using System;
using Assembler.Shell.Controls;
using Assembler.Shell.Theming;
using Assembler.Shell.Theming.Binders;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Assembler.Shell.Editor
{
	/// <summary>
	/// The operations every shell builder needs: finding or creating a child, stretching and pinning rects,
	/// writing private serialized fields, looking theme members up by name, painting and lettering objects from
	/// the theme, and opening a prefab to author in place.
	/// </summary>
	internal static class ShellBuildUtility
	{
		/// <summary>The child of <paramref name="parent"/> called <paramref name="name"/>, created if absent.</summary>
		public static GameObject Child(Transform parent, string name)
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

		/// <summary>Stretches a rect to fill its parent, inset by the given offsets.</summary>
		public static RectTransform Stretch(GameObject target, Vector2 offsetMin = default, Vector2 offsetMax = default)
		{
			var rect = Ensure<RectTransform>(target);

			// Vector2 is forced here by the RectTransform anchor API.
			rect.anchorMin = Vector2.zero;
			rect.anchorMax = Vector2.one;
			rect.pivot = new Vector2(0.5f, 0.5f);
			rect.offsetMin = offsetMin;
			rect.offsetMax = offsetMax;
			rect.localScale = Vector3.one;
			rect.localRotation = Quaternion.identity;

			return rect;
		}

		/// <summary>Anchors a rect to one corner or edge of its parent at a fixed size.</summary>
		public static RectTransform Pin(
			GameObject target,
			Vector2 anchorMin,
			Vector2 anchorMax,
			Vector2 pivot,
			Vector2 anchoredPosition,
			Vector2 size)
		{
			var rect = Ensure<RectTransform>(target);

			rect.anchorMin = anchorMin;
			rect.anchorMax = anchorMax;
			rect.pivot = pivot;
			rect.anchoredPosition = anchoredPosition;
			rect.sizeDelta = size;
			rect.localScale = Vector3.one;
			rect.localRotation = Quaternion.identity;

			return rect;
		}

		/// <summary>The component of type <typeparamref name="T"/> on <paramref name="target"/>, added if absent.</summary>
		public static T Ensure<T>(GameObject target) where T : Component
		{
			var existing = target.GetComponent<T>();

			return existing != null ? existing : target.AddComponent<T>();
		}

		/// <summary>Writes a private serialized field. Throws rather than leaving the wiring half-done.</summary>
		public static void SetField(Object target, string field, Object? value)
		{
			var property = Property(target, field);

			// A destroyed or unloaded asset assigns as an empty field instead of failing, so the wiring silently
			// comes out half-done. Catch it here rather than in a null reference at play time.
			if (value == null)
			{
				throw new InvalidOperationException(
					$"{nameof(ShellBuildUtility)}: nothing to assign to '{field}' on {target.GetType().Name}.");
			}

			property.objectReferenceValue = value;
			Apply(property, target);
		}

		/// <summary>Clears a private serialized object field — for a variant that drops a part.</summary>
		public static void ClearField(Object target, string field)
		{
			var property = Property(target, field);
			property.objectReferenceValue = null;
			Apply(property, target);
		}

		public static void SetFloat(Object target, string field, float value)
		{
			var property = Property(target, field);
			property.floatValue = value;
			Apply(property, target);
		}

		public static void SetInt(Object target, string field, int value)
		{
			var property = Property(target, field);
			property.intValue = value;
			Apply(property, target);
		}

		public static void SetBool(Object target, string field, bool value)
		{
			var property = Property(target, field);
			property.boolValue = value;
			Apply(property, target);
		}

		/// <summary>The <see cref="ColorRole"/> asset of that name. Throws when the palette has no such member.</summary>
		public static ColorRole Role(string name)
		{
			return Member<ColorRole>(ShellAssetBuilder.RolesFolder, name);
		}

		/// <summary>The <see cref="TextStyleId"/> asset of that name. Throws when the scale has no such member.</summary>
		public static TextStyleId Style(string name)
		{
			return Member<TextStyleId>(ShellAssetBuilder.TextStylesFolder, name);
		}

		private static T Member<T>(string folder, string name) where T : ScriptableEnum
		{
			var member = AssetDatabase.LoadAssetAtPath<T>($"{folder}/{name}.asset");

			if (member == null)
			{
				throw new InvalidOperationException(
					$"{nameof(ShellBuildUtility)}: no {typeof(T).Name} called '{name}'. Run " +
					"'Assembler > Shell > Create Shell Assets' first.");
			}

			return member;
		}

		private static SerializedProperty Property(Object target, string field)
		{
			var serialized = new SerializedObject(target);
			var property = serialized.FindProperty(field);

			if (property == null)
			{
				throw new InvalidOperationException(
					$"{nameof(ShellBuildUtility)}: no serialized field '{field}' on {target.GetType().Name}.");
			}

			return property;
		}

		private static void Apply(SerializedProperty property, Object target)
		{
			property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(target);
		}

		public static GameObject NestRule(Transform parent, string name, RuleWeight weight, string role)
		{
			var existing = parent.Find(name);
			GameObject instance;

			if (existing != null)
			{
				instance = existing.gameObject;
			}
			else
			{
				var source = AssetDatabase.LoadAssetAtPath<GameObject>(ShellPrefabBuilder.RulePath);

				if (source == null)
				{
					throw new InvalidOperationException(
						$"{nameof(ShellBuildUtility)}: {ShellPrefabBuilder.RulePath} has not been built yet.");
				}

				instance = (GameObject)PrefabUtility.InstantiatePrefab(source, parent);
				instance.name = name;
			}

			var rule = instance.GetComponent<Rule>();
			SetInt(rule, "weight", (int)weight);
			Repaint(instance, "Line", role);
			Repaint(instance, "LineLower", role);
			rule.Apply();

			return instance;
		}

		// A theme-bound graphic: an Image that paints from a role and, per UIPLAN 7.4, does not raycast.
		public static Image Paint(GameObject target, string role, float alpha = 1f)
		{
			var image = Ensure<Image>(target);
			image.raycastTarget = false;

			var binder = Ensure<ThemeColor>(target);
			SetField(binder, "role", Role(role));
			SetFloat(binder, "alpha", alpha);
			binder.Apply();

			return image;
		}

		public static TextMeshProUGUI Write(
			GameObject target,
			string style,
			string text,
			TextAlignmentOptions alignment)
		{
			var label = Ensure<TextMeshProUGUI>(target);
			label.raycastTarget = false;
			label.alignment = alignment;
			label.SetText(text);

			var binder = Ensure<TextStyleBinder>(target);
			SetField(binder, "style", Style(style));
			binder.Apply();

			return label;
		}

		public static void Repaint(GameObject root, string path, string role)
		{
			var target = Find(root, path);
			var binder = target.GetComponent<ThemeColor>();

			if (binder == null)
			{
				throw new InvalidOperationException(
					$"{nameof(ShellBuildUtility)}: '{path}' carries no {nameof(ThemeColor)} to repaint.");
			}

			SetField(binder, "role", Role(role));
			binder.Apply();
		}

		public static void Restyle(GameObject root, string path, string style)
		{
			var target = Find(root, path);
			var binder = target.GetComponent<TextStyleBinder>();

			if (binder == null)
			{
				throw new InvalidOperationException(
					$"{nameof(ShellBuildUtility)}: '{path}' carries no {nameof(TextStyleBinder)} to restyle.");
			}

			SetField(binder, "style", Style(style));
			binder.Apply();
		}

		public static GameObject Find(GameObject root, string path)
		{
			var found = root.transform.Find(path);

			if (found == null)
			{
				throw new InvalidOperationException($"{nameof(ShellBuildUtility)}: no '{path}' under {root.name}.");
			}

			return found.gameObject;
		}

		public static void EnsureFolder(string path)
		{
			if (AssetDatabase.IsValidFolder(path))
			{
				return;
			}

			int separator = path.LastIndexOf('/');
			AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
		}

		// Vector2 is forced here by the RectTransform anchor API.
		public static Vector2 Centre => new(0.5f, 0.5f);

		public static Vector2 TopLeft => new(0f, 1f);

		public static Vector2 TopRight => new(1f, 1f);

		public static Vector2 TopCentre => new(0.5f, 1f);

		public static Vector2 BottomLeft => new(0f, 0f);

		public static Vector2 BottomRight => new(1f, 0f);

		public static Vector2 BottomCentre => new(0.5f, 0f);

		/// <summary>
		/// An open prefab, saved back to the same path when it closes. Opening an existing one keeps its GUID —
		/// which is what makes re-running this safe for the scenes and variants already pointing at it.
		/// </summary>
		public sealed class PrefabScope : IDisposable
		{
			private readonly string _path;
			private readonly bool _fromDisk;

			private PrefabScope(GameObject root, string path, bool fromDisk)
			{
				Root = root;
				_path = path;
				_fromDisk = fromDisk;
			}

			public GameObject Root { get; }

			public static PrefabScope Open(string path, string rootName, Scene workshop)
			{
				if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
				{
					// LoadPrefabContents opens the prefab in a preview scene of its own, so this path never
					// touches the workshop or anything the editor has open.
					return new PrefabScope(PrefabUtility.LoadPrefabContents(path), path, fromDisk: true);
				}

				var root = new GameObject(rootName, typeof(RectTransform));
				SceneManager.MoveGameObjectToScene(root, workshop);

				return new PrefabScope(root, path, fromDisk: false);
			}

			/// <summary>Opens a variant of <paramref name="basePath"/>, creating it from an instance if absent.</summary>
			public static PrefabScope OpenVariant(string path, string basePath, string rootName, Scene workshop)
			{
				if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
				{
					return new PrefabScope(PrefabUtility.LoadPrefabContents(path), path, fromDisk: true);
				}

				var source = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);

				if (source == null)
				{
					throw new InvalidOperationException(
						$"{nameof(ShellBuildUtility)}: {basePath} has not been built yet.");
				}

				// Saving an instance of a prefab as a prefab is what makes the result a variant rather than a
				// copy — so the base keeps driving everything this one does not override.
				var instance = (GameObject)PrefabUtility.InstantiatePrefab(source, workshop);
				instance.name = rootName;

				return new PrefabScope(instance, path, fromDisk: false);
			}

			public void Dispose()
			{
				PrefabUtility.SaveAsPrefabAsset(Root, _path);

				if (_fromDisk)
				{
					PrefabUtility.UnloadPrefabContents(Root);
					return;
				}

				Object.DestroyImmediate(Root);
			}
		}
	}
}
