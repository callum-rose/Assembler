using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.Spawners;
using Assembler.Extensions;
using Assembler.Parsing;
using Assembler.Parsing.Info;
using Assembler.Resolving;
using UnityEngine;

namespace Assembler.Building
{
	public class GameEntityFactory : IEntitySpawner
	{
		private const string SpawnedIdPrefix = "$spawn$";

		private readonly VariableRegistry _variables;
		private readonly CompiledExpressionsRegistry _expressions;
		private readonly BehaviourRegistry _behaviourRegistry;
		private readonly AssetRegistry _assets;
		private readonly IReadOnlyDictionary<string, EntityInfo> _templates;
		private readonly IReadOnlyList<ValueInfo> _allValues;
		private readonly TriggerContext _triggerContext;

		private int _spawnCounter;

		public GameEntityFactory(VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			BehaviourRegistry behaviourRegistry,
			AssetRegistry assets,
			IReadOnlyDictionary<string, EntityInfo> templates,
			IReadOnlyList<ValueInfo> allValues,
			TriggerContext triggerContext)
		{
			_variables = variables;
			_expressions = expressions;
			_behaviourRegistry = behaviourRegistry;
			_assets = assets;
			_templates = templates;
			_allValues = allValues;
			_triggerContext = triggerContext;
		}

		public EntityBuildResult Create(EntityInfo entityInfo)
		{
			var gameObject = new GameObject(entityInfo.Id)
			{
				transform =
				{
					position = entityInfo.InitialPosition.Resolve(_variables, _expressions, _assets).Value,
					rotation = entityInfo.InitialRotation.Resolve(_variables, _expressions, _assets).Value.FromEuler()
				}
			};

			var gameEntity = gameObject.AddComponent<GameEntity>();
			gameEntity.Tags = entityInfo.Tags.ToArray();

			var behaviours = new List<(BehaviourDescriptor Descriptor, GameBehaviour Behaviour)>();
			var initialisations = new List<InitialiseBehaviourEvent>();

			foreach (var behaviourInfo in entityInfo.Behaviours)
			{
				var (gameBehaviour, initialise) = GameBehaviourFactory.Create(gameObject,
					behaviourInfo,
					_variables,
					_expressions,
					this,
					_assets,
					_triggerContext);

				behaviours.Add((new BehaviourDescriptor(entityInfo.Id, behaviourInfo.Id), gameBehaviour));
				initialisations.Add(initialise);
			}

			return new EntityBuildResult(behaviours, initialisations);
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

			var result = Create(entity);
			_behaviourRegistry.Register(result);

			foreach (var init in result.Initialisations)
			{
				init(_behaviourRegistry);
			}
		}
	}
}
