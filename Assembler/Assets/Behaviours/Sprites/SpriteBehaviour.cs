using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.Sprites
{
	public class SpriteBehaviour : GameBehaviour<SpriteData>
	{
		protected override void OnInitialise(SpriteData data)
		{
			var spriteGo = new GameObject("Sprite");
			spriteGo.transform.SetParent(transform, false);

			var spriteRenderer = spriteGo.AddComponent<SpriteRenderer>();
			spriteRenderer.sprite = data.Sprite.Value;

			data.Size.UseIfValueExists(size =>
			{
				var sprite = data.Sprite.Value;
				var nativeSize = sprite.rect.size / sprite.pixelsPerUnit;
				spriteGo.transform.localScale = new Vector3(size.x / nativeSize.x, size.y / nativeSize.y, 1f);
			});
		}

		public override void Execute() { }
	}
}