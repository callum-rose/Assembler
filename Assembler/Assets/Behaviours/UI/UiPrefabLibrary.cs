using UnityEngine;

namespace Assembler.Behaviours.UI
{
	/// <summary>
	/// Holds the reusable uGUI prefabs that the leaf UI blocks (button, label, slider) instantiate at
	/// runtime. The default asset lives at <c>Resources/UI/UiPrefabLibrary</c> and is loaded once by the
	/// Builder; a game can supply an alternative library to re-theme the whole UI without code changes.
	/// </summary>
	[CreateAssetMenu(fileName = "UiPrefabLibrary", menuName = "Assembler/UI Prefab Library")]
	public sealed class UiPrefabLibrary : ScriptableObject
	{
		/// <summary>Resources path (sans extension) the Builder loads the default library from.</summary>
		public const string DefaultResourcePath = "UI/UiPrefabLibrary";

		[SerializeField] private GameObject buttonPrefab = null!;
		[SerializeField] private GameObject labelPrefab = null!;
		[SerializeField] private GameObject sliderPrefab = null!;
		[SerializeField] private GameObject joystickPrefab = null!;
		[SerializeField] private GameObject dpadPrefab = null!;
		[SerializeField] private GameObject mobileButtonPrefab = null!;

		public GameObject ButtonPrefab => buttonPrefab;
		public GameObject LabelPrefab => labelPrefab;
		public GameObject SliderPrefab => sliderPrefab;

		/// <summary>On-screen analogue joystick prefab (carries <c>OnScreenJoystickView</c>).</summary>
		public GameObject JoystickPrefab => joystickPrefab;

		/// <summary>On-screen four-way d-pad prefab (carries <c>OnScreenDPadView</c>).</summary>
		public GameObject DPadPrefab => dpadPrefab;

		/// <summary>On-screen touch button prefab (carries <c>OnScreenButtonView</c>).</summary>
		public GameObject MobileButtonPrefab => mobileButtonPrefab;
	}
}
