using System;
using System.Linq;
using Assembler.Parsing2.Dtos;
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
		
		public void Initialise(EntityDto entityDto)
		{
			Tags = entityDto.Tags?.ToArray() ?? Array.Empty<string>();

			foreach (var behaviourDto in entityDto.Behaviours ?? Enumerable.Empty<BehaviourDto>())
			{
				
			}
		}
	}
}