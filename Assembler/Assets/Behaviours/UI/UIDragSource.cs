using Assembler.Behaviours.Triggers;
using Assembler.Behaviours.UI.Internal;
using Assembler.Behaviours.UI.Views;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using UnityEngine;

namespace Assembler.Behaviours.UI
{
	/// <summary>A draggable uGUI widget. Acts as a trigger, but unlike <c>ui button</c> (which fires only on
	/// click) it reports the whole pointer lifecycle — press, drag, release — as a screen-space position stream.
	/// A press can therefore begin a drag whose position feeds another behaviour (e.g. a board's placement
	/// logic) as the pointer moves over the play area. The caption is re-read every frame, so it can be bound to
	/// a variable/expression.</summary>
	/// <remarks>
	/// Properties:
	///   Label: Caption (re-read each frame).
	///   PreferredWidth: Preferred width for the parent layout (omit for a sensible default).
	///   PreferredHeight: Preferred height for the parent layout (omit for a sensible default).
	/// Outputs:
	///   phase [string]: Lifecycle stage of this fire — "press", "drag", or "release".
	///   position [Vector3]: Current screen-space pointer position (z is 0).
	///   start [Vector3]: Screen-space position where the press began (z is 0).
	///   delta [Vector3]: Screen-space movement since the previous drag frame (z is 0).
	/// </remarks>
	public class UIDragSource : Trigger<UIDragSourceData>
	{
		// See UIButton/TextLabel: the content is a stretch-to-fill child prefab, so fall back to a non-zero
		// default size when the descriptor omits a dimension to avoid the element collapsing to zero.
		private const float DefaultWidth = 160f;
		private const float DefaultHeight = 40f;

		// The drag source renders with the shared button prefab; it only needs the label + a raycast graphic,
		// both of which the button view already carries.
		private UiButtonView? _view;
		private Vector3 _startPosition;
		private Vector3 _lastPosition;

		protected override void OnInitialise(UIDragSourceData data)
		{
			var host = UiLayout.EnsureRectTransform(gameObject);
			UiLayout.ApplyPreferredSize(gameObject,
				data.PreferredWidth.ValueOr(DefaultWidth),
				data.PreferredHeight.ValueOr(DefaultHeight));
			_view = UiLayout.InstantiateView<UiButtonView>(data.Prefab, host);

			var relay = _view.gameObject.AddComponent<UiPointerDragRelay>();
			relay.Pressed += OnPressed;
			relay.DragBegan += OnDragBegan;
			relay.Dragged += OnDragged;
			relay.Released += OnReleased;
		}

		private void Update()
		{
			if (_view != null)
			{
				_view.Label.text = Data.Label.ValueOr(string.Empty);
			}
		}

		private void OnPressed(Vector3 position)
		{
			_startPosition = position;
			_lastPosition = position;
			Emit("press", position, Vector3.zero);
		}

		// Drag events only start once the pointer clears the EventSystem drag threshold; rebase here so the
		// first drag delta is measured from where dragging actually began, not the press point.
		private void OnDragBegan(Vector3 position) => _lastPosition = position;

		private void OnDragged(Vector3 position) => EmitMoved("drag", position);

		private void OnReleased(Vector3 position) => EmitMoved("release", position);

		private void EmitMoved(string phase, Vector3 position)
		{
			var delta = position - _lastPosition;
			_lastPosition = position;
			Emit(phase, position, delta);
		}

		private void Emit(string phase, Vector3 position, Vector3 delta) =>
			NotifyListeners(TriggerContext.New(b =>
			{
				b["phase"] = phase;
				b["position"] = position;
				b["start"] = _startPosition;
				b["delta"] = delta;
			}));
	}
}
