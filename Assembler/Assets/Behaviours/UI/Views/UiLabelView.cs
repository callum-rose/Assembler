using TMPro;
using UnityEngine;

namespace Assembler.Behaviours.UI.Views
{
	/// <summary>Typed handle to the text element of the label prefab, wired in the prefab.</summary>
	public sealed class UiLabelView : MonoBehaviour
	{
		[SerializeField] private TMP_Text text = null!;

		public TMP_Text Text => text;
	}
}
