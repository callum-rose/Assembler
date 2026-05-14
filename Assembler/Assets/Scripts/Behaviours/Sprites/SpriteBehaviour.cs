using Assembler.Core;
using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Behaviours.Sprites
{
	public class SpriteBehaviour : GameBehaviour<SpriteData>
	{
		protected override void OnInitialise(SpriteData data)
		{
			var spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
			spriteRenderer.sprite = data.Sprite.Value;
		}

		public override void Execute()
		{
		}
	}
}