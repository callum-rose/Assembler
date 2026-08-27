using System;
using Assembler.Shell.Theming;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assembler.Shell.Editor
{
	/// <summary>
	/// The small operations every shell builder needs: finding or creating a child, stretching a rect, writing a
	/// private serialized field, and looking a theme member up by name.
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
	}
}
