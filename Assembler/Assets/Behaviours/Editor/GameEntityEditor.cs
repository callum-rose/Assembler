using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Assembler.Core;
using Assembler.Resolving;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Assembler.Behaviours.Editor
{
	/// <summary>
	/// Inspector for <see cref="GameEntity"/> that surfaces the entity's runtime variable scope so per-entity
	/// state can be watched and edited live while the game runs. The scope is populated by the build pipeline at
	/// runtime (it is null in edit mode), so the section only shows variables in Play mode. Drives off the
	/// provider's declared type rather than a hand-maintained type list, so any value a variable can hold —
	/// the simple types, enums, lists of them, and <see cref="Record"/> bags — is drawn without per-type wiring.
	/// </summary>
	[CustomEditor(typeof(GameEntity))]
	public sealed class GameEntityEditor : OdinEditor
	{
		// Variables ignore the context, so a single empty one is enough for every read.
		private static readonly TriggerContext ReadContext = TriggerContext.Empty;

		// Persisted foldout state keyed by display path, so nested records/lists stay expanded across repaints.
		private readonly Dictionary<string, bool> _foldouts = new();

		// Repaint every frame in Play mode so the displayed values track the running game.
		public override bool RequiresConstantRepaint() => Application.isPlaying;

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Entity Variables", EditorStyles.boldLabel);

			if (!Application.isPlaying)
			{
				EditorGUILayout.HelpBox(
					"Entity variables are created at runtime — enter Play mode to inspect and edit them.",
					MessageType.Info);
				return;
			}

			var scope = ((GameEntity)target).VariableScope;
			if (scope is null)
			{
				EditorGUILayout.HelpBox("This entity has no variable scope.", MessageType.None);
				return;
			}

			var variables = scope.All.ToList();
			if (variables.Count == 0)
			{
				EditorGUILayout.HelpBox("This entity has no variables.", MessageType.None);
				return;
			}

			foreach (var (id, provider) in variables)
			{
				DrawVariable(id, provider);
			}
		}

		private void DrawVariable(string id, IValueProvider provider)
		{
			var type = DeclaredType(provider);

			object? current;
			try
			{
				current = provider.Get(ReadContext);
			}
			catch (Exception e)
			{
				EditorGUILayout.LabelField(id, $"<unreadable: {e.Message}>");
				return;
			}

			EditorGUI.BeginChangeCheck();
			var updated = DrawValue(id, type, current);
			// Records and lists mutate in place, so writing the same reference back is a harmless no-op; scalars
			// produce a fresh boxed value that genuinely needs storing. TrySet rejects read-only providers.
			if (EditorGUI.EndChangeCheck())
			{
				TrySet(provider, type, updated);
			}
		}

		// Draws an editable field and returns the value to store back. Records and lists are reference types
		// edited in place, so they return the same instance; scalars return the (possibly changed) boxed value.
		private object? DrawValue(string label, Type type, object? value)
		{
			switch (value)
			{
				case Record record:
					DrawRecord(label, record);
					return record;
				case IList list:
					DrawList(label, type, list);
					return list;
				default:
					return DrawScalar(label, type, value);
			}
		}

		private object? DrawScalar(string label, Type type, object? value)
		{
			if (type == typeof(int))
			{
				return EditorGUILayout.IntField(label, value is int i ? i : 0);
			}

			if (type == typeof(float))
			{
				return EditorGUILayout.FloatField(label, value is float f ? f : 0f);
			}

			if (type == typeof(bool))
			{
				return EditorGUILayout.Toggle(label, value is true);
			}

			if (type == typeof(string))
			{
				return EditorGUILayout.TextField(label, value as string ?? string.Empty);
			}

			if (type == typeof(Vector3))
			{
				return EditorGUILayout.Vector3Field(label, value is Vector3 v ? v : Vector3.zero);
			}

			if (type == typeof(Color))
			{
				return EditorGUILayout.ColorField(label, value is Color c ? c : Color.white);
			}

			if (type.IsEnum)
			{
				return EditorGUILayout.EnumPopup(label, (Enum)(value ?? Enum.GetValues(type).GetValue(0)));
			}

			// Unknown type: show it read-only rather than risk an editor that can't round-trip the value.
			EditorGUILayout.LabelField(label, value?.ToString() ?? "null");
			return value;
		}

		private void DrawRecord(string label, Record record)
		{
			if (!Foldout($"record:{label}", $"{label}  ({record.TypeName})"))
			{
				return;
			}

			EditorGUI.indentLevel++;
			foreach (var field in record.FieldNames)
			{
				var value = record[field];
				EditorGUI.BeginChangeCheck();
				var updated = DrawValue(field, value?.GetType() ?? typeof(string), value);
				if (EditorGUI.EndChangeCheck())
				{
					record[field] = updated!;
				}
			}

			EditorGUI.indentLevel--;
		}

		private void DrawList(string label, Type declared, IList list)
		{
			var element = ElementType(declared) ?? typeof(object);
			if (!Foldout($"list:{label}", $"{label}  ({element.Name}[{list.Count}])"))
			{
				return;
			}

			EditorGUI.indentLevel++;
			for (var index = 0; index < list.Count; index++)
			{
				var item = list[index];
				EditorGUI.BeginChangeCheck();
				var updated = DrawValue($"[{index}]", item?.GetType() ?? element, item);
				if (EditorGUI.EndChangeCheck())
				{
					list[index] = updated;
				}
			}

			EditorGUI.indentLevel--;
		}

		private bool Foldout(string key, string label)
		{
			_foldouts.TryGetValue(key, out var open);
			open = EditorGUILayout.Foldout(open, label, true);
			_foldouts[key] = open;
			return open;
		}

		// The declared T of an IValueProvider<T>, taken from its interface so it is known even when the current
		// value is null. Variables always implement the closed generic, so the lookup never fails in practice.
		private static Type DeclaredType(IValueProvider provider) =>
			provider.GetType().GetInterfaces()
				.Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValueProvider<>))
				.Select(i => i.GetGenericArguments()[0])
				.FirstOrDefault() ?? typeof(object);

		private static Type? ElementType(Type type) =>
			type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)
				? type.GetGenericArguments()[0]
				: null;

		// Writes back through IWriteValueProvider<T>.Set without a static T. Computed/read-only providers do not
		// implement the writable interface, so they are simply left untouched.
		private static void TrySet(IValueProvider provider, Type type, object? value)
		{
			var writable = typeof(IWriteValueProvider<>).MakeGenericType(type);
			if (writable.IsInstanceOfType(provider))
			{
				writable.GetMethod(nameof(IWriteValueProvider<object>.Set))!.Invoke(provider, new[] { value });
			}
		}
	}
}
