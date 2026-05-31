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

		public abstract void Execute(TriggerContext ctx);

		protected void SetBase(BehaviourData behaviourData, IReadOnlyList<Listener> listeners)
		{
			Id = behaviourData.Id;
			_listeners = listeners;
		}

		protected void NotifyListeners(TriggerContext ctx)
		{
			foreach (var listener in _listeners)
			{
				listener.Notify(ctx);
			}
		}

		/// <summary>Notifies only the listeners on the given branch channel (matching <see cref="Listener.When"/>).</summary>
		protected void NotifyListeners(TriggerContext ctx, bool branch)
		{
			foreach (var listener in _listeners)
			{
				if (listener.When == branch)
				{
					listener.Notify(ctx);
				}
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
