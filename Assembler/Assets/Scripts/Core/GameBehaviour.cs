using System.Collections.Generic;
using Assembler.Generators.Attributes;
using UnityEngine;

namespace Core
{
	[GenerateEnumFromTypeHierarchy]
	[GenerateDocumentation]
	public abstract class GameBehaviour : MonoBehaviour
	{
		protected GameEntity Entity { get; private set; }

		public virtual void Inject(Queue<object> args) { }

		public void Initialise(GameEntity entity)
		{
			Entity = entity;

			OnInitialise();
		}

		public abstract void Execute();

		protected virtual void OnInitialise() { }
	}
}