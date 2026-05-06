using System;
using UnityEngine;

namespace Core
{
	public sealed class GameEntity : MonoBehaviour
	{
		[SerializeField] private string[] tags = Array.Empty<string>();

		public string[] Tags
		{
			get => tags;
			set => tags = value;
		}

		private void Start()
		{
			foreach (var gameBehaviour in GetComponents<GameBehaviour>())
			{
				gameBehaviour.Initialise(this);
			}
		}
	}
}