using UnityEngine;
using UnityEngine.UI;

namespace Assembler.Behaviours.UI.Views
{
	/// <summary>Typed handle to the <see cref="Slider"/> of the slider prefab, wired in the prefab.</summary>
	public sealed class UiSliderView : MonoBehaviour
	{
		// Wired in the prefab; never null at runtime.
		[SerializeField] private Slider slider = null!;

		public Slider Slider => slider;
	}
}
