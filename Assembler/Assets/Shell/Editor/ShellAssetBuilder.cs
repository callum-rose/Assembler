using System;
using System.Collections.Generic;
using Assembler.Shell.Theming;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Assembler.Shell.Editor
{
	/// <summary>
	/// Authors the shell's two data assets — the <see cref="ShellTheme"/> carrying the Letterpress palette and
	/// typographic scale, and the <see cref="ShellConfig"/> carrying the editorial numbers.
	/// </summary>
	/// <remarks>
	/// The palette and the scale are transcribed from <c>Prototypes/app-look-prototype.html</c> (variant D). The
	/// canvas is 390 units across the short axis, which is exactly the prototype's viewport in CSS pixels, so
	/// sizes and gutters transfer 1:1. Tracking converts as em × 100; leading as (line-height − 1) × 100, since
	/// Newsreader's own line height is exactly 1em.
	/// </remarks>
	public static class ShellAssetBuilder
	{
		public const string ShellResourcesFolder = "Assets/Resources/Shell";
		public const string ThemeAssetPath = ShellResourcesFolder + "/ShellTheme.asset";
		public const string ConfigAssetPath = ShellResourcesFolder + "/ShellConfig.asset";

		private static readonly IReadOnlyList<(ColorRole Role, string Hex)> Palette = new[]
		{
			(ColorRole.Paper, "#faf6ee"),
			(ColorRole.Surface, "#fffdf8"),
			(ColorRole.Sunk, "#efe9dd"),
			(ColorRole.Ink, "#17130d"),
			(ColorRole.InkSecondary, "#4f483d"),
			(ColorRole.InkTertiary, "#948b7c"),
			(ColorRole.Rule, "#d8cfbe"),
			(ColorRole.RuleHard, "#17130d"),
			(ColorRole.Accent, "#b8121b"),
			(ColorRole.AccentSecondary, "#8a7248"),
			(ColorRole.OnAccent, "#fffdf8"),
			(ColorRole.Good, "#1d7a45"),
			(ColorRole.Bad, "#b8121b"),
			(ColorRole.Staging, "#9a6212"),
			(ColorRole.ArtBackground, "#e7e0d2"),
			(ColorRole.ButtonFace, "#17130d"),
			(ColorRole.ButtonInk, "#faf6ee"),
			(ColorRole.Offset, "#17130d")
		};

		private static readonly IReadOnlyList<StyleSpec> Scale = new[]
		{
			new StyleSpec { Id = TextStyleId.Masthead, Size = 27f, Bold = true, Tracking = -3f },
			new StyleSpec { Id = TextStyleId.Folio, Size = 10f, Case = TextCase.UpperCase, Tracking = 13f, Color = ColorRole.InkSecondary },
			new StyleSpec { Id = TextStyleId.ScreenTitle, Size = 12f, Bold = true, Case = TextCase.UpperCase, Tracking = 16f, Color = ColorRole.InkTertiary },
			new StyleSpec { Id = TextStyleId.BackLabel, Size = 15f, Bold = true },
			new StyleSpec { Id = TextStyleId.Kicker, Size = 10.5f, Bold = true, Case = TextCase.UpperCase, Tracking = 20f, Color = ColorRole.Accent },
			new StyleSpec { Id = TextStyleId.Headline, Size = 30f, Bold = true, Tracking = -2.5f, Leading = 8f },
			new StyleSpec { Id = TextStyleId.HeadlineMeta, Size = 10.5f, Case = TextCase.UpperCase, Tracking = 10f, Color = ColorRole.InkTertiary },
			new StyleSpec { Id = TextStyleId.Body, Size = 15f, Leading = 52f, Color = ColorRole.InkSecondary },
			new StyleSpec { Id = TextStyleId.DropCap, Size = 15f, Bold = true, Color = ColorRole.Accent },
			new StyleSpec { Id = TextStyleId.SectionHeader, Size = 10.5f, Bold = true, Case = TextCase.UpperCase, Tracking = 20f },
			new StyleSpec { Id = TextStyleId.CardTitle, Size = 15.5f, Bold = true, Tracking = -1.2f, Leading = 18f },
			new StyleSpec { Id = TextStyleId.CardBody, Size = 12.5f, Leading = 42f, Color = ColorRole.InkSecondary },
			new StyleSpec { Id = TextStyleId.CardMeta, Size = 9.5f, Case = TextCase.UpperCase, Tracking = 11f, Color = ColorRole.InkTertiary },
			new StyleSpec { Id = TextStyleId.RowTitle, Size = 14.5f, Bold = true, Leading = 22f },
			new StyleSpec { Id = TextStyleId.ButtonLabel, Size = 13f, Bold = true, Case = TextCase.UpperCase, Tracking = 14f, Color = ColorRole.ButtonInk },
			new StyleSpec { Id = TextStyleId.StatValue, Size = 19f, Bold = true },
			new StyleSpec { Id = TextStyleId.StatLabel, Size = 9.5f, Case = TextCase.UpperCase, Tracking = 10f, Color = ColorRole.InkTertiary },
			new StyleSpec { Id = TextStyleId.FieldText, Size = 15f },
			// The prototype strikes the stamp in a monospace face. The mono cut is in-game chrome's, and lands
			// with the game strip; until then the stamp sets in the shell's serif.
			new StyleSpec { Id = TextStyleId.Stamp, Size = 11f, Bold = true, Case = TextCase.UpperCase, Tracking = 18f, Color = ColorRole.Accent }
		};

		/// <summary>
		/// Creates whichever shell assets are missing, leaving any that already exist alone — re-running never
		/// discards hand-tuning. Use <see cref="ResetTheme"/> to force the theme back to these values.
		/// </summary>
		[MenuItem("Assembler/Shell/Create Shell Assets")]
		public static void CreateShellAssets()
		{
			EnsureFolders();

			var font = NewsreaderFontAssetBuilder.EnsureBaked();
			var theme = EnsureTheme(font);
			var config = EnsureConfig();

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log(
				$"{nameof(ShellAssetBuilder)}: theme '{theme.name}' and config '{config.name}' ready under " +
				$"{ShellResourcesFolder}.");
		}

		/// <summary>Rewrites the existing theme's palette and typographic scale from the prototype.</summary>
		[MenuItem("Assembler/Shell/Reset Shell Theme")]
		public static void ResetTheme()
		{
			var theme = AssetDatabase.LoadAssetAtPath<ShellTheme>(ThemeAssetPath);

			if (theme == null)
			{
				CreateShellAssets();
				return;
			}

			Populate(theme, NewsreaderFontAssetBuilder.EnsureBaked());
			AssetDatabase.SaveAssets();

			Debug.Log($"{nameof(ShellAssetBuilder)}: reset '{theme.name}' to the Letterpress palette and scale.");
		}

		/// <summary>Loads the theme asset, creating it first if it isn't there.</summary>
		public static ShellTheme EnsureTheme(TMP_FontAsset? font)
		{
			var existing = AssetDatabase.LoadAssetAtPath<ShellTheme>(ThemeAssetPath);

			if (existing != null)
			{
				return existing;
			}

			EnsureFolders();

			var theme = ScriptableObject.CreateInstance<ShellTheme>();
			AssetDatabase.CreateAsset(theme, ThemeAssetPath);
			Populate(theme, font);

			return theme;
		}

		/// <summary>
		/// Points every style in the existing theme at <paramref name="font"/>, leaving sizes, tracking and
		/// colour roles alone. Re-baking a font asset mints a new GUID, and this is what stops the theme's
		/// references dangling afterwards.
		/// </summary>
		public static void RepointFonts(TMP_FontAsset font)
		{
			var theme = AssetDatabase.LoadAssetAtPath<ShellTheme>(ThemeAssetPath);

			if (theme == null)
			{
				return;
			}

			var serialized = new SerializedObject(theme);
			var styles = serialized.FindProperty("textStyles");

			for (int i = 0; i < styles.arraySize; i++)
			{
				styles.GetArrayElementAtIndex(i).FindPropertyRelative("font").objectReferenceValue = font;
			}

			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(theme);
			AssetDatabase.SaveAssets();
		}

		/// <summary>Loads the config asset, creating it first if it isn't there.</summary>
		public static ShellConfig EnsureConfig()
		{
			var existing = AssetDatabase.LoadAssetAtPath<ShellConfig>(ConfigAssetPath);

			if (existing != null)
			{
				return existing;
			}

			EnsureFolders();

			var config = ScriptableObject.CreateInstance<ShellConfig>();
			AssetDatabase.CreateAsset(config, ConfigAssetPath);

			return config;
		}

		// The motion and layout blocks come from the ShellTheme's own field initialisers, so only the palette
		// and the scale are written here.
		private static void Populate(ShellTheme theme, TMP_FontAsset? font)
		{
			var serialized = new SerializedObject(theme);

			WriteColors(serialized.FindProperty("colors"));
			WriteStyles(serialized.FindProperty("textStyles"), font);

			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(theme);
		}

		private static void WriteColors(SerializedProperty colors)
		{
			colors.arraySize = Palette.Count;

			for (int i = 0; i < Palette.Count; i++)
			{
				var (role, hex) = Palette[i];

				if (!ColorUtility.TryParseHtmlString(hex, out var color))
				{
					throw new InvalidOperationException($"{nameof(ShellAssetBuilder)}: '{hex}' is not a colour.");
				}

				var entry = colors.GetArrayElementAtIndex(i);
				entry.FindPropertyRelative("role").intValue = (int)role;
				entry.FindPropertyRelative("color").colorValue = color;
			}
		}

		private static void WriteStyles(SerializedProperty styles, TMP_FontAsset? font)
		{
			if (font == null)
			{
				Debug.LogWarning(
					$"{nameof(ShellAssetBuilder)}: writing the typographic scale with no font asset — every style " +
					"will fall back to TextMeshPro's default face.");
			}

			styles.arraySize = Scale.Count;

			for (int i = 0; i < Scale.Count; i++)
			{
				var spec = Scale[i];
				var entry = styles.GetArrayElementAtIndex(i);

				entry.FindPropertyRelative("id").intValue = (int)spec.Id;
				entry.FindPropertyRelative("font").objectReferenceValue = font;
				entry.FindPropertyRelative("fontSize").floatValue = spec.Size;
				entry.FindPropertyRelative("bold").boolValue = spec.Bold;
				entry.FindPropertyRelative("italic").boolValue = false;
				entry.FindPropertyRelative("textCase").intValue = (int)spec.Case;
				entry.FindPropertyRelative("characterSpacing").floatValue = spec.Tracking;
				entry.FindPropertyRelative("lineSpacing").floatValue = spec.Leading;
				entry.FindPropertyRelative("color").intValue = (int)spec.Color;
			}
		}

		private static void EnsureFolders()
		{
			if (!AssetDatabase.IsValidFolder("Assets/Resources"))
			{
				AssetDatabase.CreateFolder("Assets", "Resources");
			}

			if (!AssetDatabase.IsValidFolder(ShellResourcesFolder))
			{
				AssetDatabase.CreateFolder("Assets/Resources", "Shell");
			}
		}

		private sealed class StyleSpec
		{
			public TextStyleId Id;
			public float Size;
			public bool Bold;
			public TextCase Case = TextCase.AsTyped;
			public float Tracking;
			public float Leading;
			public ColorRole Color = ColorRole.Ink;
		}
	}
}
