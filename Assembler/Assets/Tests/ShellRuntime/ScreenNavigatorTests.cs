using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Assembler.Shell;
using Assembler.Shell.Layout;
using Assembler.Shell.Navigation;
using DG.Tweening;
using EasyDI.Registering;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Shell
{
	/// <summary>
	/// The back stack's semantics: what is on top, what is underneath, which instances survive being left, and
	/// what each screen is handed on the way in.
	/// </summary>
	/// <remarks>
	/// <b>Play mode, not edit mode.</b> A transition is a DOTween tween, and DOTween only works once there is a
	/// running player — in the editor it hands out tweens that can be created but never killed, so every await
	/// here would hang.
	/// </remarks>
	public class ScreenNavigatorTests
	{
		private readonly List<GameObject> _objects = new();
		private readonly List<ScriptableObject> _assets = new();
		private readonly Dictionary<ScreenId, StubScreen> _sources = new();

		private ScreenNavigator _navigator = null!;
		private ShellRoot _shellRoot = null!;

		[SetUp]
		public void SetUp()
		{
			DOTween.Init();
			StubPresenter.Log.Clear();

			_shellRoot = BuildShellRoot();
			var catalog = BuildCatalog();

			// A bare root registry: the only thing a stub presenter takes is its view, and that arrives as an
			// additional argument rather than a registration.
			_navigator = new ScreenNavigator(catalog, _shellRoot, ObjectRegistry.CreateRoot().Build());
		}

		[TearDown]
		public void TearDown()
		{
			DOTween.KillAll();
			_sources.Clear();

			foreach (var created in _objects)
			{
				if (created != null)
				{
					Object.DestroyImmediate(created);
				}
			}

			foreach (var asset in _assets)
			{
				if (asset != null)
				{
					Object.DestroyImmediate(asset);
				}
			}

			_objects.Clear();
			_assets.Clear();
		}

		[UnityTest]
		public IEnumerator PushingOntoAnEmptyStackMakesTheRoot()
		{
			yield return Await(_navigator.Push(ScreenId.Feed));

			Assert.AreEqual(ScreenId.Feed, _navigator.Current);
			Assert.AreEqual(1, _navigator.Depth);
			Assert.IsFalse(_navigator.CanPop);
			Assert.IsNull(_navigator.Beneath);
			Assert.IsTrue(Instance(ScreenId.Feed).gameObject.activeSelf);
		}

		// UIPLAN 3.2: instantiate on first visit, then keep. The scroll position a cached screen holds onto is
		// the whole reason, and it only works if leaving really is deactivation rather than destruction.
		[UnityTest]
		public IEnumerator BuildsAScreenOnceAndThenKeepsIt()
		{
			yield return Await(_navigator.Push(ScreenId.Feed));
			var feed = Instance(ScreenId.Feed);

			yield return Await(_navigator.Push(ScreenId.Detail));

			Assert.IsTrue(feed != null, "the screen left behind should not have been destroyed");
			Assert.IsFalse(feed.gameObject.activeSelf);

			yield return Await(_navigator.Pop());

			Assert.AreSame(feed, Instance(ScreenId.Feed));
			Assert.IsTrue(feed.gameObject.activeSelf);
		}

		[UnityTest]
		public IEnumerator PopReturnsToTheEntryBeneath()
		{
			yield return Await(_navigator.Push(ScreenId.Feed));
			yield return Await(_navigator.Push(ScreenId.Archive));

			Assert.AreEqual(ScreenId.Feed, _navigator.Beneath, "the back control names this one");

			yield return Await(_navigator.Pop());

			Assert.AreEqual(ScreenId.Feed, _navigator.Current);
			Assert.AreEqual(1, _navigator.Depth);
		}

		[UnityTest]
		public IEnumerator ReplaceSwapsTheTopWithoutDeepening()
		{
			yield return Await(_navigator.Push(ScreenId.Feed));
			yield return Await(_navigator.Push(ScreenId.Detail));
			yield return Await(_navigator.Replace(ScreenId.Archive));

			Assert.AreEqual(ScreenId.Archive, _navigator.Current);
			Assert.AreEqual(2, _navigator.Depth);
			Assert.AreEqual(ScreenId.Feed, _navigator.Beneath);
		}

		// The ordering is the contract: a screen is bound before it is visible and unbound after it has gone, so
		// nothing it draws is ever stale while somebody can see it.
		[UnityTest]
		public IEnumerator BindsOnEveryEntryAndUnbindsAfterEveryExit()
		{
			yield return Await(_navigator.Push(ScreenId.Feed));
			yield return Await(_navigator.Push(ScreenId.Detail, new StubParams("edition-014")));
			yield return Await(_navigator.Replace(ScreenId.Detail, new StubParams("edition-013")));

			CollectionAssert.AreEqual(
				new[]
				{
					"enter Feed -",
					"exit Feed",
					"enter Detail edition-014",
					"exit Detail",
					"enter Detail edition-013"
				},
				StubPresenter.Log);
		}

		// The escape hatch of UIPLAN 3.2, and the reason the stack holds ids rather than instances: dropping the
		// instance must not lose the argument it was pushed with.
		[UnityTest]
		public IEnumerator RebuildsAScreenThatDoesNotKeepAlive()
		{
			_sources[ScreenId.Archive].keepAlive = false;

			yield return Await(_navigator.Push(ScreenId.Feed));
			yield return Await(_navigator.Push(ScreenId.Archive, new StubParams("index")));
			var first = Instance(ScreenId.Archive);

			yield return Await(_navigator.Push(ScreenId.Detail));

			Assert.IsTrue(first == null, "a screen that does not keep alive is destroyed on the way out");

			yield return Await(_navigator.Pop());

			Assert.IsTrue(Instance(ScreenId.Archive) != null, "and rebuilt on the way back");
			Assert.AreEqual("enter Archive index", StubPresenter.Log[StubPresenter.Log.Count - 1]);
		}

		// The case that matters: a double-tapped button. The guard is taken before the first await, so the
		// second request is refused rather than queued behind the first.
		[UnityTest]
		public IEnumerator RefusesASecondRequestWhileATransitionIsRunning()
		{
			yield return Await(_navigator.Push(ScreenId.Feed));

			var first = _navigator.Push(ScreenId.Detail);

			LogAssert.Expect(LogType.Warning, new Regex("a transition is already running"));
			var second = _navigator.Push(ScreenId.Archive);

			yield return Await(first);
			yield return Await(second);

			Assert.AreEqual(ScreenId.Detail, _navigator.Current);
			Assert.AreEqual(2, _navigator.Depth);
		}

		[UnityTest]
		public IEnumerator RefusesToPopTheRoot()
		{
			yield return Await(_navigator.Push(ScreenId.Feed));

			LogAssert.Expect(LogType.Warning, new Regex("nothing to pop to"));
			yield return Await(_navigator.Pop());

			Assert.AreEqual(ScreenId.Feed, _navigator.Current);
			Assert.AreEqual(1, _navigator.Depth);
		}

		private static IEnumerator Await(Awaitable awaitable)
		{
			var awaiter = awaitable.GetAwaiter();
			float deadline = Time.realtimeSinceStartup + 5f;

			while (!awaiter.IsCompleted && Time.realtimeSinceStartup < deadline)
			{
				yield return null;
			}

			Assert.IsTrue(awaiter.IsCompleted, "the transition never finished");
			awaiter.GetResult();
		}

		private StubScreen Instance(ScreenId id)
		{
			var found = _shellRoot.ScreenHost.SafeArea.Find(id.ToString());

			return found == null ? null! : found.GetComponent<StubScreen>();
		}

		private ShellRoot BuildShellRoot()
		{
			var rootObject = new GameObject("ShellRoot", typeof(RectTransform), typeof(Canvas));
			_objects.Add(rootObject);

			var root = rootObject.AddComponent<ShellRoot>();
			ShellReflection.Set(root, "rootCanvas", rootObject.GetComponent<Canvas>());
			ShellReflection.Set(root, "screenHost", Host(rootObject.transform, "ScreenHost"));
			ShellReflection.Set(root, "gameStrip", Host(rootObject.transform, "GameStrip"));
			ShellReflection.Set(root, "overlayHost", Host(rootObject.transform, "OverlayHost"));

			return root;
		}

		private static ShellHost Host(Transform parent, string name)
		{
			var hostObject = new GameObject(name, typeof(RectTransform), typeof(Canvas));
			hostObject.transform.SetParent(parent, worldPositionStays: false);

			var safeAreaObject = new GameObject("SafeArea", typeof(RectTransform));
			safeAreaObject.transform.SetParent(hostObject.transform, worldPositionStays: false);

			var host = hostObject.AddComponent<ShellHost>();
			ShellReflection.Set(host, "canvas", hostObject.GetComponent<Canvas>());
			ShellReflection.Set(host, "rect", hostObject.GetComponent<RectTransform>());
			ShellReflection.Set(host, "safeArea", safeAreaObject.AddComponent<SafeAreaPanel>());

			return host;
		}

		private ScreenCatalog BuildCatalog()
		{
			var rows = new List<ScreenCatalog.Entry>();

			foreach (ScreenId id in Enum.GetValues(typeof(ScreenId)))
			{
				var source = Source(id.ToString());
				_sources[id] = source;

				var row = new ScreenCatalog.Entry();
				ShellReflection.Set(row, "id", id);
				ShellReflection.Set(row, "view", source);
				ShellReflection.Set(row, "title", id.ToString());
				rows.Add(row);
			}

			var catalog = ScriptableObject.CreateInstance<ScreenCatalog>();
			_assets.Add(catalog);

			return ShellReflection.Set(catalog, "entries", rows.ToArray());
		}

		// Deactivated, so it behaves like the prefab it stands in for: instantiating it yields something the
		// navigator activates itself rather than something that was already running.
		private StubScreen Source(string name)
		{
			var hostObject = new GameObject(name, typeof(RectTransform));
			hostObject.SetActive(false);
			_objects.Add(hostObject);

			var group = hostObject.AddComponent<CanvasGroup>();
			var view = hostObject.AddComponent<StubScreen>();
			ShellReflection.Set(view, "canvasGroup", group);

			return view;
		}

		private sealed class StubParams : IScreenParams
		{
			public StubParams(string value)
			{
				Value = value;
			}

			public string Value { get; }
		}

		private sealed class StubScreen : ScreenView<StubPresenter>
		{
			[SerializeField] public bool keepAlive = true;

			public override bool KeepAlive => keepAlive;
		}

		private sealed class StubPresenter : IScreenPresenter
		{
			public static readonly List<string> Log = new();

			private readonly StubScreen _view;

			public StubPresenter(StubScreen view)
			{
				_view = view;
			}

			public void Enter(IScreenParams? parameters)
			{
				string argument = parameters is StubParams stub ? stub.Value : "-";
				Log.Add($"enter {_view.name} {argument}");
			}

			public void Exit()
			{
				Log.Add($"exit {_view.name}");
			}
		}
	}
}
