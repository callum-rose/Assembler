using System;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Assembler.Shell.Editor
{
	/// <summary>
	/// Configures <c>UIAtlas.png</c> and slices it from <c>UIAtlas.slices.json</c> — the same table the sheet is
	/// drawn from, in <c>Prototypes/ui-atlas/</c>.
	/// </summary>
	/// <remarks>
	/// The sheet is white on transparent and authored at four times its canvas-unit size, which is why it imports
	/// at <see cref="TextureImporter.spritePixelsPerUnit"/> 4: a 44-unit hit target ships as 176 px and Set Native
	/// Size then lands on the unit sizes the prototype uses. Nothing in the atlas carries a colour — every graphic
	/// takes one from a <c>ThemeColor</c> role binder — so a second theme asset re-skins the shell without a
	/// second sheet.
	/// <para>
	/// Rects in the JSON are top-left origin, as the Sprite Editor shows them; Unity's are bottom-left, so
	/// <see cref="ToTextureSpace"/> flips them. Borders are already in Unity's (left, bottom, right, top) order.
	/// </para>
	/// </remarks>
	public static class ShellAtlasImporter
	{
		public const string AtlasFolder = "Assets/Shell/Art";
		public const string AtlasPath = AtlasFolder + "/UIAtlas.png";
		public const string SliceTablePath = AtlasFolder + "/UIAtlas.slices.json";

		/// <summary>
		/// Applies the import settings and the slice table, replacing whatever slicing the texture carried.
		/// Re-running is the point: re-generate the sheet, re-run this, and the rects follow.
		/// </summary>
		[MenuItem("Assembler/Shell/Import UI Atlas")]
		public static void ImportAtlas()
		{
			var table = LoadSliceTable();
			var importer = ConfigureTexture(table);

			ApplySlices(importer, table);

			Debug.Log(
				$"{nameof(ShellAtlasImporter)}: sliced {table.sprites.Length} sprites from {AtlasPath} at " +
				$"{table.pixelsPerUnit} pixels per unit " +
				$"({table.sprites.Count(sprite => sprite.IsSliced)} nine-sliced).");
		}

		private static SliceTable LoadSliceTable()
		{
			var json = AssetDatabase.LoadAssetAtPath<TextAsset>(SliceTablePath);

			if (json == null)
			{
				throw new InvalidOperationException(
					$"{nameof(ShellAtlasImporter)}: no slice table at {SliceTablePath}. It is generated beside " +
					"the sheet by Prototypes/ui-atlas/build-atlas.mjs.");
			}

			var table = JsonUtility.FromJson<SliceTable>(json.text);

			if (table is null || table.sprites is null || table.sprites.Length == 0)
			{
				throw new InvalidOperationException($"{nameof(ShellAtlasImporter)}: {SliceTablePath} lists no sprites.");
			}

			var duplicate = table.sprites
				.GroupBy(sprite => sprite.name, StringComparer.OrdinalIgnoreCase)
				.FirstOrDefault(group => group.Count() > 1);

			if (duplicate is not null)
			{
				throw new InvalidOperationException(
					$"{nameof(ShellAtlasImporter)}: '{duplicate.Key}' is listed more than once — sprite names are " +
					"the handles prefabs serialise, so they have to be unique.");
			}

			return table;
		}

		// Mesh Type is the one setting that is not cosmetic: a Tight mesh trims the transparent middle out of a
		// nine-sliced sprite, and the slice tears.
		private static TextureImporter ConfigureTexture(SliceTable table)
		{
			var importer = AssetImporter.GetAtPath(AtlasPath) as TextureImporter;

			if (importer == null)
			{
				throw new InvalidOperationException($"{nameof(ShellAtlasImporter)}: no texture at {AtlasPath}.");
			}

			var settings = new TextureImporterSettings();
			importer.ReadTextureSettings(settings);

			settings.textureType = TextureImporterType.Sprite;
			settings.spriteMode = (int)SpriteImportMode.Multiple;
			settings.spriteMeshType = SpriteMeshType.FullRect;
			settings.spritePixelsPerUnit = table.pixelsPerUnit;
			settings.spriteGenerateFallbackPhysicsShape = false;
			settings.alphaIsTransparency = true;
			settings.alphaSource = TextureImporterAlphaSource.FromInput;
			settings.sRGBTexture = true;
			settings.mipmapEnabled = false;
			// ReadTextureSettings hands back the default ToNearest, which a sprite cannot use; leaving it warns
			// on every import.
			settings.npotScale = TextureImporterNPOTScale.None;
			settings.readable = false;
			settings.filterMode = FilterMode.Bilinear;
			settings.wrapMode = TextureWrapMode.Clamp;

			importer.SetTextureSettings(settings);
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.SaveAndReimport();

			return (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
		}

		private static void ApplySlices(TextureImporter importer, SliceTable table)
		{
			var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);

			if (texture == null)
			{
				throw new InvalidOperationException($"{nameof(ShellAtlasImporter)}: {AtlasPath} did not load.");
			}

			var factories = new SpriteDataProviderFactories();
			factories.Init();

			var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
			provider.InitSpriteEditorDataProvider();

			// A sprite's GUID is what a prefab's serialised reference points at, so an existing sprite keeps the
			// one it already has — re-running the import must not detach every Image in the project.
			var existing = provider.GetSpriteRects()
				.ToDictionary(rect => rect.name, rect => rect.spriteID, StringComparer.Ordinal);

			var rects = table.sprites
				.Select(sprite => new SpriteRect
				{
					name = sprite.name,
					spriteID = existing.TryGetValue(sprite.name, out var id) ? id : GUID.Generate(),
					rect = ToTextureSpace(sprite, texture.width, texture.height),
					border = sprite.BorderVector,
					alignment = SpriteAlignment.Center,
					pivot = new Vector2(0.5f, 0.5f)
				})
				.ToArray();

			provider.SetSpriteRects(rects);

			// Without the name-to-file-id table Unity mints new file ids on the next import and every reference
			// into the sheet goes null.
			var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();

			if (names is not null)
			{
				names.SetNameFileIdPairs(
					rects.Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)).ToArray());
			}

			provider.Apply();

			AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
		}

		private static Rect ToTextureSpace(SpriteSpec sprite, int textureWidth, int textureHeight)
		{
			var rect = new Rect(
				sprite.rect.x,
				textureHeight - (sprite.rect.y + sprite.rect.h),
				sprite.rect.w,
				sprite.rect.h);

			if (rect.xMin < 0f || rect.yMin < 0f || rect.xMax > textureWidth || rect.yMax > textureHeight)
			{
				throw new InvalidOperationException(
					$"{nameof(ShellAtlasImporter)}: '{sprite.name}' at {sprite.rect.x},{sprite.rect.y} " +
					$"({sprite.rect.w}x{sprite.rect.h}) falls outside the sheet.");
			}

			return rect;
		}

		[Serializable]
		private sealed class SliceTable
		{
			public string sheet = string.Empty;
			public float pixelsPerUnit = 4f;
			public SpriteSpec[] sprites = Array.Empty<SpriteSpec>();
		}

		[Serializable]
		private sealed class SpriteSpec
		{
			public string name = string.Empty;
			public RectSpec rect = new();
			public int[] border = Array.Empty<int>();
			public string mode = "Simple";

			public bool IsSliced => string.Equals(mode, "Sliced", StringComparison.Ordinal);

			/// <summary>The border, already in Unity's (left, bottom, right, top) order.</summary>
			public Vector4 BorderVector => border.Length == 4
				? new Vector4(border[0], border[1], border[2], border[3])
				: Vector4.zero;
		}

		[Serializable]
		private sealed class RectSpec
		{
			public int x;
			public int y;
			public int w;
			public int h;
		}
	}
}
