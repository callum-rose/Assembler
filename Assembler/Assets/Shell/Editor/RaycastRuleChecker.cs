using System.Collections.Generic;
using System.Linq;
using Assembler.Shell.Controls;
using Assembler.Shell.Theming;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Assembler.Shell.Editor
{
	/// <summary>
	/// Enforces UIPLAN 7.4 across the shell's prefabs: <em>nothing raycasts except things named HitTarget</em>,
	/// and no hit target is smaller than the theme's minimum.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The rule is worth a checker because breaking it is silent and the symptom is nothing like the cause. A
	/// decorative graphic left raycasting swallows the tap meant for the control beneath it; a label that
	/// raycasts makes the gap between two words dead; a graphic that both raycasts and animates cancels its own
	/// press by sliding out from under the pointer. None of that shows up as an error, and all of it shows up as
	/// "the button sometimes doesn't work".
	/// </para>
	/// <para>
	/// Prefabs only. The shell's screens and chrome all live in prefabs by rule (UIPLAN 7.1), and reading a
	/// scene means opening it, which is not something a check should do to whatever the editor is holding.
	/// </para>
	/// </remarks>
	public static class RaycastRuleChecker
	{
		private const string ShellFolder = "Assets/Shell";

		[MenuItem("Assembler/Shell/Check Raycast Rule")]
		public static void CheckRaycastRule()
		{
			var offences = Check();

			if (offences.Count == 0)
			{
				Debug.Log($"{nameof(RaycastRuleChecker)}: every shell prefab keeps the raycast rule.");
				return;
			}

			foreach (var offence in offences)
			{
				Debug.LogError($"{nameof(RaycastRuleChecker)}: {offence}");
			}

			Debug.LogError(
				$"{nameof(RaycastRuleChecker)}: {offences.Count} breach(es) of the raycast rule. Every graphic " +
				$"that is not a {nameof(HitTarget)} must set raycastTarget = false.");
		}

		/// <summary>Every breach of the rule in the shell's prefabs, as a line of text each.</summary>
		public static IReadOnlyList<string> Check()
		{
			float minimum = Theme.Current.Layout.MinHitTarget;

			return AssetDatabase
				.FindAssets("t:Prefab", new[] { ShellFolder })
				.Select(AssetDatabase.GUIDToAssetPath)
				.Distinct()
				.OrderBy(path => path)
				.SelectMany(path => CheckPrefab(path, minimum))
				.ToList();
		}

		private static IEnumerable<string> CheckPrefab(string path, float minimum)
		{
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

			if (prefab == null)
			{
				yield break;
			}

			foreach (var graphic in prefab.GetComponentsInChildren<Graphic>(includeInactive: true))
			{
				if (graphic is HitTarget target)
				{
					string? undersized = Undersized(target.rectTransform, minimum);

					if (undersized is not null)
					{
						yield return $"{path}: {Path(target.transform)} {undersized}";
					}

					continue;
				}

				if (graphic.raycastTarget)
				{
					yield return
						$"{path}: {Path(graphic.transform)} ({graphic.GetType().Name}) raycasts but is not a " +
						$"{nameof(HitTarget)}.";
				}
			}
		}

		// Only a target whose size is its own is worth measuring. A stretched one takes its size from whatever
		// it is parented to at runtime, and reads as zero in the prefab asset, where it has no parent yet.
		private static string? Undersized(RectTransform rect, float minimum)
		{
			bool fixedWidth = Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x);
			bool fixedHeight = Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y);

			if (fixedWidth && rect.rect.width + 0.01f < minimum)
			{
				return $"is {rect.rect.width} units wide, under the {minimum}-unit minimum.";
			}

			if (fixedHeight && rect.rect.height + 0.01f < minimum)
			{
				return $"is {rect.rect.height} units tall, under the {minimum}-unit minimum.";
			}

			return null;
		}

		private static string Path(Transform target)
		{
			string path = target.name;

			for (var parent = target.parent; parent != null; parent = parent.parent)
			{
				path = parent.name + "/" + path;
			}

			return path;
		}
	}
}
