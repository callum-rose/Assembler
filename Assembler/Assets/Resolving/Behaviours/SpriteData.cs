using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Resolving.Behaviours
{
	public class SpriteData : BehaviourData
	{
		public IValueProvider<Sprite> Sprite { get; }
		public IValueProvider<Vector2> Size { get; }

		public SpriteData(string id,
						IValueProvider<Sprite> sprite,
			IValueProvider<Vector2> size) : base(id) =>
			(Sprite, Size) = (sprite, size);
	}
}