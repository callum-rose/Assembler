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
		private TriggerContextHolder _contextHolder = new();

		public abstract void Execute();

		public void AttachContextHolder(TriggerContextHolder holder) => _contextHolder = holder;

		/// <summary>
		/// Sets the trigger context this behaviour will read from while executing, runs <see cref="Execute"/>,
		/// then restores whatever context was previously in scope (supports nested/reentrant chains).
		/// Called by <see cref="Listener"/> when delivering a notification.
		/// </summary>
		public void Invoke(TriggerContext ctx)
		{
			var previous = _contextHolder.Current;
			_contextHolder.Current = ctx;
			try
			{
				Execute();
			}
			finally
			{
				_contextHolder.Current = previous;
			}
		}

		protected void SetBase(BehaviourData behaviourData, IReadOnlyList<Listener> listeners)
		{
			Id = behaviourData.Id;
			_listeners = listeners;
		}

		/// <summary>
		/// The trigger context currently being processed by this behaviour. When the behaviour is executing a
		/// notification delivered by a <see cref="Listener"/>, this is the context handed in by that listener; when
		/// invoked from a Unity callback (Update, OnCollisionEnter, …) with no upstream chain, this is
		/// <see cref="TriggerContext.Empty"/>.
		/// </summary>
		protected TriggerContext IncomingContext => _contextHolder.Current;

		protected void NotifyListeners() => NotifyListeners(IncomingContext);

		protected void NotifyListeners(TriggerContext ctx)
		{
			foreach (var listener in _listeners)
			{
				listener.Notify(ctx);
			}
		}
	}

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
