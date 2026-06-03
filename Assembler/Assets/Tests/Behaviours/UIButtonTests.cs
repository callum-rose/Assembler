using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.UI;
using Assembler.Behaviours.UI.Views;
using Assembler.Resolving;
using Assembler.Resolving.Behaviours;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Tests.Behaviours
{
	public class UIButtonTests
	{
		private sealed class ActionListener : Listener
		{
			private readonly Action<TriggerContext> _action;

			public ActionListener(Action<TriggerContext> action)
				: base(new Dictionary<string, string>()) => _action = action;

			public override void Notify(TriggerContext ctx) => _action(Prepare(ctx));

			public override IEnumerable<GameBehaviour> DebugTargets() => Enumerable.Empty<GameBehaviour>();
		}

		// Builds a minimal stand-in for a button prefab: a Button + a child TMP label, with the view wired.
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

		// The view's references are [SerializeField] private (read-only public getters), so wire them the
		// same way the editor/inspector does — through SerializedObject.
		private static void Wire(UnityEngine.Object target, string field, UnityEngine.Object value)
		{
			var serialized = new UnityEditor.SerializedObject(target);
			serialized.FindProperty(field).objectReferenceValue = value;
			serialized.ApplyModifiedPropertiesWithoutUndo();
		}

		[Test]
		public void Click_NotifiesListeners()
		{
			var prefab = CreateButtonPrefab();
			var entity = new GameObject("ButtonEntity");

			try
			{
				var uiButton = entity.AddComponent<UIButton>();

				var fired = 0;
				var listener = new ActionListener(_ => fired++);

				var data = new UIButtonData(
					id: "test_button",
					label: new ValueProvider<string>("Go"),
					preferredWidth: new ValueProvider<float>(0f),
					preferredHeight: new ValueProvider<float>(0f),
					prefab: prefab);

				uiButton.Initialise(data, new List<Listener> { listener });

				// The block instantiates its own copy of the prefab under the entity; click that instance.
				var instantiatedButton = entity.GetComponentInChildren<Button>();
				Assert.IsNotNull(instantiatedButton, "UIButton should instantiate a button from the prefab.");

				instantiatedButton.onClick.Invoke();
				instantiatedButton.onClick.Invoke();

				Assert.AreEqual(2, fired, "Each click should notify listeners exactly once.");
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(entity);
				UnityEngine.Object.DestroyImmediate(prefab);
			}
		}

		[Test]
		public void Initialise_UpgradesEntityToRectTransform()
		{
			var prefab = CreateButtonPrefab();
			var entity = new GameObject("ButtonEntity");

			try
			{
				var uiButton = entity.AddComponent<UIButton>();
				uiButton.Initialise(
					new UIButtonData("b", new ValueProvider<string>(""),
						new ValueProvider<float>(0f), new ValueProvider<float>(0f), prefab),
					new List<Listener>());

				Assert.IsInstanceOf<RectTransform>(entity.transform,
					"UI blocks must upgrade their entity GameObject to a RectTransform for canvas layout.");
			}
			finally
			{
				UnityEngine.Object.DestroyImmediate(entity);
				UnityEngine.Object.DestroyImmediate(prefab);
			}
		}
	}
}
