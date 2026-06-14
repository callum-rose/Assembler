using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Sprites
{
	/// <summary>Renders a 2D sprite as a child of the entity, optionally rescaled to <c>Size</c>.</summary>
	/// <remarks>
	/// Properties:
	///   Sprite: Asset reference to the sprite to display.
	///   Size: Target world-space size in units; the sprite is scaled to fit.
	/// </remarks>
	public class SpriteBehaviour : GameBehaviour<SpriteData>
	{
		private GameObject _spriteGo;
		private SpriteRenderer _spriteRenderer;

		protected override void OnInitialise(SpriteData data)
		{
			// Create the sprite child + renderer once and reuse them across pooled lives: OnInitialise re-runs on
			// every reuse, so a guard keeps the single sprite child rather than spawning a duplicate each life, and
			// re-points the persisted renderer at this spawn's sprite. A guard rather than Awake because Awake does
			// not run in edit mode (the sandbox validator / EditMode tests build via OnInitialise).
			if (_spriteGo == null)
			{
				_spriteGo = new GameObject("Sprite");
				_spriteGo.transform.SetParent(transform, false);
				_spriteRenderer = _spriteGo.AddComponent<SpriteRenderer>();
			}

			_spriteRenderer.sprite = data.Sprite.Get();

			data.Size.UseIfValueExists(size =>
			{
				var sprite = data.Sprite.Get();
				var nativeSize = sprite.rect.size / sprite.pixelsPerUnit;
				_spriteGo.transform.localScale = new Vector3(size.x / nativeSize.x, size.y / nativeSize.y, 1f);
			});
		}
	}
}
