using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.UI;
using Assembler.Behaviours.UI.Internal;
using Assembler.Behaviours.UI.Views;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Tests.Behaviours
{
	public class UIDragSourceTests
	{
		private sealed class ActionListener : Listener
		{
			private readonly Action<TriggerContext> _action;

			public ActionListener(Action<TriggerContext> action)
				: base(new Dictionary<string, string>()) => _action = action;

			public override void Notify(TriggerContext ctx) => _action(Prepare(ctx));

#if DEBUG_CONSOLE
			public override IEnumerable<GameBehaviour> DebugTargets() => Enumerable.Empty<GameBehaviour>();
#endif
		}

		// The drag source renders with the shared button prefab, so build the same minimal stand-in the button
		// tests use: a raycast Image + a child TMP label, with the view wired.
		private static GameObject CreateButtonPrefab()
		{
			var root = new GameObject("FakeButton", typeof(RectTransform));
			root.AddComponent<Image>();
			var button = root.AddComponent<Button>();

			var labelGo = new GameObject("Label", typeof(RectTransform));
			labelGo.transform.SetParent(root.transform, worldPositionStays: false);
			var label = labelGo.AddComponent<TextMeshProUGUI>();

			var view = root.AddComponent<UiButtonView>();
			Wire(view, "button", button);
			Wire(view, "label", label);
			return root;
		}

		private static void Wire(UnityEngine.Object target, string field, UnityEngine.Object value)
		{
			var serialized = new UnityEditor.SerializedObject(target);
			serialized.FindProperty(field).objectReferenceValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		private static PointerEventData At(Vector2 position) =>
			new(EventSystem.current) { position = position };

		[Test]
		public void PressDragRelease_EmitsPhasePositionStartAndDelta()
		{
			var prefab = CreateButtonPrefab();
			var entity = new GameObject("DragSourceEntity");

			try
			{
				var dragSource = entity.AddComponent<UIDragSource>();

				var contexts = new List<TriggerContext>();
				var listener = new ActionListener(ctx => contexts.Add(ctx));

				dragSource.Initialise(
					new UIDragSourceData("test_drag",
						new ValueProvider<string>("Tower"),
						new ValueProvider<float>(0f),
						new ValueProvider<float>(0f),
						prefab),
					new List<Listener> { listener });

				var relay = entity.GetComponentInChildren<UiPointerDragRelay>();
				Assert.IsNotNull(relay, "UIDragSource should attach a pointer relay to the instantiated view.");

				var press = new Vector3(10f, 20f, 0f);
				var beginDrag = new Vector3(40f, 20f, 0f);
				var drag = new Vector3(55f, 25f, 0f);
				var release = new Vector3(60f, 30f, 0f);

				relay.OnPointerDown(At(press));
				relay.OnBeginDrag(At(beginDrag)); // rebases the delta baseline; does not itself emit
				relay.OnDrag(At(drag));
				relay.OnPointerUp(At(release));

				Assert.AreEqual(3, contexts.Count, "Expected press, drag and release fires (begin-drag rebases only).");

				AssertPhase(contexts[0], "press", position: press, start: press, delta: Vector3.zero);
				AssertPhase(contexts[1], "drag", position: drag, start: press, delta: drag - beginDrag);
				AssertPhase(contexts[2], "release", position: release, start: press, delta: release - drag);
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(entity);
				UnityEngine.Object.DestroyImmediate(prefab);
			}
		}

		private static void AssertPhase(TriggerContext ctx, string phase, Vector3 position, Vector3 start, Vector3 delta)
		{
			Assert.IsTrue(ctx.TryGet<string>("phase", out var actualPhase));
			Assert.AreEqual(phase, actualPhase, "phase output");

			Assert.IsTrue(ctx.TryGet<Vector3>("position", out var actualPosition));
			Assert.AreEqual(position, actualPosition, "position output");

			Assert.IsTrue(ctx.TryGet<Vector3>("start", out var actualStart));
			Assert.AreEqual(start, actualStart, "start output");

			Assert.IsTrue(ctx.TryGet<Vector3>("delta", out var actualDelta));
			Assert.AreEqual(delta, actualDelta, "delta output");
		}
	}
}
