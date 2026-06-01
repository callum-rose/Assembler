using System;
using System.Collections.Generic;
using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Behaviours
{
	public abstract class GameBehaviour : MonoBehaviour
	{
		[SerializeField] private string[] tags = Array.Empty<string>();

		public string[] Tags
		{
			get => tags;
			set => tags = value;
		}

		protected string Id { get; private set; }

		private IReadOnlyList<Listener> _listeners = Array.Empty<Listener>();

#if DEBUG_CONSOLE
		/// <summary>
		/// Raised whenever any behaviour notifies its listeners, just before the listeners run. Lets the
		/// debug console observe the otherwise-invisible trigger flow. Compiled out unless DEBUG_CONSOLE
		/// is defined, so release builds pay nothing.
		/// </summary>
		public static event Action<GameBehaviour, TriggerContext>? Fired;

		/// <summary>The resolved listeners this behaviour notifies when it fires. Debug-only graph inspection.</summary>
		public IReadOnlyList<Listener> DebugListeners => _listeners;
#endif

		public abstract void Execute(TriggerContext ctx);

		protected void SetBase(BehaviourData behaviourData, IReadOnlyList<Listener> listeners)
		{
			Id = behaviourData.Id;
			_listeners = listeners;
		}

		protected void NotifyListeners(TriggerContext ctx)
		{
#if DEBUG_CONSOLE
			Fired?.Invoke(this, ctx);
#endif
			foreach (var listener in _listeners)
			{
				listener.Notify(ctx);
			}
		}
	}

	/// <summary>
	/// Strongly-typed <see cref="GameBehaviour"/> base that binds a behaviour to its <typeparamref name="TData"/>
	/// configuration. The resolved data is exposed via <see cref="Data"/> after <see cref="Initialise"/> runs.
	/// </summary>
	/// <typeparam name="TData">The <see cref="BehaviourData"/> type carrying this behaviour's serialized configuration.</typeparam>
	public abstract class GameBehaviour<TData> : GameBehaviour where TData : BehaviourData
	{
		protected TData Data { get; private set; }

		public void Initialise(TData data, IReadOnlyList<Listener> listeners)
		{
			Data = data;

			SetBase(data, listeners);
			OnInitialise(data);
		}

		protected virtual void OnInitialise(TData data) { }
	}
}
