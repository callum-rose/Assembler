using UnityEngine;
using UnityEngine.UI;

namespace Assembler.Behaviours.UI.Views
{
	/// <summary>Typed handle to the <see cref="Slider"/> of the slider prefab, wired in the prefab.</summary>
	public sealed class UiSliderView : MonoBehaviour
	{
		// Assigned in the prefab; never null at runtime.
		public Slider Slider = null!;
	}
}
