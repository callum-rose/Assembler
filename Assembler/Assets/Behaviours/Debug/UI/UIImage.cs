using Assembler.Resolving.Behaviours;
using Assembler.Parsing;
using UnityEngine;

namespace Assembler.Behaviours.Debug.UI
{
	public class UIImage : GameBehaviour<UIImageData>
	{
		private Texture2D _tex;

		protected override void OnInitialise(UIImageData data)
		{
			_tex = new Texture2D(1, 1);
		}

		public override void Execute() { }

		private void OnGUI()
		{
			if (Data == null) return;
			_tex.SetPixel(0, 0, Data.Colour.Value);
			_tex.Apply();
			GUI.DrawTexture(Data.Rect.ToUnityRect(), _tex);
		}

		private void OnDestroy()
		{
			if (_tex != null) Destroy(_tex);
		}
	}
}
