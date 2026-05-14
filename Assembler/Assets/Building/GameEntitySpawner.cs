using System;
using System.Collections.Generic;
using Assembler.Behaviours;
using Assembler.Behaviours.Spawners;
using Assembler.Core;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Building
{
	internal sealed class GameEntitySpawner : IEntitySpawner
	{
		private const string SpawnedIdPrefix = "$spawn$";

		private readonly VariableRegistry _variables;
		private readonly CompiledExpressionsRegistry _expressions;
		private readonly IReadOnlyDictionary<string, EntityInfo> _templatesById;
		private readonly IReadOnlyList<VariableInfo> _allValues;
		private readonly Dictionary<BehaviourDescriptor, GameBehaviour> _behaviourRegistry;
		private int _spawnCounter;

		public GameEntitySpawner(
			VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			IReadOnlyDictionary<string, EntityInfo> templatesById,
			IReadOnlyList<VariableInfo> allValues,
			Dictionary<BehaviourDescriptor, GameBehaviour> behaviourRegistry)
		{
			_variables = variables;
			_expressions = expressions;
			_templatesById = templatesById;
			_allValues = allValues;
			_behaviourRegistry = behaviourRegistry;
		}

		public void Spawn(string templateId, Vector3 position)
		{
			if (!_templatesById.TryGetValue(templateId, out var template))
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
			GameEntityFactory.Create(entity, _variables, _expressions, this, _behaviourRegistry, inits);

			foreach (var init in inits)
			{
				init(_behaviourRegistry);
			}
		}
	}
}
