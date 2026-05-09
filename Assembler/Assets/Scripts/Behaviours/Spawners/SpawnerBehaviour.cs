using Assembler.Parsing.Phase2.Parsing.Phase2.Info;
using AssemblerAlpha.Core;
using UnityEngine;

namespace AssemblerAlpha.Behaviours.Spawners
{
	
	
	public class SpawnerBehaviour : GameBehaviour<SpawnerInfo>
	{
		private string _newObjectName;
		private string[] _newObjectTags;
		private Vector3 _spawnPosition;
		private Quaternion _spawnRotation;
		private System.Collections.Generic.IEnumerable<System.Type> _behavioursToAdd;
		
		private int _instanceCount;

		protected override void OnInitialise(SpawnerInfo behaviourInfo)
		{
		}

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