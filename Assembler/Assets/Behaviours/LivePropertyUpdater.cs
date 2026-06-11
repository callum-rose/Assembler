using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assembler.Behaviours
{
	/// <summary>
	/// Central per-game driver that re-applies polled live properties once per frame. A behaviour registers a
	/// tick (via <see cref="LivePropertyBindingExtensions.BindLive{T}"/>) only for a genuinely non-observable
	/// provider (clock/query/transform/partial expression); observable providers (variables, constants,
	/// all-observable expressions) take a push path and never register here. With an empty list,
	/// <see cref="Update"/> costs ~nothing — a constant-bound property pays no per-frame cost.
	/// </summary>
	public sealed class LivePropertyUpdater : MonoBehaviour
	{
		private readonly List<Action> _ticks = new();

		private void Update()
		{
			// Indexed loop: a tick re-applies a property and never mutates the tick list itself.
			for (int i = 0; i < _ticks.Count; i++)
			{
				_ticks[i]();
			}
		}

		/// <summary>Register a per-frame tick. Returns a handle that removes it when disposed.</summary>
		public IDisposable Register(Action tick)
		{
			_ticks.Add(tick);
			return new Registration(this, tick);
		}

		private sealed class Registration : IDisposable
		{
			private readonly LivePropertyUpdater _updater;
			private Action? _tick;

			public Registration(LivePropertyUpdater updater, Action tick) => (_updater, _tick) = (updater, tick);

			public void Dispose()
			{
				if (_tick == null)
				{
					return;
				}

				_updater._ticks.Remove(_tick);
				_tick = null;
			}
		}
	}
}
