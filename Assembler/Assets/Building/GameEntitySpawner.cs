using System;
using System.Collections.Generic;
using Assembler.Behaviours;
using Assembler.Behaviours.Spawners;
using Assembler.Core;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using UnityEngine;

namespace Assembler.Building
{
	internal sealed class GameEntitySpawner : IEntitySpawner
	{
		private const string SpawnedIdPrefix = "$spawn$";

		private readonly IReadOnlyDictionary<string, EntityInfo> _templates;
		private readonly GameEntityFactory _gameEntityFactory;
		private readonly IReadOnlyDictionary<BehaviourDescriptor, GameBehaviour> _behaviourRegistry;
		private readonly IReadOnlyList<ValueInfo> _allValues;

		private int _spawnCounter;

		public GameEntitySpawner(IReadOnlyDictionary<string, EntityInfo> templates, GameEntityFactory gameEntityFactory)
		{
			_templates = templates;
			_gameEntityFactory = gameEntityFactory;
		}

		public void Spawn(string templateId, Vector3 position)
		{
			if (!_templates.TryGetValue(templateId, out var template))
			{
				throw new InvalidOperationException($"No template registered with id '{templateId}'");
			}

			var newId = $"{SpawnedIdPrefix}{templateId}_{_spawnCounter++}";
			var parameters = new Dictionary<string, object> { ["self_id"] = newId };

			var entity = TemplateInstantiator.Instantiate(
				template,
				newId,
				new ConstantSource<Vector3>(position),
				parameters,
				_allValues);

			var inits = new List<Action<IReadOnlyDictionary<BehaviourDescriptor, GameBehaviour>>>();
			_gameEntityFactory.Create(entity, inits);

			foreach (var init in inits)
			{
				init(_behaviourRegistry);
			}
		}
	}
}
