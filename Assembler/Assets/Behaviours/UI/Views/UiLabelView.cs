using TMPro;
using UnityEngine;

namespace Assembler.Behaviours.UI.Views
{
	/// <summary>Typed handle to the text element of the label prefab, wired in the prefab.</summary>
	public sealed class UiLabelView : MonoBehaviour
	{
		// Assigned in the prefab; never null at runtime.
		public TMP_Text Text = null!;
	}
}
