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
		private readonly EntityTransformRegistry _entityTransforms;
		private readonly IReadOnlyDictionary<string, EntityInfo> _templates;
		private readonly IReadOnlyList<ValueInfo> _allValues;
		private readonly TriggerContext _triggerContext;

		private int _spawnCounter;

		public GameEntityFactory(VariableRegistry variables,
			CompiledExpressionsRegistry expressions,
			BehaviourRegistry behaviourRegistry,
			AssetRegistry assets,
			EntityTransformRegistry entityTransforms,
			IReadOnlyDictionary<string, EntityInfo> templates,
			IReadOnlyList<ValueInfo> allValues,
			TriggerContext triggerContext)
		{
			_variables = variables;
			_expressions = expressions;
			_behaviourRegistry = behaviourRegistry;
			_assets = assets;
			_entityTransforms = entityTransforms;
			_templates = templates;
			_allValues = allValues;
			_triggerContext = triggerContext;
		}

		public EntityBuildResult Create(ConcreteEntityInfo entityInfo) => Create(entityInfo, null);

		public EntityBuildResult Create(ConcreteEntityInfo entityInfo, Transform? parent)
		{
			var scope = EntityVariableScope.Create(entityInfo.Variables);

			var gameObject = new GameObject(entityInfo.Id)
			{
				transform =
				{
					position = entityInfo.InitialPosition.Resolve(_variables, _expressions, _assets, new TriggerContext(), scope, _entityTransforms).Value,
					rotation = entityInfo.InitialRotation.Resolve(_variables, _expressions, _assets, new TriggerContext(), scope, _entityTransforms).Value.FromEuler()
				}
			};

			if (parent != null)
			{
				gameObject.transform.SetParent(parent, worldPositionStays: false);
			}

			_entityTransforms.Register(entityInfo.Id, gameObject.transform);

			var gameEntity = gameObject.AddComponent<GameEntity>();
			gameEntity.Tags = entityInfo.Tags.ToArray();
			gameEntity.VariableScope = scope;

			var behaviours = new List<(BehaviourDescriptor Descriptor, GameBehaviour Behaviour, IReadOnlyList<string> BehaviourTags)>();
			var initialisations = new List<InitialiseBehaviourEvent>();

			foreach (var behaviourInfo in entityInfo.Behaviours)
			{
				var (gameBehaviour, initialise) = GameBehaviourFactory.Create(gameObject,
					behaviourInfo,
					_variables,
					_expressions,
					this,
					_assets,
					_triggerContext,
					_entityTransforms,
					scope);

				gameBehaviour.Tags = behaviourInfo.Tags.ToArray();

				behaviours.Add((new BehaviourDescriptor(entityInfo.Id, behaviourInfo.Id), gameBehaviour, behaviourInfo.Tags));
				initialisations.Add(initialise);
			}

			foreach (var child in entityInfo.Children)
			{
				var childId = child.AbsoluteId ?? $"{entityInfo.Id}/{child.IdSuffix}";

				EntityInfo childTemplate;

				if (child.TemplateRefId != null)
				{
					if (!_templates.TryGetValue(child.TemplateRefId, out childTemplate!))
					{
						throw new InvalidOperationException(
							$"Child '{childId}' references unknown template id '{child.TemplateRefId}'");
					}
				}
				else
				{
					childTemplate = NullEntityInfo.Instance;
				}

				var resolvedChild = TemplateInstantiator.Instantiate(
					childTemplate,
					childId,
					_allValues,
					child.InitialPosition,
					child.InitialRotation,
					child.Parameters,
					child.Tags,
					child.Behaviours,
					child.Variables,
					child.Children);

				var childResult = Create(resolvedChild, gameObject.transform);

				behaviours.AddRange(childResult.Behaviours);
				initialisations.AddRange(childResult.Initialisations);
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

			var entity = TemplateInstantiator.Instantiate(
				template,
				newId,
				_allValues,
				new ConstantSource<Vector3>(position),
				new ConstantSource<Vector3>(rotation),
				parameters: new Dictionary<string, AssemblerValue>(),
				runtimeParameters: parameters);

			var result = Create(entity);
			_behaviourRegistry.Register(result);

			foreach (var init in result.Initialisations)
			{
				init(_behaviourRegistry);
			}
		}
	}
}
