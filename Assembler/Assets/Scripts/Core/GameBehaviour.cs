using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using UnityEngine;

namespace AssemblerAlpha.Core
{

	public static class GameBehaviourFactory
	{
		public static void Create(GameObject gameObject, BehaviourInfo behaviour)
		{
			switch (behaviour)
			{
				case BoxColliderInfo boxColliderInfo:
					var boxCollider = gameObject.AddComponent<BoxCollider>();
					boxCollider.size = boxColliderInfo.Size.ToUnity();
					boxCollider.isTrigger = boxColliderInfo.IsTrigger;
					break;
			}
		}
	}

	public abstract class GameBehaviour<T> : MonoBehaviour where T : BehaviourInfo
	{
		protected GameEntity Entity { get; private set; }

		public void Initialise(GameEntity entity, T behaviourInfo)
		{
			Entity = entity;

			OnInitialise(behaviourInfo);
		}

		public abstract void Execute();

		protected virtual void OnInitialise(T behaviourInfo) { }
	}
}