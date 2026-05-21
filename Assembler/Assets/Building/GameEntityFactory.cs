using System;
using System.Collections.Generic;
using System.Linq;
using Assembler.Behaviours;
using Assembler.Behaviours.Spawners;
using Assembler.Building.Pooling;
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
		private readonly EntityPool _pool;

		private int _spawnCounter;

		public GameEntityFactory(VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			BehaviourRegistry behaviourRegistry,
			AssetRegistry assets,
			IReadOnlyDictionary<string, EntityInfo> templates,
			IReadOnlyList<ValueInfo> allValues,
			TriggerContext triggerContext,
			EntityPool pool)
		{
			_variables = variables;
			_expressions = expressions;
			_behaviourRegistry = behaviourRegistry;
			_assets = assets;
			_templates = templates;
			_allValues = allValues;
			_triggerContext = triggerContext;
			_pool = pool;
		}

		public EntityBuildResult Create(ConcreteEntityInfo entityInfo)
		{
			var scope = EntityVariableScope.Create(entityInfo.Variables);

			var gameObject = new GameObject(entityInfo.Id)
			{
				transform =
				{
					position = entityInfo.InitialPosition.Resolve(_variables, _expressions, _assets, new TriggerContext(), scope).Value,
					rotation = entityInfo.InitialRotation.Resolve(_variables, _expressions, _assets, new TriggerContext(), scope).Value.FromEuler()
				}
			};

			var gameEntity = gameObject.AddComponent<GameEntity>();
			gameEntity.Tags = entityInfo.Tags.ToArray();
			gameEntity.VariableScope = scope;
			gameEntity.EntityId = entityInfo.Id;

			return BuildBehaviours(gameObject, gameEntity, entityInfo, scope, existingBehaviours: null);
		}

		private EntityBuildResult BuildBehaviours(GameObject gameObject,
			GameEntity gameEntity,
			ConcreteEntityInfo entityInfo,
			EntityVariableScope scope,
			IReadOnlyList<GameBehaviour>? existingBehaviours)
		{
			var behaviours = new List<(BehaviourDescriptor Descriptor, GameBehaviour Behaviour, IReadOnlyList<string> BehaviourTags)>();
			var initialisations = new List<InitialiseBehaviourEvent>();

			for (var i = 0; i < entityInfo.Behaviours.Count; i++)
			{
				var behaviourInfo = entityInfo.Behaviours[i];
				var existing = existingBehaviours != null && i < existingBehaviours.Count ? existingBehaviours[i] : null;

				var (gameBehaviour, initialise) = GameBehaviourFactory.Create(gameObject,
					behaviourInfo,
					_variables,
					_expressions,
					this,
					_assets,
					_triggerContext,
					scope,
					existing);

				gameBehaviour.Tags = behaviourInfo.Tags.ToArray();

				behaviours.Add((new BehaviourDescriptor(entityInfo.Id, behaviourInfo.Id), gameBehaviour, behaviourInfo.Tags));
				initialisations.Add(initialise);
			}

			return new EntityBuildResult(behaviours, initialisations);
		}

		public void Spawn(string templateId, Vector3 position, Vector3 rotation, IReadOnlyDictionary<string, object> parameters)
		{
			if (!_templates.TryGetValue(templateId, out var template))
			{
				throw new InvalidOperationException($"No template registered with id '{templateId}'");
			}

			var newId = $"{SpawnedIdPrefix}{templateId}_{_spawnCounter++}";

			var entityInfo = TemplateInstantiator.Instantiate(
				template,
				newId,
				_allValues,
				new ConstantSource<Vector3>(position),
				new ConstantSource<Vector3>(rotation),
				parameters: new Dictionary<string, AssemblerValue>(),
				runtimeParameters: parameters);

			EntityBuildResult result;
			bool wasPooled = _pool.TryRent(templateId, out var pooled);

			if (wasPooled)
			{
				result = Rehydrate(pooled, entityInfo, templateId);
			}
			else
			{
				result = Create(entityInfo);
				var gameEntity = result.Behaviours.Count > 0
					? result.Behaviours[0].Behaviour.GetComponent<GameEntity>()
					: null;
				if (gameEntity != null)
				{
					gameEntity.TemplateId = templateId;
				}
			}

			_behaviourRegistry.Register(result);

			foreach (var init in result.Initialisations)
			{
				init(_behaviourRegistry);
			}

			if (wasPooled)
			{
				pooled.Entity.gameObject.SetActive(true);

				foreach (var (_, behaviour, _) in result.Behaviours)
				{
					try
					{
						behaviour.OnPostInitialise();
					}
					catch (Exception e)
					{
						Debug.LogException(e);
					}
				}
			}
		}

		private EntityBuildResult Rehydrate(PooledEntity pooled, ConcreteEntityInfo entityInfo, string templateId)
		{
			var gameEntity = pooled.Entity;

			var scope = EntityVariableScope.Create(entityInfo.Variables);

			gameEntity.VariableScope = scope;
			gameEntity.Tags = entityInfo.Tags.ToArray();
			gameEntity.TemplateId = templateId;
			gameEntity.EntityId = entityInfo.Id;
			pooled.Entity.gameObject.name = entityInfo.Id;

			pooled.Entity.gameObject.transform.SetParent(null, worldPositionStays: false);
			pooled.Entity.gameObject.transform.SetPositionAndRotation(
				entityInfo.InitialPosition.Resolve(_variables, _expressions, _assets, new TriggerContext(), scope).Value,
				entityInfo.InitialRotation.Resolve(_variables, _expressions, _assets, new TriggerContext(), scope).Value.FromEuler());

			return BuildBehaviours(pooled.Entity.gameObject, gameEntity, entityInfo, scope, pooled.Behaviours);
		}

		public void Despawn(GameEntity entity)
		{
			if (entity == null) return;

			var gameObject = entity.gameObject;
			var entityId = entity.EntityId;
			var templateId = entity.TemplateId;

			var behaviours = gameObject.GetComponents<GameBehaviour>();

			foreach (var b in behaviours)
			{
				try
				{
					b.OnDespawn();
				}
				catch (Exception e)
				{
					Debug.LogException(e);
				}
			}

			if (entityId != null)
			{
				_behaviourRegistry.Unregister(entityId);
			}

			entity.VariableScope?.Dispose();
			entity.VariableScope = null;

			if (string.IsNullOrEmpty(templateId))
			{
				UnityEngine.Object.Destroy(gameObject);
				return;
			}

			_pool.Return(templateId, new PooledEntity(entity, behaviours));
		}
	}
}
