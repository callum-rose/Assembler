using System;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Assembler.Shell.Editor
{
	/// <summary>
	/// Bakes the shell's Newsreader TextMeshPro font asset: one static SDF atlas, the whole
	/// <see cref="ShellCharacterSet"/>, kerning included. Static rather than dynamic so the atlas is fixed at
	/// build time — no runtime rasterisation hitch the first time a glyph appears, and no source TTF in the
	/// player.
	/// </summary>
	/// <remarks>
	/// One cut, not two. UIPLAN 5.4 asks for Newsreader's display and text optical cuts, but the repo carries
	/// only the variable font, and this TextMeshPro version can't instance a variable font's axes — it bakes the
	/// default instance. Importing Newsreader's static <c>Display</c> and <c>Text</c> TTFs and baking one asset
	/// from each is the follow-up; nothing else has to change, because styles name a font asset per entry.
	/// </remarks>
	public static class NewsreaderFontAssetBuilder
	{
		/// <summary>Where the baked asset lands.</summary>
		public const string FontAssetPath = "Assets/Fonts/Newsreader SDF.asset";

		private const string SourceFontPath = "Assets/Fonts/Newsreader-VariableFont_opsz,wght.ttf";

		// Sized down from TextMeshPro's 90/9 default: the whole character set at 90pt needs a 2048² atlas, which
		// serialises to an 11 MB text asset — more than twice the largest file in the repo. 64pt into 1024² holds
		// the same 218 glyphs at a quarter of the weight, and the largest type the shell sets (the 30-unit
		// headline, ~90px on a 3x screen) is only a 1.4x upscale, which an SDF carries without softening.
		private const int SamplingPointSize = 64;
		private const int AtlasPadding = 6;
		private const int AtlasSize = 1024;

		/// <summary>
		/// Re-bakes the asset from scratch. The file is replaced rather than edited in place, so it comes back
		/// with a new GUID — every reference to the old one would dangle, which is why the theme's styles are
		/// re-pointed at the new asset immediately afterwards.
		/// </summary>
		[MenuItem("Assembler/Shell/Bake Newsreader Font Asset")]
		public static void Bake()
		{
			var fontAsset = BakeInternal(overwrite: true);

			if (fontAsset == null)
			{
				return;
			}

			ShellAssetBuilder.RepointFonts(fontAsset);
			Selection.activeObject = fontAsset;
		}

		/// <summary>
		/// Bakes the font asset if it isn't there yet, and returns it either way.
		/// </summary>
		public static TMP_FontAsset? EnsureBaked()
		{
			var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

			return existing != null ? existing : BakeInternal(overwrite: false);
		}

		private static TMP_FontAsset? BakeInternal(bool overwrite)
		{
			var source = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);

			if (source == null)
			{
				Debug.LogError($"{nameof(NewsreaderFontAssetBuilder)}: no source font at '{SourceFontPath}'.");
				return null;
			}

			if (!overwrite && AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath) != null)
			{
				return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
			}

			// Baked dynamic, then frozen: TryAddCharacters refuses to run against a static atlas, so the atlas is
			// populated first and the mode flipped afterwards — which is also what the Font Asset Creator window
			// does. Flipping to Static drops the source-font reference, so the TTF stays out of the player.
			var fontAsset = TMP_FontAsset.CreateFontAsset(
				source,
				SamplingPointSize,
				AtlasPadding,
				GlyphRenderMode.SDFAA,
				AtlasSize,
				AtlasSize,
				AtlasPopulationMode.Dynamic,
				enableMultiAtlasSupport: false);

			if (fontAsset == null)
			{
				Debug.LogError(
					$"{nameof(NewsreaderFontAssetBuilder)}: TextMeshPro could not load a font face from " +
					$"'{SourceFontPath}'. Check that 'Include Font Data' is enabled on its importer.");
				return null;
			}

			fontAsset.name = "Newsreader SDF";

			if (!fontAsset.TryAddCharacters(ShellCharacterSet.Build(), out string missing, includeFontFeatures: true))
			{
				Debug.LogWarning(
					$"{nameof(NewsreaderFontAssetBuilder)}: {missing.Length} characters could not be baked — " +
					$"either Newsreader has no glyph for them or the {AtlasSize}² atlas is full: '{missing}'");
			}

			fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

			AssetDatabase.DeleteAsset(FontAssetPath);
			AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

			// The atlas texture and the material are sub-assets of the font asset — they must be added after the
			// font asset itself exists on disk, or they are written as loose objects and lost on reimport.
			var atlas = fontAsset.atlasTextures[0];
			atlas.name = fontAsset.name + " Atlas";
			AssetDatabase.AddObjectToAsset(atlas, fontAsset);

			var material = fontAsset.material;
			material.name = fontAsset.name + " Material";
			AssetDatabase.AddObjectToAsset(material, fontAsset);

			EditorUtility.SetDirty(fontAsset);
			AssetDatabase.SaveAssets();

			int glyphCount = fontAsset.characterTable?.Count ?? 0;
			Debug.Log(
				$"{nameof(NewsreaderFontAssetBuilder)}: baked '{fontAsset.name}' at {FontAssetPath} — " +
				$"{glyphCount} characters, static {AtlasSize}² SDF atlas.");

			return fontAsset;
		}
	}
}
