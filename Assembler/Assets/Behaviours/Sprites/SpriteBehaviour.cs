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
		protected override void OnInitialise(SpriteData data)
		{
			var spriteGo = new GameObject("Sprite");
			spriteGo.transform.SetParent(transform, false);

			var spriteRenderer = spriteGo.AddComponent<SpriteRenderer>();
			spriteRenderer.sprite = data.Sprite.Get(TriggerContext.Empty);

			data.Size.UseIfValueExists(TriggerContext.Empty, size =>
			{
				var sprite = data.Sprite.Get(TriggerContext.Empty);
				var nativeSize = sprite.rect.size / sprite.pixelsPerUnit;
				spriteGo.transform.localScale = new Vector3(size.x / nativeSize.x, size.y / nativeSize.y, 1f);
			});
		}

		public override void Execute(TriggerContext ctx) { }
	}
}