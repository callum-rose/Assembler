using Assembler.Voxels;
using UnityEngine;

namespace Assembler.Voxelization
{
	/// <summary>
	/// Pure-code preview rendering for the review gallery — no camera, no
	/// importer. Front view is a flat orthographic colour projection; iso view
	/// is a classic 2:1 dimetric paint with three face shades, drawn far-to-near
	/// so nearer voxels overwrite.
	/// </summary>
	public static class VoxelPreviewRenderer
	{
		public static Texture2D RenderFront(VoxelModel model, int pixelsPerVoxel = 6) =>
			RenderProjection(model, ProjectionFace.Front, pixelsPerVoxel);

		public static Texture2D RenderProjection(VoxelModel model, ProjectionFace face, int pixelsPerVoxel = 6)
		{
			var colours = VoxelProjector.Colours(model, face);
			var width = colours.GetLength(0);
			var height = colours.GetLength(1);
			var texture = NewTexture(width * pixelsPerVoxel, height * pixelsPerVoxel);

			for (var u = 0; u < width; u++)
			{
				for (var v = 0; v < height; v++)
				{
					if (colours[u, v] is not { } colour)
					{
						continue;
					}

					FillRect(texture, u * pixelsPerVoxel, v * pixelsPerVoxel, pixelsPerVoxel, pixelsPerVoxel, colour);
				}
			}

			texture.Apply();
			return texture;
		}

		public static Texture2D RenderIso(VoxelModel model, int scale = 2)
		{
			var size = model.Size;
			var tile = 2 * scale;

			// Screen-space extents of the dimetric projection:
			// sx = (x - z) * tile/2, sy = y * tile/2 + (x + z) * tile/4 (+ tile for the cube top).
			var width = (size.x + size.z) * tile / 2 + tile;
			var height = size.y * tile / 2 + (size.x + size.z) * tile / 4 + tile;
			var texture = NewTexture(width, height);

			// Painter's order: low x+z first (far), then low y, so near and upper
			// voxels overwrite.
			var voxels = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<Vector3Int, byte>>(model.Voxels);
			voxels.Sort((a, b) =>
			{
				var da = a.Key.x + a.Key.z;
				var db = b.Key.x + b.Key.z;
				return da != db ? da.CompareTo(db) : a.Key.y.CompareTo(b.Key.y);
			});

			var originX = size.z * tile / 2;
			foreach (var kv in voxels)
			{
				var p = kv.Key - model.Min;
				var colour = model.Palette[kv.Value - 1];
				var sx = originX + (p.x - p.z) * tile / 2;
				var sy = p.y * tile / 2 + (p.x + p.z) * tile / 4;
				DrawIsoCube(texture, sx, sy, scale, colour);
			}

			texture.Apply();
			return texture;
		}

		private static void DrawIsoCube(Texture2D texture, int sx, int sy, int scale, Color32 colour)
		{
			var tile = 2 * scale;
			var left = Shade(colour, 0.78f);
			var right = Shade(colour, 0.6f);
			var top = Shade(colour, 1f);

			// Side faces: left half and right half of the tile column.
			FillRect(texture, sx, sy, scale, tile, left);
			FillRect(texture, sx + scale, sy, scale, tile, right);

			// Top face: a flat cap above the column.
			FillRect(texture, sx, sy + tile, tile, scale, top);
		}

		private static Texture2D NewTexture(int width, int height)
		{
			var texture = new Texture2D(Mathf.Max(1, width), Mathf.Max(1, height), TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Point,
			};
			var clear = new Color32(0, 0, 0, 0);
			var pixels = new Color32[texture.width * texture.height];
			for (var i = 0; i < pixels.Length; i++)
			{
				pixels[i] = clear;
			}

			texture.SetPixels32(pixels);
			return texture;
		}

		private static void FillRect(Texture2D texture, int x, int y, int width, int height, Color32 colour)
		{
			for (var dx = 0; dx < width; dx++)
			{
				for (var dy = 0; dy < height; dy++)
				{
					var px = x + dx;
					var py = y + dy;
					if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
					{
						texture.SetPixel(px, py, colour);
					}
				}
			}
		}

		private static Color32 Shade(Color32 colour, float factor) => new(
			(byte)Mathf.Clamp(Mathf.RoundToInt(colour.r * factor), 0, 255),
			(byte)Mathf.Clamp(Mathf.RoundToInt(colour.g * factor), 0, 255),
			(byte)Mathf.Clamp(Mathf.RoundToInt(colour.b * factor), 0, 255),
			colour.a);
	}
}
