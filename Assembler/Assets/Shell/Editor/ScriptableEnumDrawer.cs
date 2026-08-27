using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Shell.Theming;
using UnityEditor;
using UnityEngine;

namespace Assembler.Shell.Editor
{
	/// <summary>
	/// Draws a <see cref="ScriptableEnum"/> field as a dropdown of every member asset of its type, so an
	/// asset-backed enum authors like a real one rather than like a drag-and-drop object slot.
	/// </summary>
	[CustomPropertyDrawer(typeof(ScriptableEnum), useForChildren: true)]
	public sealed class ScriptableEnumDrawer : PropertyDrawer
	{
		// One AssetDatabase search per member type, not one per repaint: the theme inspector alone draws 37 of
		// these fields. Cleared whenever the project changes, so a new member shows up without a reload.
		private static readonly Dictionary<Type, ScriptableEnum[]> MembersByType = new();

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var memberType = MemberType();

			if (memberType is null || property.propertyType != SerializedPropertyType.ObjectReference)
			{
				EditorGUI.PropertyField(position, property, label);
				return;
			}

			var members = Members(memberType);
			var current = property.objectReferenceValue as ScriptableEnum;

			// A member the search missed — one outside the project, or mid-rename — still has to read as itself
			// rather than silently as "None".
			if (current != null && !members.Contains(current))
			{
				members = members.Append(current).ToArray();
			}

			var options = new GUIContent[members.Length + 1];
			options[0] = new GUIContent("None");

			for (int i = 0; i < members.Length; i++)
			{
				options[i + 1] = new GUIContent(members[i].name, members[i].Description);
			}

			int index = current == null ? 0 : Array.IndexOf(members, current) + 1;

			EditorGUI.BeginProperty(position, label, property);
			EditorGUI.BeginChangeCheck();

			index = EditorGUI.Popup(position, label, index, options);

			if (EditorGUI.EndChangeCheck())
			{
				property.objectReferenceValue = index == 0 ? null : members[index - 1];
			}

			EditorGUI.EndProperty();
		}

		[InitializeOnLoadMethod]
		private static void InvalidateCacheOnProjectChange()
		{
			EditorApplication.projectChanged += MembersByType.Clear;
		}

		private static ScriptableEnum[] Members(Type memberType)
		{
			if (MembersByType.TryGetValue(memberType, out var cached))
			{
				return cached;
			}

			var members = AssetDatabase.FindAssets($"t:{memberType.Name}")
				.Select(AssetDatabase.GUIDToAssetPath)
				.Select(path => AssetDatabase.LoadAssetAtPath(path, memberType) as ScriptableEnum)
				.Where(member => member != null)
				.Select(member => member!)
				.OrderBy(member => member.name, StringComparer.Ordinal)
				.ToArray();

			MembersByType[memberType] = members;
			return members;
		}

		// The drawn field may be the member itself, or an array/list of them.
		private Type? MemberType()
		{
			var type = fieldInfo?.FieldType;

			if (type is null)
			{
				return null;
			}

			if (type.IsArray)
			{
				type = type.GetElementType();
			}
			else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
			{
				type = type.GetGenericArguments()[0];
			}

			return type is not null && typeof(ScriptableEnum).IsAssignableFrom(type) ? type : null;
		}
	}
}
