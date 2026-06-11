using UnityEngine;
using UnityEngine.InputSystem.OnScreen;

namespace Assembler.Behaviours.UI.Views
{
	/// <summary>
	/// Typed handle to the on-screen joystick prefab, wired in the prefab so the builder never relies on child
	/// lookups. <see cref="Bind"/> points the <see cref="OnScreenStick"/> at the control path its action is bound
	/// to under <c>mobile</c>; the stick then synthesises a virtual stick the existing binding reads.
	/// </summary>
	public sealed class OnScreenJoystickView : MonoBehaviour
	{
		[SerializeField] private OnScreenStick stick = null!;
		[SerializeField] private RectTransform background = null!;
		[SerializeField] private RectTransform handle = null!;

		public OnScreenStick Stick => stick;
		public RectTransform Background => background;
		public RectTransform Handle => handle;

		public void Bind(string controlPath) => stick.controlPath = controlPath;
	}
}
