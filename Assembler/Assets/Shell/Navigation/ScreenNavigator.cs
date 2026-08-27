using System;
using System.Collections.Generic;
using EasyDI.Instantiation;
using EasyDI.Resolving;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assembler.Shell.Navigation
{
	/// <inheritdoc cref="INavigator"/>
	/// <remarks>
	/// <para>
	/// <b>The stack is data; the instances are a cache.</b> The stack holds nothing but ids and the arguments
	/// they were pushed with, and every screen re-binds from its entry's argument on arrival. The two
	/// consequences are worth having: a screen pushed twice with different arguments is correct both times off
	/// one instance, and dropping an instance — the <see cref="ScreenView.KeepAlive"/> escape hatch of
	/// UIPLAN 3.2 — cannot lose your place in the stack.
	/// </para>
	/// <para>
	/// <b>Screens are instantiated on first visit and then kept</b>, deactivated rather than destroyed
	/// (UIPLAN 3.2), which is what makes the feed's scroll position and the archive's search text survive a
	/// round trip for free.
	/// </para>
	/// <para>
	/// <b>A transition in flight refuses the next request rather than queueing it.</b> The guard is taken
	/// synchronously, before the first await, so the second of two clicks in one frame is dropped — which is the
	/// case that matters, a double-tapped button pushing the same screen twice.
	/// </para>
	/// </remarks>
	public sealed class ScreenNavigator : INavigator
	{
		private readonly ScreenCatalog _catalog;
		private readonly ShellRoot _shellRoot;
		private readonly IObjectResolver _resolver;

		private readonly List<StackEntry> _stack = new();
		private readonly Dictionary<ScreenId, ScreenInstance> _instances = new();

		public ScreenNavigator(ScreenCatalog catalog, ShellRoot shellRoot, IObjectResolver resolver)
		{
			_catalog = catalog;
			_shellRoot = shellRoot;
			_resolver = resolver;
		}

		public ScreenId? Current => _stack.Count == 0 ? null : _stack[_stack.Count - 1].Id;

		public ScreenId? Beneath => _stack.Count < 2 ? null : _stack[_stack.Count - 2].Id;

		public int Depth => _stack.Count;

		public bool CanPop => _stack.Count > 1;

		public bool IsTransitioning { get; private set; }

		public async Awaitable Push(ScreenId id, IScreenParams? parameters = null)
		{
			if (!Begin($"push {id}"))
			{
				return;
			}

			try
			{
				var leaving = Top();
				_stack.Add(new StackEntry(id, parameters));

				await Leave(leaving);
				await Arrive(_stack[_stack.Count - 1]);
			}
			finally
			{
				IsTransitioning = false;
			}
		}

		public async Awaitable Pop()
		{
			if (!CanPop)
			{
				Debug.LogWarning(
					$"{nameof(ScreenNavigator)}: nothing to pop to — {Current} is the root of the stack.");
				return;
			}

			if (!Begin("pop"))
			{
				return;
			}

			try
			{
				var leaving = _stack[_stack.Count - 1];
				_stack.RemoveAt(_stack.Count - 1);

				await Leave(leaving);
				await Arrive(_stack[_stack.Count - 1]);
			}
			finally
			{
				IsTransitioning = false;
			}
		}

		public async Awaitable Replace(ScreenId id, IScreenParams? parameters = null)
		{
			if (!Begin($"replace with {id}"))
			{
				return;
			}

			try
			{
				var leaving = Top();
				var arriving = new StackEntry(id, parameters);

				if (_stack.Count == 0)
				{
					_stack.Add(arriving);
				}
				else
				{
					_stack[_stack.Count - 1] = arriving;
				}

				await Leave(leaving);
				await Arrive(arriving);
			}
			finally
			{
				IsTransitioning = false;
			}
		}

		private bool Begin(string request)
		{
			if (IsTransitioning)
			{
				Debug.LogWarning(
					$"{nameof(ScreenNavigator)}: ignoring '{request}' — a transition is already running.");
				return false;
			}

			IsTransitioning = true;
			return true;
		}

		private StackEntry? Top()
		{
			return _stack.Count == 0 ? null : _stack[_stack.Count - 1];
		}

		private async Awaitable Leave(StackEntry? entry)
		{
			if (entry is null || !_instances.TryGetValue(entry.Id, out var instance) || instance.View == null)
			{
				return;
			}

			instance.View.SetInteractive(false);
			await instance.View.OnExit();

			// After the exit, not before: a screen stays bound while it is on its way out, so nothing it is
			// still drawing goes stale mid-fade.
			instance.Presenter?.Exit();
			instance.View.gameObject.SetActive(false);

			if (!instance.View.KeepAlive)
			{
				Discard(entry.Id);
			}
		}

		private async Awaitable Arrive(StackEntry entry)
		{
			var instance = Obtain(entry.Id);

			if (instance is null)
			{
				return;
			}

			var view = instance.View;
			view.Title = _catalog.TitleOf(entry.Id);
			LabelBackControl(view);

			// Prepared before it is activated, so activating cannot flash a fully-drawn page for the frame
			// before the fade starts; last in its parent, so it draws over whatever is still on its way out.
			view.PrepareEnter();
			view.transform.SetAsLastSibling();
			view.gameObject.SetActive(true);

			// Bound before the entrance, so the page is right by the time any of it is visible.
			instance.Presenter?.Enter(entry.Parameters);

			await view.OnEnter();
			view.SetInteractive(true);
		}

		// The label names the entry beneath the top of the stack, not this screen (UIPLAN 3.3) — "The Archive"
		// on a detail page reached from the archive, "Front Page" on the same page reached from the feed.
		private void LabelBackControl(ScreenView view)
		{
			var back = view.BackButton;

			if (back == null)
			{
				return;
			}

			var beneath = Beneath;
			back.gameObject.SetActive(beneath.HasValue);

			if (beneath.HasValue)
			{
				back.Text = _catalog.TitleOf(beneath.Value);
			}
		}

		private ScreenInstance? Obtain(ScreenId id)
		{
			// Checked for life as well as presence: a screen destroyed from outside — a scene unload part-way
			// through a transition — would otherwise be handed back as a live instance.
			if (_instances.TryGetValue(id, out var cached) && cached.View != null)
			{
				return cached;
			}

			var entry = _catalog.Find(id);
			var prefab = entry == null ? null : entry.View;

			if (prefab == null)
			{
				Debug.LogError(
					$"{nameof(ScreenNavigator)}: no prefab for {id}. Add a row to the screen catalog.",
					_catalog);
				return null;
			}

			var view = Object.Instantiate(prefab, _shellRoot.ScreenHost.SafeArea);
			view.name = prefab.name;
			view.gameObject.SetActive(false);

			var instance = new ScreenInstance(view, CreatePresenter(view));
			_instances[id] = instance;

			// Wired once, with the instance, rather than on every arrival — the button is part of the view and
			// lives exactly as long as it does.
			if (view.BackButton != null)
			{
				view.BackButton.OnClick.AddListener(OnBackClicked);
			}

			return instance;
		}

		private IScreenPresenter? CreatePresenter(ScreenView view)
		{
			var presenterType = view.PresenterType;

			if (presenterType is null)
			{
				return null;
			}

			try
			{
				// The view's concrete type, not ScreenView: EasyDI matches an additional argument on the exact
				// type it was declared with, so a presenter taking `FeedView` needs the argument typed as one.
				var arguments = new[] { new ArgumentInfo(view.GetType(), view) };

				return (IScreenPresenter)_resolver.Instantiate(presenterType, arguments);
			}
			catch (Exception exception)
			{
				Debug.LogError(
					$"{nameof(ScreenNavigator)}: could not build {presenterType.Name} for '{view.name}'. The " +
					$"screen will open with nothing driving it. {exception}",
					view);
				return null;
			}
		}

		private void Discard(ScreenId id)
		{
			if (!_instances.TryGetValue(id, out var instance))
			{
				return;
			}

			_instances.Remove(id);

			if (instance.View == null)
			{
				return;
			}

			if (instance.View.BackButton != null)
			{
				instance.View.BackButton.OnClick.RemoveListener(OnBackClicked);
			}

			Object.Destroy(instance.View.gameObject);
		}

		// async void because it is a UnityEvent handler, which cannot return an Awaitable. The whole body is
		// wrapped: an exception escaping an async void is unhandled.
		private async void OnBackClicked()
		{
			try
			{
				await Pop();
			}
			catch (Exception exception)
			{
				Debug.LogError($"{nameof(ScreenNavigator)}: going back failed. {exception}");
			}
		}

		/// <summary>One rung of the stack: which screen, and what it was pushed with.</summary>
		private sealed record StackEntry(ScreenId Id, IScreenParams? Parameters);

		/// <summary>One live screen: the view, and the presenter built alongside it.</summary>
		private sealed record ScreenInstance(ScreenView View, IScreenPresenter? Presenter);
	}
}
