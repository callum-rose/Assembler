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

		private IReadOnlyList<Action> _listeners;

		public abstract void Execute();

		public virtual void OnDespawn() { }

		public virtual void OnPostInitialise() { }

		protected void SetBase(BehaviourData behaviourData) => (Id, _listeners) = (behaviourData.Id, behaviourData.Listeners);

		protected void NotifyListeners()
		{
			foreach (var listener in _listeners)
			{
				listener.Invoke();
			}
		}
	}

	public abstract class GameBehaviour<TData> : GameBehaviour where TData : BehaviourData
	{
		protected TData Data { get; private set; }

		public void Initialise(TData data)
		{
			Data = data;

			SetBase(data);
			OnInitialise(data);
		}

		protected virtual void OnInitialise(TData data) { }
	}
}