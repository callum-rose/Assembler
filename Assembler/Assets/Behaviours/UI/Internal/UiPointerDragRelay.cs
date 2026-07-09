using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Assembler.Behaviours.UI.Internal
{
	/// <summary>
	/// Runtime-attached forwarder that surfaces the uGUI pointer lifecycle (press, drag begin, drag, release)
	/// on the graphic it sits on as plain C# events carrying the screen-space pointer position. Added by
	/// <see cref="UI.UIDragSource"/> to the instantiated view root. uGUI captures the pointer to the object a
	/// drag began on, so drag and release keep arriving even after the pointer leaves the widget and moves over
	/// the play area — which is what lets a UI drag hand its position off to gameplay logic.
	/// </summary>
	public sealed class UiPointerDragRelay : MonoBehaviour,
		IPointerDownHandler, IPointerUpHandler,
		IBeginDragHandler, IDragHandler
	{
		// eventData.position is a Vector2; it widens to Vector3 (z = 0) via Unity's implicit conversion, keeping
		// the project convention that 2D quantities are Vector3.
		public event Action<Vector3>? Pressed;
		public event Action<Vector3>? DragBegan;
		public event Action<Vector3>? Dragged;
		public event Action<Vector3>? Released;

		public void OnPointerDown(PointerEventData eventData) => Pressed?.Invoke(eventData.position);

		public void OnBeginDrag(PointerEventData eventData) => DragBegan?.Invoke(eventData.position);

		public void OnDrag(PointerEventData eventData) => Dragged?.Invoke(eventData.position);

		public void OnPointerUp(PointerEventData eventData) => Released?.Invoke(eventData.position);
	}
}
