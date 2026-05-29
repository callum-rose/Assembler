using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using Assembler.Parsing;
using UnityEngine;

namespace Assembler.Behaviours.Debug.UI
{
	/// <summary>Draws a solid-coloured rectangle on-screen. Useful as a simple HUD backdrop or indicator.</summary>
	/// <remarks>
	/// Properties:
	///   Colour: Fill colour.
	///   Rect: Screen-space rectangle.
	/// </remarks>
	public class UIImage : GameBehaviour<UIImageData>
	{
		private Texture2D _tex;

		protected override void OnInitialise(UIImageData data)
		{
			_tex = new Texture2D(1, 1);
		}

		public override void Execute(TriggerContext ctx) { }

		private void OnGUI()
		{
			if (Data == null) return;
			_tex.SetPixel(0, 0, Data.Colour.Get(TriggerContext.Empty));
			_tex.Apply();
			GUI.DrawTexture(Data.Rect.ToUnityRect(), _tex);
		}

		private void OnDestroy()
		{
			if (_tex != null) Destroy(_tex);
		}
	}
}
