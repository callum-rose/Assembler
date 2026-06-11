using UnityEngine;
using UnityEngine.InputSystem.OnScreen;

namespace Assembler.Behaviours.UI.Views
{
	/// <summary>
	/// Typed handle to the on-screen d-pad prefab: four <see cref="OnScreenButton"/>s wired in the prefab.
	/// <see cref="Bind"/> takes the action's base <c>mobile</c> path (a Vector2 control such as
	/// <c>&lt;Gamepad&gt;/dpad</c>) and points each button at the matching directional sub-control, so the four
	/// presses combine back into the Vector2 the action reads.
	/// </summary>
	public sealed class OnScreenDPadView : MonoBehaviour
	{
		[SerializeField] private OnScreenButton up = null!;
		[SerializeField] private OnScreenButton down = null!;
		[SerializeField] private OnScreenButton left = null!;
		[SerializeField] private OnScreenButton right = null!;

		public OnScreenButton Up => up;
		public OnScreenButton Down => down;
		public OnScreenButton Left => left;
		public OnScreenButton Right => right;

		public void Bind(string basePath)
		{
			up.controlPath = $"{basePath}/up";
			down.controlPath = $"{basePath}/down";
			left.controlPath = $"{basePath}/left";
			right.controlPath = $"{basePath}/right";
		}
	}
}
