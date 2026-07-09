#nullable enable

using System;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.EditorCommon
{
	/// <summary>
	/// The password field + "Save" button row every generation window draws for its API key.
	/// The field edits in place; the button hands the current value to <paramref name="onSave"/>
	/// (typically <see cref="ApiKeyStore.Save"/>).
	/// </summary>
	public static class ApiKeyField
	{
		/// <summary>Draw the row and return the (possibly edited) key.</summary>
		public static string Draw(string label, string key, Action<string> onSave)
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				key = EditorGUILayout.PasswordField(label, key);
				if (GUILayout.Button("Save", GUILayout.Width(60)))
				{
					onSave(key);
				}
			}

			return key;
		}
	}
}
