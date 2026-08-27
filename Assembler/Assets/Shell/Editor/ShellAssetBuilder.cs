using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Shell.Theming;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Assembler.Shell.Editor
{
	/// <summary>
	/// Authors the shell's data assets — the <see cref="ColorRole"/> and <see cref="TextStyleId"/> members, the
	/// <see cref="ShellTheme"/> carrying the Letterpress palette and typographic scale, and the
	/// <see cref="ShellConfig"/> carrying the editorial numbers.
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

		/// <summary>Where the <see cref="ColorRole"/> members live — one asset per role.</summary>
		public const string RolesFolder = "Assets/Shell/Theming/Roles";

		/// <summary>Where the <see cref="TextStyleId"/> members live — one asset per style.</summary>
		public const string TextStylesFolder = "Assets/Shell/Theming/TextStyles";

		// Role and style members are named here rather than referenced as enum values: they are assets now, so
		// the name is what identifies a row of this table with the asset it authors.
		private static readonly IReadOnlyList<(string Name, string Hex, string Description)> Palette = new[]
		{
			("Paper", "#faf6ee", "The page ground the whole shell sits on."),
			("Surface", "#fffdf8", "Raised surfaces: sheets, cards, inputs."),
			("Sunk", "#efe9dd", "Inset surfaces: the search field, segmented controls."),
			("Ink", "#17130d", "Primary text."),
			("InkSecondary", "#4f483d", "Body text — a step down from Ink."),
			("InkTertiary", "#948b7c", "Metadata and captions — the quietest text."),
			("Rule", "#d8cfbe", "Hairline rules between cells and rows."),
			("RuleHard", "#17130d", "The heavy rules: under the masthead, under a section header."),
			("Accent", "#b8121b", "The masthead red."),
			("AccentSecondary", "#8a7248", "The demoted second accent."),
			("OnAccent", "#fffdf8", "Text and glyphs drawn on top of Accent."),
			("Good", "#1d7a45", "Positive verdicts."),
			("Bad", "#b8121b", "Negative verdicts and failures."),
			("Staging", "#9a6212", "Staging-channel entries, visible only in dev mode."),
			("ArtBackground", "#e7e0d2", "The ground a piece of game art sits on before it loads."),
			("ButtonFace", "#17130d", "The plate of a letterpress button."),
			("ButtonInk", "#faf6ee", "Text on a letterpress button."),
			("Offset", "#17130d", "The hard ledge a letterpress element casts — the depth that a press consumes."),
			("Scrim", "#040508b8", "The dark ground an overlay lays over everything beneath it. Carries its own alpha.")
		};

		private static readonly IReadOnlyList<StyleSpec> Scale = new[]
		{
			new StyleSpec { Name = "Masthead", Size = 27f, Bold = true, Tracking = -3f, Description = "The paper's name, top-left of the masthead." },
			new StyleSpec { Name = "Folio", Size = 10f, Case = TextCase.UpperCase, Tracking = 13f, Color = "InkSecondary", Description = "The folio strip under the masthead: edition number, date, count." },
			new StyleSpec { Name = "ScreenTitle", Size = 12f, Bold = true, Case = TextCase.UpperCase, Tracking = 16f, Color = "InkTertiary", Description = "The small capitalised title of a pushed screen." },
			new StyleSpec { Name = "BackLabel", Size = 15f, Bold = true, Description = "The back-button label, which names the screen beneath the top of the stack." },
			new StyleSpec { Name = "Kicker", Size = 10.5f, Bold = true, Case = TextCase.UpperCase, Tracking = 20f, Color = "Accent", Description = "The accent-red rubric above a headline." },
			new StyleSpec { Name = "Headline", Size = 30f, Bold = true, Tracking = -2.5f, Leading = 8f, Description = "A lead or detail headline." },
			new StyleSpec { Name = "HeadlineMeta", Size = 10.5f, Case = TextCase.UpperCase, Tracking = 10f, Color = "InkTertiary", Description = "The byline/date line under a headline." },
			new StyleSpec { Name = "Body", Size = 15f, Leading = 52f, Color = "InkSecondary", Description = "Running body copy." },
			new StyleSpec { Name = "DropCap", Size = 15f, Bold = true, Color = "Accent", Description = "The drop cap that opens the lead story." },
			new StyleSpec { Name = "SectionHeader", Size = 10.5f, Bold = true, Case = TextCase.UpperCase, Tracking = 20f, Description = "A section header ('MORE EDITIONS')." },
			new StyleSpec { Name = "CardTitle", Size = 15.5f, Bold = true, Tracking = -1.2f, Leading = 18f, Description = "A feed card's headline." },
			new StyleSpec { Name = "CardBody", Size = 12.5f, Leading = 42f, Color = "InkSecondary", Description = "A feed card's standfirst." },
			new StyleSpec { Name = "CardMeta", Size = 9.5f, Case = TextCase.UpperCase, Tracking = 11f, Color = "InkTertiary", Description = "A feed card's meta line." },
			new StyleSpec { Name = "RowTitle", Size = 14.5f, Bold = true, Leading = 22f, Description = "An archive row's headline." },
			new StyleSpec { Name = "ButtonLabel", Size = 13f, Bold = true, Case = TextCase.UpperCase, Tracking = 14f, Color = "ButtonInk", Description = "The label on a letterpress button." },
			new StyleSpec { Name = "QuietButtonLabel", Size = 12f, Bold = true, Case = TextCase.UpperCase, Tracking = 14f, Description = "The label on an outlined button, which shows paper rather than ink behind it." },
			new StyleSpec { Name = "IconGlyph", Size = 17f, Description = "The glyph on an icon button." },
			new StyleSpec { Name = "StatValue", Size = 19f, Bold = true, Description = "A stat band figure." },
			new StyleSpec { Name = "StatLabel", Size = 9.5f, Case = TextCase.UpperCase, Tracking = 10f, Color = "InkTertiary", Description = "The caption under a stat band figure." },
			new StyleSpec { Name = "FieldText", Size = 15f, Description = "Editable or selectable field text: search, settings rows." },
			// The prototype strikes the stamp in a monospace face. The mono cut is in-game chrome's, and lands
			// with the game strip; until then the stamp sets in the shell's serif.
			new StyleSpec { Name = "Stamp", Size = 11f, Bold = true, Case = TextCase.UpperCase, Tracking = 18f, Color = "Accent", Description = "The PLAY stamp struck across unplayed lead art." }
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
			TopUp(theme, font);
			var config = EnsureConfig();

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();

			Debug.Log(
				$"{nameof(ShellAssetBuilder)}: theme '{theme.name}' and config '{config.name}' ready under " +
				$"{ShellResourcesFolder}, with {Palette.Count} roles and {Scale.Count} styles under " +
				$"{RolesFolder} and {TextStylesFolder}.");
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

		/// <summary>
		/// Appends any role or style this table carries that the theme does not, leaving every row it already
		/// has exactly as authored.
		/// </summary>
		/// <remarks>
		/// Adding a member to the tables above would otherwise be a trap: the member asset would be created and
		/// prefabs could bind it, but the theme — which is only ever populated when it is first created — would
		/// have no row for it, and everything bound to it would paint magenta until somebody thought to run
		/// <see cref="ResetTheme"/> and discard their tuning along the way.
		/// </remarks>
		public static void TopUp(ShellTheme theme, TMP_FontAsset? font)
		{
			WarnIfFontMissing(font);

			var serialized = new SerializedObject(theme);
			var colors = serialized.FindProperty("colors");
			var styles = serialized.FindProperty("textStyles");

			int added = 0;

			foreach (var spec in Palette)
			{
				if (Bound(colors, "role", spec.Name))
				{
					continue;
				}

				colors.arraySize++;
				WriteColor(colors.GetArrayElementAtIndex(colors.arraySize - 1), spec);
				added++;
			}

			foreach (var spec in Scale)
			{
				if (Bound(styles, "id", spec.Name))
				{
					continue;
				}

				styles.arraySize++;
				WriteStyle(styles.GetArrayElementAtIndex(styles.arraySize - 1), spec, font);
				added++;
			}

			if (added == 0)
			{
				return;
			}

			serialized.ApplyModifiedPropertiesWithoutUndo();
			EditorUtility.SetDirty(theme);

			Debug.Log($"{nameof(ShellAssetBuilder)}: added {added} missing theme rows to '{theme.name}'.");
		}

		// Matched on the member asset's name rather than its reference, so a row bound to a member that was
		// deleted and re-created still counts as bound.
		private static bool Bound(SerializedProperty rows, string field, string memberName)
		{
			for (int i = 0; i < rows.arraySize; i++)
			{
				var member = rows.GetArrayElementAtIndex(i).FindPropertyRelative(field).objectReferenceValue;

				if (member != null && member.name == memberName)
				{
					return true;
				}
			}

			return false;
		}

		// The motion and layout blocks come from the ShellTheme's own field initialisers, so only the palette
		// and the scale are written here.
		private static void Populate(ShellTheme theme, TMP_FontAsset? font)
		{
			EnsureFolders();

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
				WriteColor(colors.GetArrayElementAtIndex(i), Palette[i]);
			}
		}

		private static void WriteStyles(SerializedProperty styles, TMP_FontAsset? font)
		{
			WarnIfFontMissing(font);

			styles.arraySize = Scale.Count;

			for (int i = 0; i < Scale.Count; i++)
			{
				WriteStyle(styles.GetArrayElementAtIndex(i), Scale[i], font);
			}
		}

		private static void WriteColor(
			SerializedProperty entry,
			(string Name, string Hex, string Description) spec)
		{
			if (!ColorUtility.TryParseHtmlString(spec.Hex, out var color))
			{
				throw new InvalidOperationException($"{nameof(ShellAssetBuilder)}: '{spec.Hex}' is not a colour.");
			}

			entry.FindPropertyRelative("role").objectReferenceValue =
				EnsureMember<ColorRole>(RolesFolder, spec.Name, spec.Description);
			entry.FindPropertyRelative("color").colorValue = color;
		}

		private static void WriteStyle(SerializedProperty entry, StyleSpec spec, TMP_FontAsset? font)
		{
			entry.FindPropertyRelative("id").objectReferenceValue =
				EnsureMember<TextStyleId>(TextStylesFolder, spec.Name, spec.Description);
			entry.FindPropertyRelative("font").objectReferenceValue = font;
			entry.FindPropertyRelative("fontSize").floatValue = spec.Size;
			entry.FindPropertyRelative("bold").boolValue = spec.Bold;
			entry.FindPropertyRelative("italic").boolValue = false;
			entry.FindPropertyRelative("textCase").intValue = (int)spec.Case;
			entry.FindPropertyRelative("characterSpacing").floatValue = spec.Tracking;
			entry.FindPropertyRelative("lineSpacing").floatValue = spec.Leading;
			entry.FindPropertyRelative("color").objectReferenceValue = Role(spec.Color);
		}

		private static void WarnIfFontMissing(TMP_FontAsset? font)
		{
			if (font != null)
			{
				return;
			}

			Debug.LogWarning(
				$"{nameof(ShellAssetBuilder)}: writing the typographic scale with no font asset — every style " +
				"will fall back to TextMeshPro's default face.");
		}

		// A style's colour has to name a role the palette actually carries: a typo here would otherwise mint a
		// stray role asset that no theme row binds, and paint the label magenta at runtime.
		private static ColorRole Role(string roleName)
		{
			if (Palette.All(entry => entry.Name != roleName))
			{
				throw new InvalidOperationException(
					$"{nameof(ShellAssetBuilder)}: '{roleName}' is not a role in the palette.");
			}

			return EnsureMember<ColorRole>(RolesFolder, roleName);
		}

		// Creating the asset is the point, but so is rewriting its description: these tables are the source of
		// truth for what a member means, and a re-run should carry an edited blurb onto the asset.
		private static T EnsureMember<T>(string folder, string memberName, string? description = null)
			where T : ScriptableEnum
		{
			var path = $"{folder}/{memberName}.asset";
			var member = AssetDatabase.LoadAssetAtPath<T>(path);

			if (member == null)
			{
				member = ScriptableObject.CreateInstance<T>();
				AssetDatabase.CreateAsset(member, path);
			}

			if (description is not null)
			{
				var serialized = new SerializedObject(member);
				serialized.FindProperty("description").stringValue = description;
				serialized.ApplyModifiedPropertiesWithoutUndo();
				EditorUtility.SetDirty(member);
			}

			return member;
		}

		private static void EnsureFolders()
		{
			EnsureFolder("Assets/Resources");
			EnsureFolder(ShellResourcesFolder);
			EnsureFolder(RolesFolder);
			EnsureFolder(TextStylesFolder);
		}

		private static void EnsureFolder(string path)
		{
			if (AssetDatabase.IsValidFolder(path))
			{
				return;
			}

			int separator = path.LastIndexOf('/');
			AssetDatabase.CreateFolder(path.Substring(0, separator), path.Substring(separator + 1));
		}

		private sealed class StyleSpec
		{
			public string Name = string.Empty;
			public string Description = string.Empty;
			public float Size;
			public bool Bold;
			public TextCase Case = TextCase.AsTyped;
			public float Tracking;
			public float Leading;
			public string Color = "Ink";
		}
	}
}
