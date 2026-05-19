using UnityEngine;

namespace Assembler.Resolving
{
	public enum AnchorPoint { TopLeft, TopRight, BottomLeft, BottomRight, Center }

	public struct ScreenRect
	{
		public AnchorPoint Anchor;
		public float X, Y, Width, Height;

		public Rect ToUnityRect()
		{
			float ox = Anchor switch
			{
				AnchorPoint.TopRight or AnchorPoint.BottomRight => Screen.width,
				AnchorPoint.Center => Screen.width * 0.5f,
				_ => 0f
			};
			float oy = Anchor switch
			{
				AnchorPoint.BottomLeft or AnchorPoint.BottomRight => Screen.height,
				AnchorPoint.Center => Screen.height * 0.5f,
				_ => 0f
			};
			float left = ox + X;
			float top = Anchor is AnchorPoint.BottomLeft or AnchorPoint.BottomRight
				? oy - Y - Height
				: oy + Y;
			return new Rect(left, top, Width, Height);
		}
	}
}
