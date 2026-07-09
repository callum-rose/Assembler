#nullable enable

using System.IO;
using UnityEngine;

namespace Assembler.AssetGeneration.EditorCommon
{
	/// <summary>
	/// Owns a single decoded preview <see cref="Texture2D"/> for a generation window: loading a new
	/// image destroys the previous one (the copy-pasted variants that forgot this leaked a texture
	/// per run), and <see cref="Draw"/> renders it aspect-fitted. The window destroys it in
	/// <c>OnDisable</c> via <see cref="Clear"/>.
	/// </summary>
	/// <remarks>
	/// Only for textures this owns (decoded from bytes/files). Do NOT feed it a texture loaded from
	/// the AssetDatabase — that is owned by Unity and must not be destroyed.
	/// </remarks>
	public sealed class TexturePreview
	{
		private Texture2D? _texture;

		public Texture2D? Texture => _texture;

		public bool HasTexture => _texture != null;

		/// <summary>Decode <paramref name="bytes"/> into the preview, destroying any previous texture.</summary>
		public void Load(byte[] bytes)
		{
			Clear();
			var tex = new Texture2D(2, 2);
			if (tex.LoadImage(bytes))
			{
				_texture = tex;
			}
			else
			{
				Object.DestroyImmediate(tex);
			}
		}

		/// <summary>
		/// Load an image file into the preview, destroying any previous texture. Returns false (and
		/// leaves the preview empty) when the file is missing or not a decodable image.
		/// </summary>
		public bool LoadFile(string path)
		{
			Clear();
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{
				return false;
			}

			var tex = new Texture2D(2, 2);
			if (tex.LoadImage(File.ReadAllBytes(path)))
			{
				_texture = tex;
				return true;
			}

			Object.DestroyImmediate(tex);
			return false;
		}

		/// <summary>Destroy the owned texture, if any.</summary>
		public void Clear()
		{
			if (_texture != null)
			{
				Object.DestroyImmediate(_texture);
			}

			_texture = null;
		}

		/// <summary>Draw the preview aspect-fitted, capping its width at <paramref name="maxWidth"/>.</summary>
		public void Draw(float maxWidth)
		{
			if (_texture == null)
			{
				return;
			}

			var width = Mathf.Min(maxWidth, _texture.width);
			var height = width * _texture.height / Mathf.Max(1, _texture.width);
			var rect = GUILayoutUtility.GetRect(width, height, GUILayout.ExpandWidth(false));
			GUI.DrawTexture(rect, _texture, ScaleMode.ScaleToFit);
		}
	}
}
