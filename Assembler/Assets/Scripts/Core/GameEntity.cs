using System;
using System.Linq;
using Parsing.Phase1.Dtos;
using UnityEngine;

namespace AssemblerAlpha.Core
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