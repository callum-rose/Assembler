using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.UI;

namespace Assembler.Behaviours.UI.Views
{
	/// <summary>
	/// Typed handle to the on-screen button prefab, wired in the prefab. <see cref="Bind"/> points the
	/// <see cref="OnScreenButton"/> at the control path its action is bound to under <c>mobile</c>;
	/// <see cref="SetLabel"/> sets the optional caption.
	/// </summary>
	public sealed class OnScreenButtonView : MonoBehaviour
	{
		[SerializeField] private OnScreenButton button = null!;
		[SerializeField] private Image image = null!;
		[SerializeField] private TMP_Text label = null!;

		public OnScreenButton Button => button;
		public Image Image => image;
		public TMP_Text Label => label;

		public void Bind(string controlPath) => button.controlPath = controlPath;

		public void SetLabel(string text) => label.text = text;
	}
}
