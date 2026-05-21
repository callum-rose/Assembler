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

		private readonly Dictionary<GameEntity, ActiveEntry> _active = new();

		private int _spawnCounter;

		private readonly struct ActiveEntry
		{
			public ActiveEntry(string entityId, string? templateId)
			{
				EntityId = entityId;
				TemplateId = templateId;
			}

			public string EntityId { get; }
			public string? TemplateId { get; }
		}

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
			return Create(entityInfo, templateId: null);
		}

		private EntityBuildResult Create(ConcreteEntityInfo entityInfo, string? templateId)
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

			_active[gameEntity] = new ActiveEntry(entityInfo.Id, templateId);

			return BuildBehaviours(gameObject, entityInfo, scope, existingBehaviours: null);
		}

		private EntityBuildResult BuildBehaviours(GameObject gameObject,
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

			bool wasPooled = _pool.TryRent(templateId, out var pooled);
			var result = wasPooled ? 
				Rehydrate(pooled, entityInfo, templateId) : 
				Create(entityInfo, templateId);

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
			gameEntity.gameObject.name = entityInfo.Id;

			_active[gameEntity] = new ActiveEntry(entityInfo.Id, templateId);

			gameEntity.gameObject.transform.SetParent(null, worldPositionStays: false);
			gameEntity.gameObject.transform.SetPositionAndRotation(
				entityInfo.InitialPosition.Resolve(_variables, _expressions, _assets, new TriggerContext(), scope).Value,
				entityInfo.InitialRotation.Resolve(_variables, _expressions, _assets, new TriggerContext(), scope).Value.FromEuler());

			return BuildBehaviours(gameEntity.gameObject, entityInfo, scope, pooled.Behaviours);
		}

		public void Despawn(GameEntity entity)
		{
			if (entity == null) return;

			if (!_active.Remove(entity, out var info))
			{
				UnityEngine.Object.Destroy(entity.gameObject);
				return;
			}

			var behaviours = entity.gameObject.GetComponents<GameBehaviour>();

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

			_behaviourRegistry.Unregister(info.EntityId);

			entity.VariableScope?.Dispose();
			entity.VariableScope = null;

			if (string.IsNullOrEmpty(info.TemplateId))
			{
				UnityEngine.Object.Destroy(entity.gameObject);
				return;
			}

			_pool.Return(info.TemplateId, new PooledEntity(entity, behaviours));
		}
	}
}
