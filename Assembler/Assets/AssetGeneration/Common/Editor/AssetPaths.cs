#nullable enable

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Assembler.AssetGeneration.EditorCommon
{
	/// <summary>
	/// Project-path helpers the generation windows all reinvented: is a path inside the project's
	/// <c>Assets/</c> folder, its project-relative <c>Assets/…</c> form, and refreshing the
	/// AssetDatabase for a freshly-written file so Unity imports it.
	/// </summary>
	public static class AssetPaths
	{
		/// <summary>True when <paramref name="path"/> resolves to somewhere under the project's <c>Assets/</c>.</summary>
		public static bool IsUnderAssets(string path)
		{
			var full = Path.GetFullPath(path);
			var assets = Path.GetFullPath(Application.dataPath);
			return full.StartsWith(assets, StringComparison.OrdinalIgnoreCase);
		}

		/// <summary>
		/// The project-relative <c>Assets/…</c> path for <paramref name="path"/>, or <c>null</c> when it
		/// lives outside the project (so callers can decide whether an AssetDatabase call makes sense).
		/// </summary>
		public static string? ToAssetRelative(string path)
		{
			var full = Path.GetFullPath(path).Replace('\\', '/');
			var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/') + "/";
			if (!full.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
			{
				return null;
			}

			var relative = full.Substring(projectRoot.Length);
			return relative.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ? relative : null;
		}

		/// <summary>Import a just-written file into the project when it landed inside <c>Assets/</c>.</summary>
		public static void RefreshIfInside(string path)
		{
			if (IsUnderAssets(path))
			{
				AssetDatabase.Refresh();
			}
		}
	}
}
