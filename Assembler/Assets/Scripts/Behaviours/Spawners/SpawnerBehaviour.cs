using System;
using System.Collections.Generic;
using Core;
using UnityEngine;

namespace Behaviours.Spawners
{
	
	
	public class SpawnerBehaviour : GameBehaviour
	{
		private string _newObjectName;
		private string[] _newObjectTags;
		private Vector3 _spawnPosition;
		private Quaternion _spawnRotation;
		private IEnumerable<Type> _behavioursToAdd;
		
		private int _instanceCount;
		
		public override void Execute()
		{
			var gameObject = new GameObject
			{
				name = $"{_newObjectName} {_instanceCount++}"
			};

			gameObject.AddComponent<GameEntity>().Tags = _newObjectTags;
			
			gameObject.transform.position = _spawnPosition;
			gameObject.transform.rotation = _spawnRotation;
			
			foreach (var behaviourType in _behavioursToAdd)
			{
				gameObject.AddComponent(behaviourType);
			}
		}
	}
}