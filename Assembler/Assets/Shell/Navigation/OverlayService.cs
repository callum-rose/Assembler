using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assembler.Shell.Navigation
{
	/// <inheritdoc cref="IOverlayService"/>
	/// <remarks>
	/// <para>
	/// Overlays are cached the way screens are — instantiated on first use, then deactivated rather than
	/// destroyed. It matters more here than it does for a screen: the two overlays that will be shown most, the
	/// pause sheet and the result slip, both arrive at moments where a hitch would be read as the game
	/// stuttering.
	/// </para>
	/// <para>
	/// They parent to the overlay host's full-bleed rect rather than its safe area, because a scrim that stops
	/// at the notch is not a scrim.
	/// </para>
	/// </remarks>
	public sealed class OverlayService : IOverlayService
	{
		private readonly OverlayCatalog _catalog;
		private readonly ShellRoot _shellRoot;

		private readonly Dictionary<OverlayId, OverlayView> _instances = new();

		private OverlayId? _current;
		private bool _busy;

		public OverlayService(OverlayCatalog catalog, ShellRoot shellRoot)
		{
			_catalog = catalog;
			_shellRoot = shellRoot;
		}

		public OverlayId? Current => _current;

		public bool IsShowing => _current.HasValue;

		public async Awaitable Show<TOverlay>(OverlayId id, Action<TOverlay>? configure = null)
			where TOverlay : OverlayView
		{
			if (!Begin($"show {id}"))
			{
				return;
			}

			try
			{
				await Close();

				var view = Obtain(id);

				if (view == null)
				{
					return;
				}

				if (configure is not null)
				{
					if (view is TOverlay typed)
					{
						configure.Invoke(typed);
					}
					else
					{
						Debug.LogError(
							$"{nameof(OverlayService)}: {id} is a {view.GetType().Name}, not a " +
							$"{typeof(TOverlay).Name}. It is opening unbound.",
							view);
					}
				}

				_current = id;
				view.transform.SetAsLastSibling();
				view.gameObject.SetActive(true);

				await view.OnShow();
			}
			finally
			{
				_busy = false;
			}
		}

		public Awaitable Show(OverlayId id)
		{
			return Show<OverlayView>(id);
		}

		public async Awaitable Dismiss()
		{
			if (!IsShowing)
			{
				return;
			}

			if (!Begin("dismiss"))
			{
				return;
			}

			try
			{
				await Close();
			}
			finally
			{
				_busy = false;
			}
		}

		private bool Begin(string request)
		{
			if (_busy)
			{
				Debug.LogWarning(
					$"{nameof(OverlayService)}: ignoring '{request}' — the overlay layer is mid-animation.");
				return false;
			}

			_busy = true;
			return true;
		}

		private async Awaitable Close()
		{
			if (_current is not { } id || !_instances.TryGetValue(id, out var view) || view == null)
			{
				_current = null;
				return;
			}

			_current = null;

			await view.OnDismiss();
			view.gameObject.SetActive(false);
		}

		private OverlayView? Obtain(OverlayId id)
		{
			if (_instances.TryGetValue(id, out var cached) && cached != null)
			{
				return cached;
			}

			var entry = _catalog.Find(id);
			var prefab = entry == null ? null : entry.View;

			if (prefab == null)
			{
				Debug.LogError(
					$"{nameof(OverlayService)}: no prefab for {id}. Add a row to the overlay catalog.",
					_catalog);
				return null;
			}

			var view = Object.Instantiate(prefab, _shellRoot.OverlayHost.Rect);
			view.name = prefab.name;
			view.gameObject.SetActive(false);

			_instances[id] = view;
			view.Dismissed += OnDismissRequested;

			return view;
		}

		// async void because it answers an event, which cannot return an Awaitable. The whole body is wrapped:
		// an exception escaping an async void is unhandled.
		private async void OnDismissRequested()
		{
			try
			{
				await Dismiss();
			}
			catch (Exception exception)
			{
				Debug.LogError($"{nameof(OverlayService)}: dismissing failed. {exception}");
			}
		}
	}
}
